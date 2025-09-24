namespace SoZvon.UI.Settings_Pages
{
    public interface ISettingsPage
    {
        void MakeSettingsUI(List<SettingsLogicManager.SettingsLogic.ISetting> settingUIs);
        void UpdateUI(string id);
        void UpdateUIs();

        void SelectMicrophoneByName(string name);
        void ReloadConnectionServ();
        void CloseApplication();

        void OnHotkeyPressed(string Id, bool UseFormCapture);

        void UpdateSetting<T>(string id, T value);
        void ChangeHasInvalidKey(bool value);
    }
}
