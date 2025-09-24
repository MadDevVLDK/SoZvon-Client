namespace SoZvon.UI.My_SettingsUIManager
{
    public interface ISettingsUIManager
    {
        List<SettingsUI.ISettingUI> MakeUIFromISetting(List<SettingsLogicManager.SettingsLogic.ISetting> settings);
        void InitializeUI(System.Windows.Controls.StackPanel panel, List<SettingsUI.ISettingUI> settings);

        void UpdateUI(string id);
        void UpdateUIs();

        void UpdateMicrophoneOptions(Dictionary<string, string> options);
        void ComboBox_OnSelectionChanged(string id, string selectedValue);

        void HotkeyButton_Click(object sender, System.Windows.RoutedEventArgs e);
        void HotkeyButton_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e);
        void HotkeyButton_LostFocus(object sender, System.Windows.RoutedEventArgs e);

        void CheckBox_Changed(string id, bool value);
        void HotkeyCheckBox_Changed(string id, bool value);

        void OnHotkeyPressed(string id);
    }
}
