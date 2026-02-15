using Newtonsoft.Json;
using System;
using System.IO;

namespace ToolCheckCmt {
    public static class AppConfigManager {
        private const string SETTINGS_FILE = "last_session.json";

        public static void SaveSettings(string tokens, string links) {
            try {
                var settings = new AppSettings { LastTokens = tokens, LastLinks = links };
                string json = JsonConvert.SerializeObject(settings, Formatting.Indented);
                File.WriteAllText(SETTINGS_FILE, json);
            } catch { }
        }

        public static AppSettings LoadSettings() {
            try {
                if (File.Exists(SETTINGS_FILE)) {
                    string json = File.ReadAllText(SETTINGS_FILE);
                    return JsonConvert.DeserializeObject<AppSettings>(json);
                }
            } catch { }
            return null;
        }

        public static void AutoCreateShortcut() {
            try {
                string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                string shortcutName = "RefineMeta.lnk";
                string shortcutPath = Path.Combine(desktopPath, shortcutName);
                string exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName;

                if (!File.Exists(shortcutPath)) {
                    Type shellType = Type.GetTypeFromProgID("WScript.Shell");
                    dynamic shell = Activator.CreateInstance(shellType);
                    dynamic shortcut = shell.CreateShortcut(shortcutPath);
                    shortcut.TargetPath = exePath;
                    shortcut.WorkingDirectory = Path.GetDirectoryName(exePath);
                    shortcut.WindowStyle = 1;
                    shortcut.Description = "Tool Check Live + Username";
                    shortcut.IconLocation = exePath + ",0";
                    shortcut.Save();
                }
            } catch { }
        }
    }
}