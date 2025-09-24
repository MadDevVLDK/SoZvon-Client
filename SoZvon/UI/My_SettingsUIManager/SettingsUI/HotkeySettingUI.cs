using SoZvon.SettingsLogicManager.SettingsLogic;
using SoZvon.UI.Room_Pages;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace SoZvon.UI.My_SettingsUIManager.SettingsUI
{
    public class HotkeySettingUI(HotkeySetting Setting, ISettingsUIManager uiManager) : ISettingUI
    {
        HotkeySetting Setting { get; } = Setting;
        readonly ISettingsUIManager uiManager = uiManager;

        public Button ButtonUI { get; private set; } = null!;
        public CheckBox CaptureModeCheckBox { get; private set; } = null!;
        public TextBlock CaptureModeLabel { get; private set; } = null!;

        public string GetID() => Setting.Id;

        public void UpdateUI()
        {
            HighlightButton();

            if (CaptureModeCheckBox != null && CaptureModeLabel != null)
            {
                CaptureModeCheckBox.IsChecked = Setting.UseFormCapture;
                CaptureModeLabel.Text = Setting.UseFormCapture ? "Только в приложении" : "В системе";
            }
        }
        public StackPanel CreateUI()
        {
            var stackPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(0, 5, 0, 5)
            };
            var textBlock = new TextBlock
            {
                FontFamily = new FontFamily("Comic Sans MS"),
                FontSize = 14,
                Text = Setting.Description + ":",
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 15, 0),
            };
            var button = new Button
            {
                FontFamily = new FontFamily("Comic Sans MS"),
                FontSize = 14,
                Tag = Setting.Id,
                Background = new SolidColorBrush(Colors.White),
                Padding = new Thickness(20, 0, 20, 0),
                HorizontalAlignment = HorizontalAlignment.Left,
                Content = ToStringRepresentation(Setting.Key, Setting.Modifiers)
            };
            var checkBox = new CheckBox
            {
                VerticalContentAlignment = VerticalAlignment.Center,
                Margin = new Thickness(10, 0, 2, 0),
                Width = 20,
                Height = 20,
                IsChecked = Setting.UseFormCapture
            };
            var checkBoxLabel = new TextBlock
            {
                Text = Setting.UseFormCapture ? "Только в приложении" : "В системе",
                FontFamily = new FontFamily("Comic Sans MS"),
                Foreground = new SolidColorBrush(Colors.DarkSlateGray),
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center
            };

            checkBox.Checked += CheckBox_Changed;
            checkBox.Unchecked += CheckBox_Changed;

            button.Click += uiManager.HotkeyButton_Click;
            button.PreviewKeyDown += uiManager.HotkeyButton_PreviewKeyDown;
            button.LostFocus += uiManager.HotkeyButton_LostFocus;

            ButtonUI = button;
            CaptureModeCheckBox = checkBox;
            CaptureModeLabel = checkBoxLabel;

            stackPanel.Children.Add(textBlock);
            stackPanel.Children.Add(button);
            stackPanel.Children.Add(checkBox);
            stackPanel.Children.Add(checkBoxLabel);

            return stackPanel;
        }

        void CheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (sender is not CheckBox checkBox)
                return;
            if (Setting.UseFormCapture == (checkBox.IsChecked is true))
                return;

            uiManager.HotkeyCheckBox_Changed(Setting.Id, checkBox.IsChecked is true);
        }
        void HighlightButton()
        {
            if (ButtonUI == null)
                return;

            ButtonUI.Content = ToStringRepresentation(Setting.Key, Setting.Modifiers);

            if (Setting.IsDuplicate)
            {
                ButtonUI.Background = Brushes.Red;
                ButtonUI.BorderBrush = Brushes.Red;
                ButtonUI.BorderThickness = new Thickness(1);
            }
            else
            {
                ButtonUI.Background = Brushes.White;
                ButtonUI.BorderBrush = Brushes.Gray;
                ButtonUI.BorderThickness = new Thickness(1);
            }
        }

        static string ToStringRepresentation(Key key, ModifierKeys modifier)
        {
            string text = "";

            if (modifier.HasFlag(ModifierKeys.Control))
                text += "Ctrl + ";
            if (modifier.HasFlag(ModifierKeys.Alt))
                text += "Alt + ";
            if (modifier.HasFlag(ModifierKeys.Shift))
                text += "Shift + ";
            if (modifier.HasFlag(ModifierKeys.Windows))
                text += "Win + ";

            return text + key.ToString();
        }
    }
}
