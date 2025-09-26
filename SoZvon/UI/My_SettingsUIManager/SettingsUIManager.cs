using System.Collections.Frozen;
using System.Windows.Controls;
using System.Windows.Input;

namespace SoZvon.UI.My_SettingsUIManager
{
    using SettingsLogicManager.SettingsLogic;
    using SettingsUI;
    using Settings_Pages;

    public class SettingsUIManager(ISettingsPage settingsPage) : ISettingsUIManager
    {
        readonly ISettingsPage settingsPage = settingsPage;
        readonly Dictionary<string, ISettingUI> settingUIs = [];

        readonly HashSet<Key> skipKeys = [Key.LeftCtrl, Key.RightCtrl, Key.LeftAlt, Key.RightAlt, Key.LeftShift, Key.RightShift, Key.LWin, Key.RWin];
        readonly HashSet<Key> bannedKeys = [Key.Escape, Key.Apps, Key.System];

        string currentHotkeyId = string.Empty;

        public void InitializeUI(StackPanel panel, List<ISettingUI> settings)
        {
            panel.Children.Clear();
            settingUIs.Clear();

            foreach (var setting in settings)
            {
                var uiElement = setting.CreateUI();
                panel.Children.Add(uiElement);

                var id = setting.GetID();
                settingUIs[id] = setting;
            }
        }
        public void UpdateUI(string id)
        {
            settingUIs[id].UpdateUI();
        }
        public void UpdateUIs()
        {
            foreach (var settingUI in settingUIs.Values)
            {
                settingUI.UpdateUI();
            }

            currentHotkeyId = string.Empty;
        }

        public void UpdateMicrophoneOptions(Dictionary<string, string> options)
        {
            if (settingUIs.TryGetValue("Microphones", out var settingUI) && settingUI is ComboBoxSettingUI comboBoxUI)
            {
                comboBoxUI.UpdateValuesUI(options);
            }
        }

        public void ComboBox_OnSelectionChanged(string id, string selectedValue)
        {
            settingsPage.UpdateSetting(id, selectedValue);
        }
        public void CheckBox_Changed(string id, bool value)
        {
            settingsPage.UpdateSetting(id, value);
        }

        public void HotkeyCheckBox_Changed(string id, bool value)
        {
            settingsPage.UpdateSetting(id, value);
        }
        public void HotkeyButton_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            if (sender is not Button button || button.Tag is not string id)
                return;

            currentHotkeyId = id;
            button.Content = "Нажмите клавишу...";
            button.Focus();

            e.Handled = true;
        }
        public void HotkeyButton_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (currentHotkeyId == string.Empty)
                return;

            Key newKey = e.Key;
            ModifierKeys newModifiers = Keyboard.Modifiers;

            if (skipKeys.Contains(newKey))
                return;

            if (bannedKeys.Contains(newKey))
            {
                settingsPage.ChangeHasInvalidKey(true);
                currentHotkeyId = string.Empty;
                Keyboard.ClearFocus();
                return;
            }

            // Обновляем настройку
            settingsPage.UpdateSetting(currentHotkeyId, (newKey, newModifiers, false));
            settingsPage.ChangeHasInvalidKey(false);

            currentHotkeyId = string.Empty;
            Keyboard.ClearFocus();
            e.Handled = true;
        }
        public void HotkeyButton_LostFocus(object sender, System.Windows.RoutedEventArgs e)
        {
            if (sender is not Button button || button.Tag is not string id)
                return;

            if (id == currentHotkeyId)
            {
                currentHotkeyId = string.Empty;

                // Восстанавливаем текст кнопки
                if (settingUIs.TryGetValue(id, out var settingUI))
                {
                    settingUI.UpdateUI();
                }
            }

            e.Handled = true;
        }
        public void OnHotkeyPressed(string id)
        {
            switch (id)
            {
                case "ExitApp":
                    settingsPage.CloseApplication();
                    break;
                case "Reconnect":
                    settingsPage.ReloadConnectionServ();
                    break;
                case "MicToggle":
                    // Пока не реализовано
                    return;
                case "AppSoundUp":
                    // Пока не реализовано
                    return;
                case "AppSoundDown":
                    // Пока не реализовано
                    return;
                default:
                    // Некорректный идентификатор горячей клавиши
                    return;
            }
        }

        public List<ISettingUI> MakeUIFromISetting(List<ISetting> settings)
        {
            var settingsUIList = new List<ISettingUI>();

            foreach (var setting in settings)
            {
                switch (setting)
                {
                    case ComboBoxSetting comboBox:
                        settingsUIList.Add(new ComboBoxSettingUI(comboBox, this));
                        break;
                    case CheckboxSetting checkbox:
                        settingsUIList.Add(new CheckboxSettingUI(checkbox, this));
                        break;
                    case HotkeySetting hotkey:
                        settingsUIList.Add(new HotkeySettingUI(hotkey, this));
                        break;
                    default:
                        throw new ArgumentException("WTF");
                }
            }

            return settingsUIList;
        }
    }
}
