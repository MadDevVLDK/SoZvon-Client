using RoutedEventArgs = System.Windows.RoutedEventArgs;
using Page = System.Windows.Controls.Page;

namespace SoZvon.UI.Room_Pages
{
    public partial class SettingsPage : Page, Settings_Pages.ISettingsPage
    {
        IMainWindow mainWindow = null!;
        My_SettingsUIManager.ISettingsUIManager settingsUIManager = null!;

        public void StartProperties(IMainWindow mainWindow_)
        {
            mainWindow = mainWindow_;
            InitializeComponent();

            settingsUIManager = new My_SettingsUIManager.SettingsUIManager(this);
        }

        public void UpdateSetting<T>(string id, T value) => mainWindow.UpdateSetting(id, value);
        public void ChangeHasInvalidKey(bool value) => mainWindow.ChangeHasInvalidKeySetting(value);

        public void OnMicrophonesInfo(Dictionary<string, string> values) => settingsUIManager.UpdateMicrophoneOptions(values);
        
        public void SelectMicrophoneByName(string name) => mainWindow.SelectMicrophoneByName(name);
        public void ReloadConnectionServ() => mainWindow.ReloadConnectionServ();
        public void CloseApplication() => mainWindow.CloseApplication();

        public void UpdateUI(string id) => settingsUIManager.UpdateUIs();
        public void UpdateUIs() => settingsUIManager.UpdateUIs();
        public void MakeSettingsUI(List<SettingsLogicManager.SettingsLogic.ISetting> settingUIs)
        {
            var listUIs = settingsUIManager.MakeUIFromISetting(settingUIs);
            settingsUIManager.InitializeUI(SettingsPanel, listUIs);
        }
        public void OnHotkeyPressed(string Id, bool UseFormCapture)
        {
            if (UseFormCapture && !mainWindow.IsWindowFocused())
                return;

            settingsUIManager.OnHotkeyPressed(Id);
        }

        void SaveButton_Click(object sender, RoutedEventArgs e) => mainWindow.TrySaveSettings();
        void ResetButton_Click(object sender, RoutedEventArgs e) => mainWindow.TryResetToLastSettings();
        void ResetDefaultButton_Click(object sender, RoutedEventArgs e) => mainWindow.TryResetToDefaultSettings();
    }
}
