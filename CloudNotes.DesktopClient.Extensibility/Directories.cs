﻿namespace CloudNotes.DesktopClient.Extensibility
{
    using System.IO;
    using System.Windows.Forms;

    internal static class Directories
    {
        private const string CloudNotesDataFolder = "CloudNotes";
        public static string GetFullName(string fileOrDir)
        {
#if DEBUG
            return Path.Combine(Application.StartupPath, fileOrDir);
#else
            var path = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                CloudNotesDataFolder);
            return Path.Combine(path, fileOrDir);
#endif
        }
    }
}
