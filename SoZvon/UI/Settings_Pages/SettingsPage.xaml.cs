using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace SoZvon.UI.Room_Pages
{
    public interface ISetting
    {
        string Id { get; }
        string Description { get; }

        bool HasChanges();
        bool HasChangesDefaultSettings();

        void ResetToOriginal();
        void ResetToDefault();
        void SaveCurrentState();
    }
    public abstract class SettingBase(string id, string description) : ISetting
    {
        public string Id { get; } = id;
        public string Description { get; } = description;

        public abstract bool HasChanges();
        public abstract bool HasChangesDefaultSettings();

        public abstract void ResetToOriginal();
        public abstract void ResetToDefault();
        public abstract void SaveCurrentState();
    }
    public class ComboBoxSetting : SettingBase
    {
        public string SelectedValue { get; set; }
        public string OldSelectedValue { get; set; }
        public string DefaultSelectedValue { get; set; }

        public Dictionary<string, string> Options { get; private set; }

        public ComboBoxSetting(ComboBoxSetting comboboxSetting) : base(comboboxSetting.Id, comboboxSetting.Description)
        {
            SelectedValue = comboboxSetting.SelectedValue;
            OldSelectedValue = comboboxSetting.OldSelectedValue;
            DefaultSelectedValue = comboboxSetting.DefaultSelectedValue;
            Options = comboboxSetting.Options;
        }
        public ComboBoxSetting(string id, string description, string currentValue, string defaultValue, Dictionary<string, string> options) : base(id, description)
        {
            SelectedValue = currentValue;
            OldSelectedValue = currentValue;
            DefaultSelectedValue = defaultValue;
            Options = options;
        }

        public override bool HasChanges() => SelectedValue != OldSelectedValue;
        public override bool HasChangesDefaultSettings() => SelectedValue != DefaultSelectedValue;

        public override void ResetToOriginal() => SelectedValue = OldSelectedValue;
        public override void ResetToDefault() => SelectedValue = DefaultSelectedValue;
        public override void SaveCurrentState()
        {
            if (OldSelectedValue.Equals(SelectedValue))
                return;

            OldSelectedValue = SelectedValue;
        }

        public void ChangeComboboxValues(Dictionary<string, string> values)
        {
            Options = new Dictionary<string, string>(values);
        }
    }
    public class CheckboxSetting : SettingBase
    {
        public bool IsChecked { get; set; }
        public bool OldIsChecked { get; set; }
        public bool DefaultIsChecked { get; set; }

        public CheckboxSetting(CheckboxSetting checkboxSetting) : base(checkboxSetting.Id, checkboxSetting.Description)
        {
            IsChecked = checkboxSetting.IsChecked;
            OldIsChecked = checkboxSetting.OldIsChecked;
            DefaultIsChecked = checkboxSetting.DefaultIsChecked;
        }
        public CheckboxSetting(string id, string description, bool currentValue, bool defaultValue) : base(id, description)
        {
            IsChecked = currentValue;
            OldIsChecked = currentValue;
            DefaultIsChecked = defaultValue;
        }

        public override bool HasChanges() => IsChecked != OldIsChecked;
        public override bool HasChangesDefaultSettings() => IsChecked != DefaultIsChecked;

        public override void ResetToOriginal() => IsChecked = OldIsChecked;
        public override void ResetToDefault() => IsChecked = DefaultIsChecked;
        public override void SaveCurrentState() => OldIsChecked = IsChecked;
    }
    public interface IHotkeySettings
    {
        string Id { get; }
        bool SupportsAutoRepeat { get; }
        int InitialRepeatDelay { get; } // ms
        int RepeatInterval { get; } // ms
        Key OldKey { get; }
        ModifierKeys OldModifiers { get; }
        void OnHotkeyPressed();
    }
    
    public class HotkeySetting: SettingBase, IHotkeySettings
    {
        readonly ISettingsService settingsService;

        public bool IsDuplicate { get; set; } = false;
        public bool SupportsAutoRepeat { get; }
        public int InitialRepeatDelay { get; } // ms
        public int RepeatInterval { get; } // ms


        public Key Key { get; set; }
        public ModifierKeys Modifiers { get; set; }
        public bool UseFormCapture { get; set; }

        public Key OldKey { get; set; }
        public ModifierKeys OldModifiers { get; set; }
        public bool OldUseFormCapture { get; set; }

        public Key DefaultKey { get; set; }
        public ModifierKeys DefaultModifiers { get; set; }
        public bool DefaultUseFormCapture { get; }


        public HotkeySetting(HotkeySetting hotkeySetting) : base(hotkeySetting.Id, hotkeySetting.Description)
        {
            IsDuplicate = hotkeySetting.IsDuplicate;

            SupportsAutoRepeat = hotkeySetting.SupportsAutoRepeat;
            InitialRepeatDelay = hotkeySetting.InitialRepeatDelay;
            RepeatInterval = hotkeySetting.RepeatInterval;

            Key = hotkeySetting.Key;
            Modifiers = hotkeySetting.Modifiers;
            UseFormCapture = hotkeySetting.UseFormCapture;

            OldKey = hotkeySetting.OldKey;
            OldModifiers = hotkeySetting.OldModifiers;
            OldUseFormCapture = hotkeySetting.OldUseFormCapture;

            DefaultKey = hotkeySetting.DefaultKey;
            DefaultModifiers = hotkeySetting.DefaultModifiers;
            DefaultUseFormCapture = hotkeySetting.DefaultUseFormCapture;

            settingsService = hotkeySetting.settingsService;
        }
        public HotkeySetting(string id, string description, Tuple<Key, ModifierKeys, bool> _current, Tuple<Key, ModifierKeys, bool> _default, bool supportsAutoRepeat, int initialRepeatDelay, int repeatInterval, ISettingsService _settingsService) : base(id, description)
        {
            SupportsAutoRepeat = supportsAutoRepeat;
            InitialRepeatDelay = initialRepeatDelay;
            RepeatInterval = repeatInterval;

            Key = _current.Item1;
            Modifiers = _current.Item2;
            UseFormCapture = _current.Item3;

            OldKey = _current.Item1;
            OldModifiers = _current.Item2;
            OldUseFormCapture = _current.Item3;

            DefaultKey = _default.Item1;
            DefaultModifiers = _default.Item2;
            DefaultUseFormCapture = _default.Item3;

            settingsService = _settingsService;
        }


        public override bool HasChanges() => Key != OldKey || Modifiers != OldModifiers || UseFormCapture != OldUseFormCapture;
        public override bool HasChangesDefaultSettings() => Key != DefaultKey || Modifiers != DefaultModifiers || UseFormCapture != DefaultUseFormCapture;

        public override void ResetToOriginal()
        {
            Key = OldKey;
            Modifiers = OldModifiers;
            UseFormCapture = OldUseFormCapture;
            IsDuplicate = false;
        }
        public override void ResetToDefault()
        {
            Key = DefaultKey;
            Modifiers = DefaultModifiers;
            UseFormCapture = DefaultUseFormCapture;
            IsDuplicate = false;
        }
        public override void SaveCurrentState()
        {
            OldKey = Key;
            OldModifiers = Modifiers;
            OldUseFormCapture = UseFormCapture;
        }

        public void OnHotkeyPressed()
        {
            // Реализация будет в конкретном классе-обработчике
            settingsService.OnHotkeyPressed(Id, OldUseFormCapture);
        }
    }

    public interface ISettingUI
    {
        string GetID();
        void UpdateUI();
        StackPanel CreateUI();
    }
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

    public interface ISettingsPage
    {
        void MakeSettingsUI(List<ISetting> settingUIs);
        void UpdateUI();

        void SelectMicrophoneByName(string name);
        void ReloadConnectionServ();
        void CloseApplication();

        void OnHotkeyPressed(string Id, bool UseFormCapture);

        void UpdateSetting<T>(string id, T value);
        void ChangeHasInvalidKey(bool value);
        List<ISetting> GetSettings();
    }
    public partial class SettingsPage : Page, ISettingsPage
    {
        IMainWindow mainWindow = null!;
        ISettingsService settingsService = null!;
        ISettingsUIManager settingsUIManager = null!;

        public void StartProperties(IMainWindow mainWindow_)
        {
            mainWindow = mainWindow_;
            InitializeComponent();

            settingsUIManager = new SettingsUIManager(this);
            settingsService = new SettingsService(this);
            
            settingsService.StartSettings();
        }

        public void UpdateSetting<T>(string id, T value) => settingsService.UpdateSetting<T>(id, value);
        public void ChangeHasInvalidKey(bool value) => settingsService.ChangeHasInvalidKey(value);
        public List<ISetting> GetSettings() => settingsService.GetSettings();

        public void OnMicrophonesInfo(Dictionary<string, string> values)
        {
            settingsUIManager.UpdateMicrophoneOptions(values);
        }
        public void SelectMicrophoneByName(string name) => mainWindow.SelectMicrophoneByName(name);
        public void ReloadConnectionServ() => mainWindow.ReloadConnectionServ();
        public void CloseApplication() => mainWindow.CloseApplication();

        public void UpdateUI() => mainWindow.MakeAction_Form(settingsUIManager.UpdateUI);
        public void MakeSettingsUI(List<ISetting> settingUIs)
        {
            mainWindow.MakeAction_Form(() => {
                var listUIs = settingsUIManager.MakeUIFromISetting(settingUIs);
                settingsUIManager.InitializeUI(SettingsPanel, listUIs);
            });
        }

        public void OnHotkeyPressed(string Id, bool UseFormCapture)
        {
            if (UseFormCapture && !mainWindow.IsWindowFocused())
                return;

            mainWindow.MakeAction_Form(() => settingsUIManager.OnHotkeyPressed(Id));
        }

        void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            var (success, error_text) = settingsService.TrySaveSettings();

            if (!success)
            {
                mainWindow.Make_ErrorMessage("Settings", error_text ?? "unknown_error");
            }
            else mainWindow.Make_NotifyMessage("Settings", "Настройки сохранены успешно!", 1000);
        }
        void ResetButton_Click(object sender, RoutedEventArgs e)
        {
            var (success, error_text) = settingsService.TryResetToLast();

            if (!success)
            {
                mainWindow.Make_ErrorMessage("Settings", error_text ?? "unknown_error");
            }
            else mainWindow.Make_NotifyMessage("Settings", "Настройки сброшены к сохраненным значениям!", 1000);
        }
        void ResetDefaultButton_Click(object sender, RoutedEventArgs e)
        {
            var (success, error_text) = settingsService.TryResetToDefault();

            if (!success)
            {
                mainWindow.Make_ErrorMessage("Settings", error_text ?? "unknown_error");
            }
            else mainWindow.Make_NotifyMessage("Settings", "Настройки сброшены к значениям по умолчанию!", 1000);
        }
    }
}
