using System;
using System.Collections.Generic;
using System.IO;

namespace CustomisableUI
{
    // Writes to <Documents>\Mount and Blade II Bannerlord\Logs\CustomisableUI.log. Logging must never be able to break
    // the UI, so every failure here is swallowed.
    internal static class Log
    {
        private static readonly object Gate = new object();
        private static readonly string FilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "Mount and Blade II Bannerlord", "Logs", "CustomisableUI.log");

        private static readonly HashSet<string> ReportedErrors = new HashSet<string>();
        private static bool _started;

        public static string LogPath => FilePath;

        public static void Info(string message)
        {
            Write("INFO ", message);
        }

        // Each distinct error is written once, because a failing hook runs every frame.
        public static void Error(string context, Exception ex)
        {
            string text = context + ": " + ex;
            lock (Gate)
            {
                if (!ReportedErrors.Add(text))
                {
                    return;
                }
            }
            Write("ERROR", text);
        }

        private static void Write(string level, string message)
        {
            try
            {
                lock (Gate)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
                    string line = DateTime.Now.ToString("HH:mm:ss") + " " + level + " " + message + Environment.NewLine;
                    if (!_started)
                    {
                        _started = true;
                        File.WriteAllText(FilePath, line);
                    }
                    else
                    {
                        File.AppendAllText(FilePath, line);
                    }
                }
            }
            catch
            {
                // Nothing sensible to do if the log can't be written.
            }
        }
    }
}
