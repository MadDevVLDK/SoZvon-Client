using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Collections.Generic;


namespace SoZvon.ClientSettingsManager
{
    enum TypeSetting { Hotkey, ComboBox, CheckBox }
    record Setting(string Id, TypeSetting Type, string Value);
    internal class SettingsManager
    {
        readonly Main_Thread.IUser user;
        public SettingsManager(Main_Thread.IUser user)
        {
            this.user = user;

        }
    }


    [Serializable]
    public class AppSettings
    {
        public Dictionary<string, string> Hotkeys { get; set; } = new()
        {
            ["Save"] = "Ctrl+S",
            ["Open"] = "Ctrl+O",
            ["New"] = "Ctrl+N"
        };

        public string Theme { get; set; } = "Dark";
        public bool AutoSave { get; set; } = true;
        public int AutoSaveInterval { get; set; } = 5;
    }

    public class AppSettingsManager
    {
        private readonly string settingsPath;

        public AppSettingsManager()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            settingsPath = Path.Combine(appData, "MyApp", "settings.json");
            Directory.CreateDirectory(Path.GetDirectoryName(settingsPath) ?? throw new Exception("settingsPath is null"));
        }

        public AppSettings LoadSettings()
        {
            if (!File.Exists(settingsPath))
                return new AppSettings();

            try
            {
                var json = File.ReadAllText(settingsPath);
                return JsonSerializer.Deserialize<AppSettings>(json);
            }
            catch
            {
                return new AppSettings();
            }
        }

        public void SaveSettings(AppSettings settings)
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(settings, options);
            File.WriteAllText(settingsPath, json);
        }
    }
}
