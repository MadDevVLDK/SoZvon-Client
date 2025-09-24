using SoZvon.SettingsLogicManager.SettingsLogic;
using SoZvon.UI.Room_Pages;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace SoZvon.UI.My_SettingsUIManager.SettingsUI
{
    public class CheckboxSettingUI(CheckboxSetting setting, ISettingsUIManager uiManager) : ISettingUI
    {
        readonly CheckboxSetting setting = setting;
        readonly ISettingsUIManager uiManager = uiManager;
        public CheckBox CheckBoxUI { get; private set; } = null!;

        public string GetID() => setting.Id;

        public void UpdateUI()
        {
            if (CheckBoxUI is null)
                return;

            CheckBoxUI.IsChecked = setting.IsChecked;
        }
        public StackPanel CreateUI()
        {
            var stackPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 5, 0, 5)
            };

            var checkBox = new CheckBox
            {
                FontFamily = new FontFamily("Comic Sans MS"),
                FontSize = 14,
                Content = setting.Description,
                IsChecked = setting.IsChecked,
                Tag = setting.Id,
                Margin = new Thickness(0, 0, 5, 0)
            };

            checkBox.Checked += CheckBox_Changed;
            checkBox.Unchecked += CheckBox_Changed;

            CheckBoxUI = checkBox;
            stackPanel.Children.Add(checkBox);

            return stackPanel;
        }

        void CheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (sender is not CheckBox checkBox)
                return;

            uiManager.CheckBox_Changed(setting.Id, checkBox.IsChecked == true);
        }
    }
}
