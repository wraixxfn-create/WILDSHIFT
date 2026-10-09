using System;
using System.IO;
using UnityEngine;

namespace Wildshift.Persistence
{
    /// <summary>
    /// Resolves where a save file and its working files live on disk. It is a value-like description
    /// of paths only: it creates nothing, writes nothing, and reads nothing.
    /// </summary>
    /// <remarks>
    /// The default location is <c>Application.persistentDataPath</c>, the per-user, per-application
    /// folder Unity guarantees is writable on PC (on Windows,
    /// <c>%userprofile%\AppData\LocalLow\&lt;company&gt;\&lt;product&gt;</c>). Never write saves next to
    /// the executable or into the project folder: those are read-only for an installed build.
    /// Paths are developer data. Show <see cref="UserFacingDescription"/> in gameplay UI instead of a
    /// real path, so a screenshot or stream cannot leak a user name or folder layout.
    /// </remarks>
    public sealed class SaveLocation
    {
        /// <summary>Subfolder of the persistent data path that holds save files.</summary>
        public const string DefaultFolderName = "Saves";

        /// <summary>File name of the single prototype save slot. Multiple slots are out of scope for now.</summary>
        public const string DefaultFileName = "prototype-save.json";

        /// <summary>Extension of the temporary file a save is written to before it replaces the live file.</summary>
        public const string TemporaryFileExtension = ".tmp";

        /// <summary>Extension of the previous-save backup kept by an atomic replace.</summary>
        public const string BackupFileExtension = ".bak";

        /// <summary>
        /// Creates a location from an explicit directory and file name. Tests pass a temporary
        /// directory; the game uses <see cref="CreateDefault"/>.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown when the directory or file name is blank.</exception>
        public SaveLocation(string directoryPath, string fileName)
        {
            if (string.IsNullOrWhiteSpace(directoryPath))
            {
                throw new ArgumentException("A save directory path is required.", nameof(directoryPath));
            }

            if (string.IsNullOrWhiteSpace(fileName))
            {
                throw new ArgumentException("A save file name is required.", nameof(fileName));
            }

            DirectoryPath = directoryPath;
            FileName = fileName;
            FilePath = Path.Combine(directoryPath, fileName);
            TemporaryFilePath = FilePath + TemporaryFileExtension;
            BackupFilePath = FilePath + BackupFileExtension;
        }

        /// <summary>The standard per-user save location for this application on this machine.</summary>
        public static SaveLocation CreateDefault()
        {
            return new SaveLocation(
                Path.Combine(Application.persistentDataPath, DefaultFolderName),
                DefaultFileName);
        }

        /// <summary>Folder that contains the save file; developer-facing information.</summary>
        public string DirectoryPath { get; }

        /// <summary>Save file name without its folder.</summary>
        public string FileName { get; }

        /// <summary>Full path of the live save file; developer-facing information.</summary>
        public string FilePath { get; }

        /// <summary>Full path of the temporary file a save is written to first.</summary>
        public string TemporaryFilePath { get; }

        /// <summary>Full path of the backup holding the previous save contents after a successful replace.</summary>
        public string BackupFilePath { get; }

        /// <summary>
        /// Safe wording for gameplay UI and player-visible messages. It deliberately describes the
        /// location instead of revealing a path.
        /// </summary>
        public string UserFacingDescription => "the local save folder for this computer";
    }
}
