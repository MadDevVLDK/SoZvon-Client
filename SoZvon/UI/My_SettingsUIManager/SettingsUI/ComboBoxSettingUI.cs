using SoZvon.SettingsLogicManager.SettingsLogic;
using SoZvon.UI.Room_Pages;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace SoZvon.UI.My_SettingsUIManager.SettingsUI
{
    public class ComboBoxSettingUI(ComboBoxSetting setting, ISettingsUIManager uiManager) : ISettingUI
    {
        readonly ComboBoxSetting setting = setting;
        readonly ISettingsUIManager uiManager = uiManager;
        public ComboBox ComboBoxUI { get; private set; } = null!;

        public string GetID() => setting.Id;

        public StackPanel CreateUI()
        {
            var stackPanel = new StackPanel
            {
                Orientation = Orientation.Vertical,
                Margin = new Thickness(0, 10, 0, 10)
            };

            var textBlock = new TextBlock
            {
                FontFamily = new FontFamily("Comic Sans MS"),
                FontSize = 14,
                Text = setting.Description + ":",
                Margin = new Thickness(0, 0, 0, 5),
                FontWeight = FontWeights.Bold
            };

            var comboBox = new ComboBox
            {
                FontFamily = new FontFamily("Comic Sans MS"),
                FontSize = 14,
                MinWidth = 176,
                MaxWidth = 300,
                HorizontalAlignment = HorizontalAlignment.Left,
                Tag = setting.Id
            };
            comboBox.SelectionChanged += ComboBox_SelectionChanged;

            ComboBoxUI = comboBox;

            UpdateValuesUI(setting.Options);

            stackPanel.Children.Add(textBlock);
            stackPanel.Children.Add(comboBox);

            return stackPanel;
        }
        public void UpdateUI()
        {
            if (ComboBoxUI is null)
                return;

            foreach (ComboBoxItem item in ComboBoxUI.Items)
            {
                if (item.Tag?.ToString() == setting.SelectedValue)
                {
                    ComboBoxUI.SelectedItem = item;
                    break;
                }
            }
        }
        public void UpdateValuesUI(Dictionary<string, string> values)
        {
            if (ComboBoxUI is null)
                return;

            ComboBoxUI.Items.Clear();

            foreach (var option in values)
            {
                var item = new ComboBoxItem
                {
                    Content = option.Value,
                    Tag = option.Key,
                    FontFamily = new FontFamily("Comic Sans MS"),
                    FontSize = 14
                };

                ComboBoxUI.Items.Add(item);

                if (option.Key == setting.SelectedValue)
                {
                    ComboBoxUI.SelectedItem = item;
                }
            }
        }

        void ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is not ComboBox comboBox || comboBox.SelectedItem is not ComboBoxItem selectedItem)
                return;

            uiManager.ComboBox_OnSelectionChanged(setting.Id, selectedItem.Tag?.ToString() ?? string.Empty);
        }
    }
}
