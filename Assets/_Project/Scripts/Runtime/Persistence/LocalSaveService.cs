using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using Wildshift.Core.Diagnostics;

namespace Wildshift.Persistence
{
    /// <summary>
    /// Reads and writes one local save file for a Unity PC build. It owns only the storage details —
    /// where the file lives, how it is written safely, which schema versions are readable, and what
    /// gets logged — while <see cref="GameSaveData"/> owns the data model and <see cref="GameSaveMapper"/>
    /// owns translation to and from live runtime state.
    ///
    /// Saving and loading are explicit operations: nothing here polls, autosaves, or writes on a
    /// frame update, and a load never writes to disk. Use <see cref="SaveWriteThrottle"/> at the
    /// composition root when a system wants periodic saves without touching the disk every frame.
    /// This class is a plain C# object owned by whoever constructs it, not a global singleton.
    /// </summary>
    public sealed class LocalSaveService
    {
        /// <summary>Save file name used when a caller does not choose one.</summary>
        public const string DefaultFileName = "wildshift_save.json";

        private const string BackupSuffix = ".bak";
        private const string TemporarySuffix = ".tmp";
        private const string UnusablePrefix = ".unusable-";

        // UTF-8 without a byte-order mark keeps the file readable by external tools and by JsonUtility.
        private static readonly Encoding SaveEncoding = new UTF8Encoding(false);

        // One lock object per save file path, shared by every service instance in this process, so two
        // services that point at the same file cannot interleave their reads and writes.
        private static readonly object FileLockTableGate = new object();
        private static readonly Dictionary<string, object> FileLocks =
            new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        private readonly string _saveDirectory;
        private readonly string _saveFilePath;
        private readonly string _backupFilePath;
        private readonly object _fileLock;

        /// <summary>
        /// Creates a service for one save file. Passing no directory uses
        /// <c>Application.persistentDataPath</c>, which on Windows standalone resolves to
        /// <c>%USERPROFILE%\AppData\LocalLow\&lt;company&gt;\&lt;product&gt;</c> and therefore survives
        /// patching and lives outside the install folder. Tests and tools pass an explicit directory.
        /// The directory is created lazily on the first save, so constructing this object touches no disk.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown when the file name is blank or contains a path.</exception>
        public LocalSaveService(string saveDirectory = null, string fileName = DefaultFileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                throw new ArgumentException("The save file name must not be empty or whitespace.", nameof(fileName));
            }

            if (fileName.IndexOfAny(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }) >= 0)
            {
                throw new ArgumentException(
                    $"The save file name '{fileName}' must be a plain file name without a directory.", nameof(fileName));
            }

            string directory = string.IsNullOrWhiteSpace(saveDirectory) ? Application.persistentDataPath : saveDirectory;
            if (string.IsNullOrWhiteSpace(directory))
            {
                throw new ArgumentException(
                    "No save directory is available. Pass one explicitly, or run inside Unity where " +
                    "Application.persistentDataPath exists.",
                    nameof(saveDirectory));
            }

            _saveDirectory = directory;
            _saveFilePath = Path.Combine(_saveDirectory, fileName);
            _backupFilePath = _saveFilePath + BackupSuffix;
            _fileLock = GetFileLock(Path.GetFullPath(_saveFilePath));
        }

        /// <summary>
        /// Directory that holds the save files. Development and diagnostics only: gameplay UI must
        /// present a <see cref="SaveLoadStatus"/>, never this path.
        /// </summary>
        public string SaveDirectory => _saveDirectory;

        /// <summary>Full path of the primary save file. Development and diagnostics only.</summary>
        public string SaveFilePath => _saveFilePath;

        /// <summary>
        /// Full path of the backup kept from the previous successful save. Development and diagnostics only.
        /// </summary>
        public string BackupFilePath => _backupFilePath;

        /// <summary>
        /// True when the primary save file exists. Existence says nothing about whether the file is
        /// readable, current, or valid; only <see cref="TryLoad"/> decides that.
        /// </summary>
        public bool SaveFileExists()
        {
            return File.Exists(_saveFilePath);
        }

        /// <summary>
        /// Writes one save. The data is validated first, then written to a temporary file that is
        /// moved into place, so an interrupted or rejected write can never leave a half-written file
        /// where the previous good save was. The previous save is kept as a backup so a corrupted
        /// primary file can still be recovered from. An existing primary that is unusable is moved
        /// aside under a unique name rather than replaced. Saves and loads on the same file are
        /// serialized. Success and failure are logged for development; the returned status is what
        /// gameplay UI should surface.
        /// </summary>
        public SaveLoadStatus TrySave(GameSaveData saveData, out string error)
        {
            lock (_fileLock)
            {
                return TrySaveUnlocked(saveData, out error);
            }
        }

        /// <summary>
        /// Reads the primary save file, falling back to the previous save kept as a backup when the
        /// primary file is missing or unusable. A file that fails to parse, fails validation, or
        /// declares an unsupported schema version is reported through the returned status and is left
        /// exactly as it was: loading never overwrites, repairs, or deletes a save. Saves and loads on
        /// the same file are serialized.
        /// </summary>
        public SaveLoadStatus TryLoad(out GameSaveData saveData, out string error)
        {
            lock (_fileLock)
            {
                return TryLoadUnlocked(out saveData, out error);
            }
        }

        private static object GetFileLock(string fullPath)
        {
            lock (FileLockTableGate)
            {
                if (!FileLocks.TryGetValue(fullPath, out object fileLock))
                {
                    fileLock = new object();
                    FileLocks.Add(fullPath, fileLock);
                }

                return fileLock;
            }
        }

        private SaveLoadStatus TrySaveUnlocked(GameSaveData saveData, out string error)
        {
            if (saveData == null)
            {
                error = "Cannot save because no save data was provided.";
                WildshiftLog.Error($"Save failed: {error}");
                return SaveLoadStatus.InvalidArgument;
            }

            if (!saveData.TryValidate(out error))
            {
                WildshiftLog.Error($"Save failed because the data is invalid: {error}");
                return SaveLoadStatus.InvalidData;
            }

            string json = JsonUtility.ToJson(saveData, true);
            SaveLoadStatus status = WriteSaveFileAtomically(json, out error);
            if (status != SaveLoadStatus.Success)
            {
                WildshiftLog.Error($"Save failed: {error}");
                return status;
            }

            WildshiftLog.Info(
                $"Saved WILDSHIFT prototype state (schema v{saveData.SchemaVersion}, " +
                $"{saveData.Regions.Count} region(s), {saveData.WorldEvents.Count} world event(s)) " +
                $"to '{_saveFilePath}'.");

            error = null;
            return SaveLoadStatus.Success;
        }

        private SaveLoadStatus TryLoadUnlocked(out GameSaveData saveData, out string error)
        {
            saveData = null;

            SaveLoadStatus primaryStatus = TryReadSaveFile(_saveFilePath, out GameSaveData primaryData, out string primaryError);
            if (primaryStatus == SaveLoadStatus.Success)
            {
                saveData = primaryData;
                error = null;
                WildshiftLog.Info(
                    $"Loaded WILDSHIFT prototype state (schema v{primaryData.SchemaVersion}, saved {primaryData.SavedAtUtc}) " +
                    $"from '{_saveFilePath}'.");
                return SaveLoadStatus.Success;
            }

            // A missing or unusable primary file is recoverable from the backup kept by the last
            // successful save. Reading the backup writes nothing. A future "delete save" operation
            // must remove the backup too, or the deleted save would be recovered from here.
            SaveLoadStatus backupStatus = TryReadSaveFile(_backupFilePath, out GameSaveData backupData, out _);
            if (backupStatus == SaveLoadStatus.Success)
            {
                saveData = backupData;
                error = null;
                WildshiftLog.Warning(
                    $"The save file '{_saveFilePath}' could not be used ({primaryError}). " +
                    $"The previous save '{_backupFilePath}' was loaded instead, and nothing was overwritten.");
                return SaveLoadStatus.RecoveredFromBackup;
            }

            if (primaryStatus == SaveLoadStatus.NoSaveFile)
            {
                // No save at all yet: normal on a first run, so this is informational rather than an error.
                error = primaryError;
                WildshiftLog.Info($"{primaryError} Starting without saved state.");
                return SaveLoadStatus.NoSaveFile;
            }

            error = primaryError;
            WildshiftLog.Error($"Loading the save failed: {primaryError}");
            return primaryStatus;
        }

        /// <summary>
        /// Reads and validates one file: missing file, unreadable bytes, unparseable content,
        /// unsupported schema version, and invalid data are all reported as distinct statuses.
        /// The version is checked before content validation so an unknown layout is never interpreted.
        /// </summary>
        private SaveLoadStatus TryReadSaveFile(string path, out GameSaveData saveData, out string error)
        {
            saveData = null;

            if (!File.Exists(path))
            {
                error = $"No save file exists at '{path}'.";
                return SaveLoadStatus.NoSaveFile;
            }

            string json;
            try
            {
                json = File.ReadAllText(path, SaveEncoding);
            }
            catch (IOException exception)
            {
                error = $"The save file '{path}' exists but could not be read: {exception.Message}";
                return SaveLoadStatus.ReadFailed;
            }
            catch (UnauthorizedAccessException exception)
            {
                error = $"The save file '{path}' exists but access was denied: {exception.Message}";
                return SaveLoadStatus.ReadFailed;
            }

            if (string.IsNullOrWhiteSpace(json))
            {
                error = $"The save file '{path}' is empty.";
                return SaveLoadStatus.MalformedData;
            }

            if (!TryParseSaveJson(json, out saveData, out error))
            {
                error = $"The save file '{path}' is not a readable WILDSHIFT save: {error}";
                return SaveLoadStatus.MalformedData;
            }

            if (!GameSaveData.IsSupportedSchemaVersion(saveData.SchemaVersion))
            {
                error = $"The save file '{path}' uses save schema version {saveData.SchemaVersion}, but this build " +
                        $"supports versions {GameSaveData.OldestSupportedSchemaVersion} to " +
                        $"{GameSaveData.CurrentSchemaVersion}. Its data was not interpreted.";
                return SaveLoadStatus.IncompatibleVersion;
            }

            if (!saveData.TryValidate(out error))
            {
                error = $"The save file '{path}' failed validation: {error}";
                return SaveLoadStatus.InvalidData;
            }

            error = null;
            return SaveLoadStatus.Success;
        }

        private static bool TryParseSaveJson(string json, out GameSaveData saveData, out string error)
        {
            saveData = null;
            try
            {
                saveData = JsonUtility.FromJson<GameSaveData>(json);
            }
            // JsonUtility reports malformed JSON inconsistently across Unity versions: some inputs
            // return null, others throw a parse exception. Both mean the same thing here.
            catch (Exception exception)
            {
                error = exception.Message;
                return false;
            }

            if (saveData == null)
            {
                error = "the file does not contain a WILDSHIFT save object";
                return false;
            }

            error = null;
            return true;
        }

        private SaveLoadStatus WriteSaveFileAtomically(string json, out string error)
        {
            string temporaryPath = _saveFilePath + TemporarySuffix;
            try
            {
                Directory.CreateDirectory(_saveDirectory);

                // Full write to a temporary file first: if anything fails here the previous save is untouched.
                File.WriteAllText(temporaryPath, json, SaveEncoding);

                if (File.Exists(_saveFilePath))
                {
                    if (TryReadSaveFile(_saveFilePath, out _, out _) == SaveLoadStatus.Success)
                    {
                        // A usable primary becomes the backup, replacing the older backup.
                        if (File.Exists(_backupFilePath))
                        {
                            File.Delete(_backupFilePath);
                        }

                        File.Move(_saveFilePath, _backupFilePath);
                    }
                    else
                    {
                        // An unusable primary (corrupt, truncated, or from another version) is never deleted
                        // or replaced. It is moved aside under a unique name, and the existing backup, which
                        // is the last save that was known to be good, stays where it is.
                        string preservedPath = FindUnusedUnusablePath();
                        File.Move(_saveFilePath, preservedPath);
                        WildshiftLog.Warning(
                            $"The existing save file was not usable, so it was kept unchanged as '{preservedPath}' " +
                            "before the new save was written. The previous good save remains the backup.");
                    }
                }

                // A rename inside one directory is the smallest possible window for an interrupted write.
                File.Move(temporaryPath, _saveFilePath);

                error = null;
                return SaveLoadStatus.Success;
            }
            catch (IOException exception)
            {
                error = $"The save file '{_saveFilePath}' could not be written: {exception.Message}";
                return SaveLoadStatus.WriteFailed;
            }
            catch (UnauthorizedAccessException exception)
            {
                error = $"The save file '{_saveFilePath}' could not be written because access was denied: {exception.Message}";
                return SaveLoadStatus.WriteFailed;
            }
            catch (ArgumentException exception)
            {
                error = $"The save file '{_saveFilePath}' could not be written because its path is invalid: {exception.Message}";
                return SaveLoadStatus.WriteFailed;
            }
            catch (NotSupportedException exception)
            {
                error = $"The save file '{_saveFilePath}' could not be written because its path is not supported: {exception.Message}";
                return SaveLoadStatus.WriteFailed;
            }
            finally
            {
                TryDeleteTemporaryFile(temporaryPath);
            }
        }

        private string FindUnusedUnusablePath()
        {
            string stamp = DateTime.UtcNow.ToString("yyyyMMdd'T'HHmmssfff'Z'", CultureInfo.InvariantCulture);
            string candidate = _saveFilePath + UnusablePrefix + stamp;
            for (int suffix = 2; File.Exists(candidate); suffix++)
            {
                candidate = _saveFilePath + UnusablePrefix + stamp + "-" + suffix.ToString(CultureInfo.InvariantCulture);
            }

            return candidate;
        }

        private static void TryDeleteTemporaryFile(string temporaryPath)
        {
            try
            {
                if (File.Exists(temporaryPath))
                {
                    File.Delete(temporaryPath);
                }
            }
            catch (IOException exception)
            {
                WildshiftLog.Warning($"The temporary save file '{temporaryPath}' could not be removed: {exception.Message}");
            }
            catch (UnauthorizedAccessException exception)
            {
                WildshiftLog.Warning($"The temporary save file '{temporaryPath}' could not be removed: {exception.Message}");
            }
        }
    }
}
