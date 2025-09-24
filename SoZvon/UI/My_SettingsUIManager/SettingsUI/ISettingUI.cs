namespace SoZvon.UI.My_SettingsUIManager.SettingsUI
{
    public interface ISettingUI
    {
        string GetID();
        void UpdateUI();
        System.Windows.Controls.StackPanel CreateUI();
    }
}
