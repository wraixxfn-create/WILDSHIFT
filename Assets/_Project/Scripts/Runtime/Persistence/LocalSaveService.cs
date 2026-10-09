using System;
using System.IO;
using System.Text;
using UnityEngine;
using Wildshift.Core.Diagnostics;
using Wildshift.Persistence.Model;

namespace Wildshift.Persistence
{
    /// <summary>
    /// Reads and writes the single local prototype save file. It owns file access, atomic writes,
    /// schema-version checks, and failure reporting; it does not own game state and never decides on
    /// its own when to save — callers trigger <see cref="Save"/> and <see cref="Load"/> explicitly.
    /// </summary>
    /// <remarks>
    /// Safety rules this service enforces:
    /// <list type="bullet">
    /// <item><description>A save is written to a temporary file and then swapped in, so an interrupted
    /// write cannot truncate the previous valid save; the replaced contents are kept as a backup.</description></item>
    /// <item><description>A payload is validated before it is written and after it is read.</description></item>
    /// <item><description>After a failed load, writing over the existing file is blocked until the
    /// caller explicitly calls <see cref="AllowOverwriteAfterFailedLoad"/>, so an unreadable save is
    /// never destroyed automatically.</description></item>
    /// <item><description>Saves closer together than <see cref="MinimumSecondsBetweenSaves"/> are
    /// rejected, which makes per-frame disk writes impossible even if a caller misbehaves.</description></item>
    /// </list>
    /// The service is not thread-safe and not a singleton: create one where the save flow is owned.
    /// </remarks>
    public sealed class LocalSaveService
    {
        /// <summary>Default minimum wall-clock gap between two writes of the save file.</summary>
        public const double DefaultMinimumSecondsBetweenSaves = 5d;

        private static readonly UTF8Encoding Utf8WithoutBom = new UTF8Encoding(false);

        private readonly SaveLocation _location;
        private readonly Func<double> _monotonicClock;
        private readonly double _minimumSecondsBetweenSaves;

        private double _lastSuccessfulSaveSeconds = double.NegativeInfinity;
        private bool _overwriteBlocked;

        /// <summary>
        /// Creates a service for one save location.
        /// </summary>
        /// <param name="location">Where the save lives; defaults to <see cref="SaveLocation.CreateDefault"/>.</param>
        /// <param name="monotonicClock">
        /// Source of monotonically increasing seconds used for write throttling. Defaults to Unity's
        /// unscaled realtime clock; tests inject a deterministic one.
        /// </param>
        /// <param name="minimumSecondsBetweenSaves">Minimum gap between two writes; must not be negative.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the minimum gap is negative or not finite.</exception>
        public LocalSaveService(
            SaveLocation location = null,
            Func<double> monotonicClock = null,
            double minimumSecondsBetweenSaves = DefaultMinimumSecondsBetweenSaves)
        {
            if (double.IsNaN(minimumSecondsBetweenSaves) ||
                double.IsInfinity(minimumSecondsBetweenSaves) ||
                minimumSecondsBetweenSaves < 0d)
            {
                throw new ArgumentOutOfRangeException(nameof(minimumSecondsBetweenSaves), minimumSecondsBetweenSaves,
                    "The minimum interval between saves must be a finite, non-negative number of seconds.");
            }

            _location = location ?? SaveLocation.CreateDefault();
            _monotonicClock = monotonicClock ?? ReadUnityRealtime;
            _minimumSecondsBetweenSaves = minimumSecondsBetweenSaves;
        }

        /// <summary>Where this service reads and writes. Developer-facing; do not print it in gameplay UI.</summary>
        public SaveLocation Location => _location;

        /// <summary>Minimum wall-clock seconds between two successful writes.</summary>
        public double MinimumSecondsBetweenSaves => _minimumSecondsBetweenSaves;

        /// <summary>True when a save file currently exists.</summary>
        public bool HasSaveFile => SafeFileExists(_location.FilePath);

        /// <summary>True when a backup of a previous save currently exists.</summary>
        public bool HasBackupFile => SafeFileExists(_location.BackupFilePath);

        /// <summary>
        /// True when the last <see cref="Load"/> failed on an existing file, so saving over it is
        /// refused until <see cref="AllowOverwriteAfterFailedLoad"/> is called.
        /// </summary>
        public bool IsOverwriteBlocked => _overwriteBlocked;

        /// <summary>
        /// Releases the block set by a failed load. Call this only from an explicit, deliberate path
        /// (for example "start a new game and overwrite the damaged save"), never automatically.
        /// </summary>
        public void AllowOverwriteAfterFailedLoad()
        {
            if (!_overwriteBlocked)
            {
                return;
            }

            _overwriteBlocked = false;
            WildshiftLog.Warning(
                $"Save overwrite protection released by an explicit request; the next save will replace " +
                $"'{_location.FilePath}' (its previous contents are kept at '{_location.BackupFilePath}').");
        }

        /// <summary>
        /// Validates and writes the payload. Returns a non-success status instead of throwing for
        /// ordinary failures; the previous file is left untouched unless the new one is written whole.
        /// </summary>
        /// <param name="data">Payload produced by a mapper from owned runtime state.</param>
        /// <param name="ignoreWriteInterval">
        /// True for deliberate, rare saves (quit, checkpoint) that must bypass the throttle. Leave it
        /// false for anything that can repeat, so disk writes stay bounded.
        /// </param>
        public SaveResult Save(SaveGameData data, bool ignoreWriteInterval = false)
        {
            if (!SaveDataValidator.TryValidate(data, out string validationError))
            {
                SaveResult invalid = SaveResult.Failure(
                    SaveStatus.InvalidData,
                    $"Refused to save: {validationError}",
                    "The game could not be saved because its state looks inconsistent.");
                WildshiftLog.Error(invalid.DeveloperMessage);
                return invalid;
            }

            if (data.SchemaVersion != SaveSchema.CurrentVersion)
            {
                SaveResult wrongVersion = SaveResult.Failure(
                    SaveStatus.InvalidData,
                    $"Refused to save a payload declaring schema version {data.SchemaVersion}; this build " +
                    $"only writes version {SaveSchema.CurrentVersion}.",
                    "The game could not be saved because its state looks inconsistent.");
                WildshiftLog.Error(wrongVersion.DeveloperMessage);
                return wrongVersion;
            }

            if (_overwriteBlocked && HasSaveFile)
            {
                SaveResult blocked = SaveResult.Failure(
                    SaveStatus.OverwriteBlocked,
                    "Refused to overwrite the existing save because the last load of it failed. Call " +
                    "AllowOverwriteAfterFailedLoad() from an explicit user choice before saving again.",
                    "The existing save could not be read, so it was not replaced.");
                WildshiftLog.Warning(blocked.DeveloperMessage);
                return blocked;
            }

            double now = ReadClock();
            double secondsSinceLastSave = now - _lastSuccessfulSaveSeconds;
            if (!ignoreWriteInterval && secondsSinceLastSave < _minimumSecondsBetweenSaves)
            {
                // Not an error: callers may request saves often; the disk just does not follow along.
                SaveResult throttled = SaveResult.Failure(
                    SaveStatus.Throttled,
                    $"Skipped save: only {secondsSinceLastSave:0.###}s since the last write, minimum is " +
                    $"{_minimumSecondsBetweenSaves:0.###}s.",
                    "The game was saved a moment ago.");
                WildshiftLog.Verbose(throttled.DeveloperMessage);
                return throttled;
            }

            string json = SaveSerializer.Serialize(data);

            try
            {
                WriteAtomically(json);
            }
            catch (Exception writeException) when (IsRecoverableIoException(writeException))
            {
                TryDeleteTemporaryFile();
                SaveResult failed = SaveResult.Failure(
                    SaveStatus.WriteFailed,
                    $"Failed to write the save to '{_location.FilePath}' " +
                    $"({writeException.GetType().Name}: {writeException.Message}). The previous save file " +
                    "was not modified.",
                    "The game could not be saved. Your previous save is unchanged.");
                WildshiftLog.Error(failed.DeveloperMessage);
                return failed;
            }

            _lastSuccessfulSaveSeconds = now;

            SaveResult success = SaveResult.Success(
                $"Saved {data} to '{_location.FilePath}'.");
            WildshiftLog.Info(success.DeveloperMessage);
            return success;
        }

        /// <summary>
        /// Reads, parses, version-checks, and validates the save file. A missing file is reported as
        /// <see cref="LoadStatus.NoSaveFile"/> and is not an error. Any other failure blocks
        /// overwriting the file until the caller explicitly allows it.
        /// </summary>
        public LoadResult Load()
        {
            LoadResult result = LoadFrom(_location.FilePath, "save file");

            // A missing file is the normal first run: a new game must still be able to save.
            _overwriteBlocked = !result.IsSuccess && result.Status != LoadStatus.NoSaveFile;

            return result;
        }

        /// <summary>
        /// Explicit recovery path: reads the backup left by the last successful replace. It is never
        /// consulted automatically, because silently continuing from older state would hide data loss.
        /// It does not change the overwrite block set by <see cref="Load"/>.
        /// </summary>
        public LoadResult LoadBackup()
        {
            return LoadFrom(_location.BackupFilePath, "save backup");
        }

        private LoadResult LoadFrom(string path, string label)
        {
            if (!SafeFileExists(path))
            {
                LoadResult missing = LoadResult.Failure(
                    LoadStatus.NoSaveFile,
                    $"No {label} exists at '{path}' yet.",
                    "No saved game was found.");
                WildshiftLog.Info(missing.DeveloperMessage);
                return missing;
            }

            string json;
            try
            {
                json = File.ReadAllText(path, Utf8WithoutBom);
            }
            catch (Exception readException) when (IsRecoverableIoException(readException))
            {
                LoadResult unreadable = LoadResult.Failure(
                    LoadStatus.ReadFailed,
                    $"Failed to read the {label} at '{path}' " +
                    $"({readException.GetType().Name}: {readException.Message}). The file was left as it is.",
                    "The saved game could not be read.");
                WildshiftLog.Error(unreadable.DeveloperMessage);
                return unreadable;
            }

            if (!SaveSerializer.TryDeserialize(json, out SaveGameData data, out string parseError))
            {
                LoadResult malformed = LoadResult.Failure(
                    LoadStatus.Malformed,
                    $"Could not parse the {label} at '{path}': {parseError} The file was left as it is.",
                    "The saved game is damaged and was not loaded.");
                WildshiftLog.Error(malformed.DeveloperMessage);
                return malformed;
            }

            string incompatibility = SaveSchema.DescribeIncompatibility(data.SchemaVersion);
            if (incompatibility != null)
            {
                LoadResult incompatible = LoadResult.Failure(
                    LoadStatus.IncompatibleVersion,
                    $"Refused to interpret the {label} at '{path}': {incompatibility} The file was left " +
                    "as it is; continue from default state or let the player choose to start over.",
                    "This saved game was made with a different version of the game and cannot be loaded.");
                WildshiftLog.Error(incompatible.DeveloperMessage);
                return incompatible;
            }

            if (!SaveDataValidator.TryValidate(data, out string validationError))
            {
                LoadResult invalid = LoadResult.Failure(
                    LoadStatus.InvalidData,
                    $"Rejected the {label} at '{path}': {validationError} Nothing was applied and the file " +
                    "was left as it is.",
                    "The saved game is damaged and was not loaded.");
                WildshiftLog.Error(invalid.DeveloperMessage);
                return invalid;
            }

            LoadResult success = LoadResult.Success(data, $"Loaded {data} from '{path}'.");
            WildshiftLog.Info(success.DeveloperMessage);
            return success;
        }

        private void WriteAtomically(string json)
        {
            Directory.CreateDirectory(_location.DirectoryPath);

            // 1. Write the whole payload to a temporary file and flush it to the device. A crash here
            //    can only damage the temporary file.
            using (FileStream stream = new FileStream(
                       _location.TemporaryFilePath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                using (StreamWriter writer = new StreamWriter(stream, Utf8WithoutBom))
                {
                    writer.Write(json);
                    writer.Flush();
                    stream.Flush(true);
                }
            }

            // 2. Swap it in. The previous contents become the backup instead of disappearing.
            if (File.Exists(_location.FilePath))
            {
                try
                {
                    File.Replace(_location.TemporaryFilePath, _location.FilePath, _location.BackupFilePath, true);
                }
                catch (Exception replaceException) when (
                    replaceException is PlatformNotSupportedException ||
                    replaceException is NotSupportedException ||
                    replaceException is IOException ||
                    replaceException is UnauthorizedAccessException)
                {
                    // Some filesystems do not support an atomic replace; fall back to moves, which
                    // still never leave the destination half-written.
                    WildshiftLog.Verbose(
                        $"Atomic file replace unavailable ({replaceException.GetType().Name}); " +
                        "falling back to a move-based swap.");
                    ReplaceByMove();
                }
            }
            else
            {
                File.Move(_location.TemporaryFilePath, _location.FilePath);
            }
        }

        private void ReplaceByMove()
        {
            if (File.Exists(_location.BackupFilePath))
            {
                File.Delete(_location.BackupFilePath);
            }

            File.Move(_location.FilePath, _location.BackupFilePath);
            File.Move(_location.TemporaryFilePath, _location.FilePath);
        }

        private void TryDeleteTemporaryFile()
        {
            try
            {
                if (File.Exists(_location.TemporaryFilePath))
                {
                    File.Delete(_location.TemporaryFilePath);
                }
            }
            catch (Exception cleanupException) when (IsRecoverableIoException(cleanupException))
            {
                WildshiftLog.Warning(
                    $"Could not remove the temporary save file '{_location.TemporaryFilePath}' " +
                    $"({cleanupException.GetType().Name}: {cleanupException.Message}).");
            }
        }

        private double ReadClock()
        {
            double now = _monotonicClock();
            if (double.IsNaN(now) || double.IsInfinity(now))
            {
                // A broken clock must not disable saving altogether; treat it as "enough time passed".
                return _lastSuccessfulSaveSeconds + _minimumSecondsBetweenSaves;
            }

            return now;
        }

        private static double ReadUnityRealtime()
        {
            return Time.realtimeSinceStartupAsDouble;
        }

        private static bool SafeFileExists(string path)
        {
            // File.Exists swallows its own errors and returns false, which is exactly what callers want.
            return !string.IsNullOrEmpty(path) && File.Exists(path);
        }

        private static bool IsRecoverableIoException(Exception exception)
        {
            return exception is IOException ||
                   exception is UnauthorizedAccessException ||
                   exception is NotSupportedException ||
                   exception is System.Security.SecurityException ||
                   exception is ArgumentException;
        }
    }
}
