using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Frozen;
using System.ComponentModel.DataAnnotations;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Keys = System.Windows.Forms.Keys;

namespace SoZvon.UI.Room_Pages
{
    public class AppSetting
    {
        [Key]
        [MaxLength(100)]
        public string Id { get; set; } = string.Empty;

        [Required]
        public string Value { get; set; } = string.Empty;

        public DateTime LastModified { get; set; } = DateTime.UtcNow;
    }
    public class AppDbContext : DbContext
    {
        public DbSet<AppSetting> Settings { get; set; }

        public AppDbContext() { }
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                optionsBuilder.UseSqlite("Data Source=settings.db");
            }
        }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<AppSetting>(entity => {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasMaxLength(100);
                entity.Property(e => e.Value).IsRequired();
                entity.Property(e => e.LastModified).HasDefaultValueSql("CURRENT_TIMESTAMP");
            });
        }
    }

    public class GlobalHotKeyManager
    {
        [DllImport("user32.dll")] static extern short GetAsyncKeyState(Keys vkey);

        readonly Dictionary<string, IHotkeySettings> currentKeys = [];
        readonly CancellationToken cts = new();
        readonly object currentKeys_lock = new();

        bool NeedCheck = false;

        public void AddorUpdateHotkey(IHotkeySettings hotkeySettings)
        {
            lock (currentKeys_lock)
                currentKeys[hotkeySettings.Id] = hotkeySettings;
        }
        public async Task ReadKeysAsync()
        {
            try
            {
                while (!cts.IsCancellationRequested)
                {
                    lock (currentKeys_lock)
                    {
                        if (NeedCheck)
                        {
                            foreach (IHotkeySettings value in currentKeys.Values)
                            {
                                if (!IsKeyCombinationPressed(value.OldKey, value.OldModifiers))
                                    continue;

                                value.OnHotkeyPressed();
                            }
                        }
                    }

                    await Task.Delay(100, cts);
                }
            }
            catch { }
        }

        static bool IsKeyPressed(Keys key)
        {
            short state = GetAsyncKeyState(key);
            return (state & 0x8000) != 0;
        }
        static bool IsKeyCombinationPressed(Key key, ModifierKeys modifiers)
        {
            // Проверяем что нажаты именно те модификаторы, которые требуются
            if (modifiers.HasFlag(ModifierKeys.Control) != IsKeyPressed(Keys.ControlKey)) 
                return false;
            if (modifiers.HasFlag(ModifierKeys.Shift) != IsKeyPressed(Keys.ShiftKey)) 
                return false;
            if (modifiers.HasFlag(ModifierKeys.Alt) != IsKeyPressed(Keys.Menu)) 
                return false;
            if (modifiers.HasFlag(ModifierKeys.Windows) != (IsKeyPressed(Keys.LWin) || IsKeyPressed(Keys.RWin))) 
                return false;

            // Проверяем основную клавишу
            return IsKeyPressed((Keys)KeyInterop.VirtualKeyFromKey(key));
        }

        public void StartChecking()
        {
            lock (currentKeys_lock)
                NeedCheck = true;
        }
        public void StopChecking()
        {
            lock (currentKeys_lock)
                NeedCheck = false;
        }
    }

    public interface ISetting
    {
        string Id { get; }
        string Description { get; }
        string DefaultValue { get; }
        bool HasChanges();
        bool HasChangesDefaultSettings();

        void ResetToOriginal();
        void ResetToDefault();
        void SaveCurrentState();
        void UpdateUI();
        void CreateUI(StackPanel panel);
    }
    public class ComboBoxSetting(string id, string description, string defaultValue, Dictionary<string, string> options, ISettingsManager settingsManager) : ISetting
    {
        readonly ISettingsManager settingsManager = settingsManager;

        public string Id { get; } = id;
        public string Description { get; } = description;
        public string DefaultValue { get; } = defaultValue;

        public string SelectedValue { get; set; } = defaultValue;
        public string OldSelectedValue { get; set; } = defaultValue;

        public Dictionary<string, string> Options { get; private set; } = options;
        public ComboBox ComboBoxUI { get; set; } = null!;

        public ComboBoxSetting(string id, string description, string defaultValue, Dictionary<string, string> options, ComboBox comboBoxUI, ISettingsManager settingsManager) : this(id, description, defaultValue, options, settingsManager)
        {
            ComboBoxUI = comboBoxUI;
        }

        public bool HasChanges() => SelectedValue != OldSelectedValue;
        public bool HasChangesDefaultSettings() => SelectedValue != DefaultValue;

        public void ResetToOriginal() => SelectedValue = OldSelectedValue;
        public void ResetToDefault() => SelectedValue = DefaultValue;
        public void SaveCurrentState()
        {
            if(!OldSelectedValue.Equals(SelectedValue))
                settingsManager.ComboBox_OnSelectionChanged(this, SelectedValue);

            OldSelectedValue = SelectedValue;
        }

        public void UpdateUI()
        {
            if (ComboBoxUI is null)
                return;

            // Устанавливаем выбранный элемент
            foreach (ComboBoxItem item in ComboBoxUI.Items)
            {
                if (item.Tag?.ToString() == SelectedValue)
                {
                    ComboBoxUI.SelectedItem = item;
                    break;
                }
            }
        }
        public void CreateUI(StackPanel panel)
        {
            var stackPanel = new StackPanel
            {
                Orientation = Orientation.Vertical,
                Margin = new Thickness(0, 10, 0, 10)
            };

            // Текст с описанием
            var textBlock = new TextBlock
            {
                FontFamily = new FontFamily("Comic Sans MS"),
                FontSize = 14,
                Text = Description + ":",
                Margin = new Thickness(0, 0, 0, 5),
                FontWeight = FontWeights.Bold
            };

            // ComboBox с опциями
            var comboBox = new ComboBox
            {
                FontFamily = new FontFamily("Comic Sans MS"),
                FontSize = 14,
                MinWidth = 176,
                MaxWidth = 300,
                HorizontalAlignment = HorizontalAlignment.Left,
                Tag = Id
            };

            // Заполняем опции
            UpdateValuesUI(comboBox, Options);

            // Обработчик изменения выбора
            comboBox.SelectionChanged += ComboBox_SelectionChanged;

            ComboBoxUI = comboBox;

            stackPanel.Children.Add(textBlock);
            stackPanel.Children.Add(comboBox);
            panel.Children.Add(stackPanel);
        }
        public void UpdateValuesUI(ComboBox comboBox, Dictionary<string, string> values)
        {
            if (comboBox is null)
                return;

            comboBox.Items.Clear();

            // Устанавливаем выбранный элемент
            foreach (var option in values)
            {
                var item = new ComboBoxItem
                {
                    Content = option.Value, // Отображаемое имя
                    Tag = option.Key,       // Внутреннее значение
                    FontFamily = new FontFamily("Comic Sans MS"),
                    FontSize = 14
                };

                comboBox.Items.Add(item);

                // Устанавливаем выбранный элемент по умолчанию
                if (option.Key == SelectedValue)
                {
                    comboBox.SelectedItem = item;
                }
            }
        }

        public void ChangeComboboxValues(Dictionary<string, string> values)
        {
            Options = new Dictionary<string, string>(values);

            UpdateValuesUI(ComboBoxUI, values);
        }

        void ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is not ComboBox comboBox || comboBox.SelectedItem is not ComboBoxItem selectedItem)
                return;

            SelectedValue = selectedItem.Tag?.ToString() ?? string.Empty;
        }
    }
    public class CheckboxSetting(string id, string description, bool isChecked, ISettingsManager settingsManager) : ISetting
    {
        readonly ISettingsManager settingsManager = settingsManager;

        public string Id { get; } = id;
        public string Description { get; } = description;

        public bool IsChecked { get; set; } = isChecked;
        public bool OldIsChecked { get; set; } = isChecked;
        public string DefaultValue { get; } = ToStringRepresentation(isChecked);

        public CheckBox CheckBoxUI { get; set; } = null!;

        public CheckboxSetting(string id, string description, bool isChecked, CheckBox checkBoxUI, ISettingsManager settingsManager) : this(id, description, isChecked, settingsManager)
        {
            CheckBoxUI = checkBoxUI;
        }

        public bool HasChanges() => IsChecked != OldIsChecked;
        public bool HasChangesDefaultSettings() => IsChecked != ParseStringRepresentation(DefaultValue);

        public void ResetToOriginal() => IsChecked = OldIsChecked;
        public void SaveCurrentState() => OldIsChecked = IsChecked;
        public void ResetToDefault()
        {
            IsChecked = DefaultValue.Equals("включено", StringComparison.CurrentCultureIgnoreCase) ||
                        DefaultValue.Equals("true", StringComparison.CurrentCultureIgnoreCase) ||
                        DefaultValue == "1";
        }

        public void UpdateUI()
        {
            if (CheckBoxUI is null)
                return;

            CheckBoxUI.IsChecked = IsChecked;
        }
        public void CreateUI(StackPanel panel)
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
                Content = Description,
                IsChecked = IsChecked,
                Tag = Id,
                Margin = new Thickness(0, 0, 5, 0)
            };

            checkBox.Checked += CheckBox_Changed;
            checkBox.Unchecked += CheckBox_Changed;

            CheckBoxUI = checkBox;

            stackPanel.Children.Add(checkBox);
            panel.Children.Add(stackPanel);
        }

        void CheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (sender is not CheckBox checkBox)
                return;

            IsChecked = checkBox.IsChecked is true;
        }

        static bool ParseStringRepresentation(string defaultValue) => defaultValue.Equals("Включено", StringComparison.OrdinalIgnoreCase);
        static string ToStringRepresentation(bool isChecked) => isChecked ? "Включено" : "Выключено";
    }
    public interface IHotkeySettings
    {
        string Id { get; }
        string Description { get; }
        Key OldKey { get; }
        ModifierKeys OldModifiers { get; }

        void OnHotkeyPressed();
    }
    public class HotkeySetting(string id, string description, Key key, ModifierKeys modifiers, ISettingsManager settingsManager, bool useFormCapture = true) : ISetting, IHotkeySettings
    {
        readonly ISettingsManager settingsManager = settingsManager;

        public string Id { get; } = id;
        public string Description { get; } = description;
        public bool IsDuplicate { get; set; } = false;
        public string DefaultValue { get; } = ToStringRepresentation(key, modifiers);
        public bool DefaultUseFormCapture { get; } = useFormCapture;

        public Key Key { get; set; } = key;
        public ModifierKeys Modifiers { get; set; } = modifiers;
        public bool UseFormCapture { get; set; } = useFormCapture;

        public Key OldKey { get; set; } = key;
        public ModifierKeys OldModifiers { get; set; } = modifiers;
        public bool OldUseFormCapture { get; set; } = useFormCapture;

        public Button ButtonUI { get; set; } = null!;
        public CheckBox CaptureModeCheckBox { get; set; } = null!;
        public TextBlock CaptureModeLabel { get; set; } = null!;

        public HotkeySetting(string id, string description, Key key, ModifierKeys modifiers, Button buttonUI, CheckBox captureModeCheckBox, ISettingsManager settingsManager, bool useFormCapture = true) : this(id, description, key, modifiers, settingsManager, useFormCapture)
        {
            ButtonUI = buttonUI;
            CaptureModeCheckBox = captureModeCheckBox;
        }

        public bool HasChanges() => Key != OldKey || Modifiers != OldModifiers || UseFormCapture != OldUseFormCapture;
        public bool HasChangesDefaultSettings()
        {
            var (key, modifiers) = ParseHotkey(DefaultValue);

            return Key != key || Modifiers != modifiers || UseFormCapture != DefaultUseFormCapture;
        }

        public void ResetToOriginal()
        {
            Key = OldKey;
            Modifiers = OldModifiers;
            UseFormCapture = OldUseFormCapture;
            IsDuplicate = false;
            UpdateUI();
        }
        public void ResetToDefault()
        {
            var (defaultKey, defaultModifiers) = ParseHotkey(DefaultValue);
            Key = defaultKey;
            Modifiers = defaultModifiers;
            UseFormCapture = DefaultUseFormCapture;
            IsDuplicate = false;
            UpdateUI();
        }
        public void SaveCurrentState()
        {
            OldKey = Key;
            OldModifiers = Modifiers;
            OldUseFormCapture = UseFormCapture;
        }

        public void OnHotkeyPressed() => settingsManager.OnHotkeyPressed(this, OldUseFormCapture);

        public void UpdateUI()
        {
            if (ButtonUI is not null)
            {
                ButtonUI.Content = ToStringRepresentation(Key, Modifiers);

                if (IsDuplicate)
                {
                    HighlightButton(false);
                }
                else ResetButtonAppearance();
            }

            if (CaptureModeCheckBox is not null && CaptureModeLabel is not null)
            {
                CaptureModeCheckBox.IsChecked = UseFormCapture;
                CaptureModeLabel.Text = UseFormCapture ? "Только в приложении" : "В системе";
            }
        }
        public void CreateUI(StackPanel panel)
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
                Text = Description + ":",
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 15, 0),
            };

            var button = new Button
            {
                FontFamily = new FontFamily("Comic Sans MS"),
                FontSize = 14,
                Tag = Id,
                Padding = new Thickness(20, 0, 20, 0),
                HorizontalAlignment = HorizontalAlignment.Left
            };

            var checkBox = new CheckBox
            {
                VerticalContentAlignment = VerticalAlignment.Center,
                Margin = new Thickness(10, 0, 2, 0),
                Width = 20,
                Height = 20
            };

            var checkBoxLabel = new TextBlock
            {
                Text = DefaultUseFormCapture ? "Только в приложении" : "В системе",
                FontFamily = new FontFamily("Comic Sans MS"),
                Foreground = new SolidColorBrush(Colors.DarkSlateGray),
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center
            };

            checkBox.Checked += CheckBox_Changed;
            checkBox.Unchecked += CheckBox_Changed;

            button.Click += settingsManager.HotkeyButton_Click;
            button.PreviewKeyDown += settingsManager.HotkeyButton_PreviewKeyDown;
            button.LostFocus += settingsManager.HotkeyButton_LostFocus;

            ButtonUI = button;
            CaptureModeCheckBox = checkBox;
            CaptureModeLabel = checkBoxLabel;

            stackPanel.Children.Add(textBlock);
            stackPanel.Children.Add(button);
            stackPanel.Children.Add(checkBox);
            stackPanel.Children.Add(checkBoxLabel);

            panel.Children.Add(stackPanel);
        }
        
        void CheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (sender is not CheckBox checkBox)
                return;

            if (UseFormCapture == (checkBox.IsChecked is true))
                return;

            UseFormCapture = checkBox.IsChecked is true;
            UpdateUI();
        }

        public void HighlightButton(bool isInvalid)
        {
            if (ButtonUI is null)
                return;

            var color = isInvalid ? Color.FromRgb(255, 200, 200) : Color.FromRgb(255, 220, 220);
            ButtonUI.Background = new SolidColorBrush(color);
            ButtonUI.BorderBrush = Brushes.Red;
            ButtonUI.BorderThickness = new Thickness(1);
        }
        public void ResetButtonAppearance()
        {
            if (ButtonUI is null)
                return;

            ButtonUI.Background = Brushes.White;
            ButtonUI.BorderBrush = Brushes.Gray;
            ButtonUI.BorderThickness = new Thickness(1);
        }

        static (Key, ModifierKeys) ParseHotkey(string hotkeyString)
        {
            Key key = Key.None;
            ModifierKeys modifiers = ModifierKeys.None;

            var parts = hotkeyString.Split('+');
            foreach (var part in parts)
            {
                var trimmed = part.Trim();
                if (trimmed.Equals("Ctrl", StringComparison.OrdinalIgnoreCase))
                    modifiers |= ModifierKeys.Control;
                else if (trimmed.Equals("Alt", StringComparison.OrdinalIgnoreCase))
                    modifiers |= ModifierKeys.Alt;
                else if (trimmed.Equals("Shift", StringComparison.OrdinalIgnoreCase))
                    modifiers |= ModifierKeys.Shift;
                else if (Enum.TryParse<Key>(trimmed, true, out var parsedKey))
                    key = parsedKey;
            }

            return (key, modifiers);
        }
        public static string ToStringRepresentation(Key key, ModifierKeys modifier)
        {
            string text = "";

            if (modifier.HasFlag(ModifierKeys.Control))
                text += "Ctrl + ";
            if (modifier.HasFlag(ModifierKeys.Alt))
                text += "Alt + ";
            if (modifier.HasFlag(ModifierKeys.Shift))
                text += "Shift + ";

            return text + key.ToString();
        }
    }

    public interface ISettingsManager
    {
        void ComboBox_OnSelectionChanged(ComboBoxSetting setting, string selectedValue);

        void HotkeyButton_Click(object sender, RoutedEventArgs e);
        void HotkeyButton_PreviewKeyDown(object sender, KeyEventArgs e);
        void HotkeyButton_LostFocus(object sender, RoutedEventArgs e);

        void OnHotkeyPressed(IHotkeySettings setting, bool oldUseFormCapture);
    }

    public class SettingsRepository
    {
        private readonly AppDbContext _context;

        public SettingsRepository()
        {
            _context = new AppDbContext();
            InitializeDatabase();
        }

        private void InitializeDatabase()
        {
            try
            {
                SQLitePCL.raw.SetProvider(new SQLitePCL.());

                _context.Database.EnsureCreated();

                // Создаем базовые настройки если их нет
                if (!_context.Settings.Any())
                {
                    CreateDefaultSettings();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error initializing database: {ex.Message}");
                throw;
            }
        }

        private void CreateDefaultSettings()
        {
            var defaultSettings = new List<AppSetting>
            {
                new AppSetting { Id = "Theme", Value = "light" },
                new AppSetting { Id = "Microphones", Value = "auto" },
                new AppSetting { Id = "NotifyApp", Value = "false" },
                new AppSetting { Id = "ServerAutoConnect", Value = "false" },
                new AppSetting { Id = "MicToggle_Hotkey", Value = "Ctrl+M" },
                new AppSetting { Id = "ExitApp_Hotkey", Value = "Ctrl+Alt+Q" },
                new AppSetting { Id = "AutoConnect_Hotkey", Value = "Ctrl+Shift+Alt+A" },
                new AppSetting { Id = "MicToggle_Capture", Value = "false" },
                new AppSetting { Id = "ExitApp_Capture", Value = "true" },
                new AppSetting { Id = "AutoConnect_Capture", Value = "true" }
            };

            _context.Settings.AddRange(defaultSettings);
            _context.SaveChanges();
        }

        public void SaveSetting(string id, string value)
        {
            try
            {
                var existingSetting = _context.Settings.FirstOrDefault(s => s.Id == id);

                if (existingSetting != null)
                {
                    existingSetting.Value = value;
                    existingSetting.LastModified = DateTime.UtcNow;
                }
                else
                {
                    _context.Settings.Add(new AppSetting { Id = id, Value = value });
                }

                _context.SaveChanges();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving setting {id}: {ex.Message}");
            }
        }

        public string? GetSetting(string id)
        {
            try
            {
                return _context.Settings
                    .Where(s => s.Id == id)
                    .Select(s => s.Value)
                    .FirstOrDefault();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting setting {id}: {ex.Message}");
                return null;
            }
        }

        public string GetSetting(string id, string defaultValue)
        {
            try
            {
                return _context.Settings
                    .Where(s => s.Id == id)
                    .Select(s => s.Value)
                    .FirstOrDefault() ?? defaultValue;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting setting {id}: {ex.Message}");
                return defaultValue;
            }
        }

        public Dictionary<string, string> GetAllSettings()
        {
            var settings = new Dictionary<string, string>();

            try
            {
                var allSettings = _context.Settings.ToList();
                foreach (var setting in allSettings)
                {
                    settings[setting.Id] = setting.Value;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting all settings: {ex.Message}");
            }

            return settings;
        }

        public void DeleteSetting(string id)
        {
            try
            {
                var setting = _context.Settings.FirstOrDefault(s => s.Id == id);
                if (setting != null)
                {
                    _context.Settings.Remove(setting);
                    _context.SaveChanges();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error deleting setting {id}: {ex.Message}");
            }
        }

        public void ClearAllSettings()
        {
            try
            {
                _context.Settings.RemoveRange(_context.Settings);
                _context.SaveChanges();
                CreateDefaultSettings(); // Восстанавливаем настройки по умолчанию
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error clearing settings: {ex.Message}");
            }
        }

        public void Dispose()
        {
            _context?.Dispose();
        }
    }
    public class SettingsManager(ISettingsPage settingsPage) : ISettingsManager
    {
        readonly ISettingsPage settingsPage = settingsPage;
        readonly GlobalHotKeyManager globalHotKeyManager = new();
        readonly SettingsRepository settingsRepository = new();

        bool isLoading = true;
        bool hasInvalidKey = false;
        string? currentHotkeyButtonId;

        readonly ReaderWriterLockSlim settingsLock = new();

        readonly FrozenSet<Key> skipKeys = new HashSet<Key> { Key.LeftCtrl, Key.RightCtrl, Key.LeftAlt, Key.RightAlt, Key.LeftShift, Key.RightShift }.ToFrozenSet();
        readonly FrozenSet<Key> bannedKeys = new HashSet<Key> { Key.Escape, Key.Apps, Key.System, Key.LWin, Key.RWin }.ToFrozenSet();

        readonly Dictionary<string, ISetting> currentSettings = [];
        readonly Dictionary<string, ISetting> lastSettings = [];

        public void LoadDefaultSettingsButtons(StackPanel panel)
        {
            settingsLock.EnterWriteLock();
            try
            {
                isLoading = true;

                // Загружаем сохраненные настройки
                var savedSettings = settingsRepository.GetAllSettings();

                AddComboBox("Theme", GetSavedValue(savedSettings, "Theme", "light"), "Тема оформления", new() {
                    ["light"] = "Светлая"
                });

                AddComboBox("Microphones", GetSavedValue(savedSettings, "Microphones", "auto"), "Микрофон", new() {
                    ["auto"] = "По умолчанию"
                });

                AddCheckBox("NotifyApp", bool.Parse(GetSavedValue(savedSettings, "NotifyApp", "false")), "Включить уведомления");
                AddCheckBox("ServerAutoConnect", bool.Parse(GetSavedValue(savedSettings, "ServerAutoConnect", "false")), "Автоподключение к серверу при входе в приложение");


                // Загрузка горячих клавиш
                var micToggleHotkey = ParseHotkeyString(GetSavedValue(savedSettings, "MicToggle_Hotkey", "Ctrl+M"));
                var exitAppHotkey = ParseHotkeyString(GetSavedValue(savedSettings, "ExitApp_Hotkey", "Ctrl+Alt+Q"));
                var autoConnectHotkey = ParseHotkeyString(GetSavedValue(savedSettings, "AutoConnect_Hotkey", "Ctrl+Shift+Alt+A"));

                var micToggleCapture = bool.Parse(GetSavedValue(savedSettings, "MicToggle_Capture", "false"));
                var exitAppCapture = bool.Parse(GetSavedValue(savedSettings, "ExitApp_Capture", "true"));
                var autoConnectCapture = bool.Parse(GetSavedValue(savedSettings, "AutoConnect_Capture", "true"));

                AddHotkey("MicToggle", micToggleHotkey.Key, micToggleHotkey.Modifiers, "Вкл/Выкл микрофон", micToggleCapture);
                AddHotkey("ExitApp", exitAppHotkey.Key, exitAppHotkey.Modifiers, "Выход из приложения", exitAppCapture);
                AddHotkey("AutoConnect", autoConnectHotkey.Key, autoConnectHotkey.Modifiers, "Переподключиться к серверу", autoConnectCapture);

                CreateUIs(panel);
                SaveCurrentSettings();

                isLoading = false;

                UpdateUIs();

                _ = globalHotKeyManager.ReadKeysAsync();
                globalHotKeyManager.StartChecking();
            }
            finally
            {
                settingsLock.ExitWriteLock();
            }
        }
        static string GetSavedValue(Dictionary<string, string> savedSettings, string key, string defaultValue)
        {
            return savedSettings.TryGetValue(key, out var value) ? value : defaultValue;
        }
        static (Key Key, ModifierKeys Modifiers) ParseHotkeyString(string hotkeyString)
        {
            Key key = Key.None;
            ModifierKeys modifiers = ModifierKeys.None;

            var parts = hotkeyString.Split('+');
            foreach (var part in parts)
            {
                var trimmed = part.Trim();
                if (trimmed.Equals("Ctrl", StringComparison.OrdinalIgnoreCase))
                    modifiers |= ModifierKeys.Control;
                else if (trimmed.Equals("Alt", StringComparison.OrdinalIgnoreCase))
                    modifiers |= ModifierKeys.Alt;
                else if (trimmed.Equals("Shift", StringComparison.OrdinalIgnoreCase))
                    modifiers |= ModifierKeys.Shift;
                else if (Enum.TryParse<Key>(trimmed, true, out var parsedKey))
                    key = parsedKey;
            }

            return (key, modifiers);
        }
        void SaveSettingToDatabase(ISetting setting)
        {
            if (isLoading) return; // Не сохраняем при загрузке

            switch (setting)
            {
                case ComboBoxSetting comboBox:
                    settingsRepository.SaveSetting(comboBox.Id, comboBox.SelectedValue);
                    break;

                case CheckboxSetting checkbox:
                    settingsRepository.SaveSetting(checkbox.Id, checkbox.IsChecked.ToString());
                    break;

                case HotkeySetting hotkey:

                    settingsRepository.SaveSetting($"{hotkey.Id}_Hotkey", HotkeySetting.ToStringRepresentation(hotkey.Key, hotkey.Modifiers));
                    settingsRepository.SaveSetting($"{hotkey.Id}_Capture", hotkey.UseFormCapture.ToString());
                    break;
            }
        }

        public bool ChangeComboboxValues(string id, Dictionary<string, string> values)
        {
            settingsLock.EnterWriteLock();
            try
            {
                if (!TryGetSettingLast<ComboBoxSetting>(id, out var comboBox))
                    return false;

                comboBox.ChangeComboboxValues(values);
                return true;
            }
            finally
            {
                settingsLock.ExitWriteLock();
            }
        }

        public bool TrySaveChanges(out string error_text)
        {
            settingsLock.EnterWriteLock();
            try
            {
                if (hasInvalidKey)
                {
                    error_text = "Невозможно сохранить: назначена недопустимая клавиша";
                    return false;
                }

                if (CheckDuplicates())
                {
                    error_text = "Невозможно сохранить: обнаружены повторяющиеся комбинации клавиш";
                    return false;
                }

                if (!HasChanges())
                {
                    error_text = "Настройки не были изменены.";
                    return false;
                }

                foreach (var setting in currentSettings.Values)
                {
                    setting.SaveCurrentState();

                    // Сохраняем в базу данных
                    SaveSettingToDatabase(setting);
                }

                SaveCurrentSettings();
                error_text = default!;

                return true;
            }
            finally
            {
                settingsLock.ExitWriteLock();
            }
        }
        public bool TryResetSettingsToLast(out string error_text)
        {
            settingsLock.EnterWriteLock();
            try
            {
                if (!HasChanges())
                {
                    error_text = "Настройки уже соответствуют сохраненным значениям.";
                    return false;
                }

                foreach (var setting in currentSettings.Values)
                {
                    setting.ResetToOriginal();
                    setting.UpdateUI();
                }

                hasInvalidKey = false;
                error_text = default!;

                return true;
            }
            finally
            {
                settingsLock.ExitWriteLock();
            }
        }
        public bool TryResetSettingsToDefault(out string error_text)
        {
            settingsLock.EnterWriteLock();
            try
            {
                if (!HasChangesDefaultSettings())
                {
                    error_text = "Настройки уже соответствуют сохраненным значениям.";
                    return false;
                }

                foreach (var setting in currentSettings.Values)
                {
                    setting.ResetToDefault();
                    setting.UpdateUI();
                }

                hasInvalidKey = false;
                error_text = default!;

                return true;
            }
            finally
            {
                settingsLock.ExitWriteLock();
            }
        }

        public void HotkeyButton_Click(object sender, RoutedEventArgs e)
        {
            settingsLock.EnterWriteLock();
            try
            {
                if (sender is not Button button || button.Tag is not string id)
                    return;

                currentHotkeyButtonId = id;

                if (!TryGetSetting<HotkeySetting>(id, out var hotkey))
                    return;

                button.Content = "Нажмите клавишу...";
                hotkey.ResetButtonAppearance();
                button.Focus();
            }
            finally
            {
                settingsLock.ExitWriteLock();
            }
        }
        public void HotkeyButton_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            settingsLock.EnterWriteLock();
            try
            {
                Key newKey = e.Key;
                ModifierKeys newModifiers = Keyboard.Modifiers;

                if (currentHotkeyButtonId is not string id)
                    return;

                if (skipKeys.Contains(newKey))
                    return;

                if (!TryGetSetting<HotkeySetting>(id, out var hotkeySetting))
                    return;

                if (bannedKeys.Contains(newKey))
                {
                    hotkeySetting.ButtonUI.Content = "Недопустимая клавиша";
                    hotkeySetting.HighlightButton(true);

                    hasInvalidKey = true;
                    return;
                }

                // Проверяем на дублирование
                bool isDuplicate = CheckForHotkeyDuplicates(id, newKey, newModifiers);

                hotkeySetting.Key = newKey;
                hotkeySetting.Modifiers = newModifiers;
                hotkeySetting.IsDuplicate = isDuplicate;

                // Обновляем интерфейс
                UpdateUIs();
                currentHotkeyButtonId = null;
                Keyboard.ClearFocus();

                hasInvalidKey = false;

                e.Handled = true;
            }
            finally
            {
                settingsLock.ExitWriteLock();
            }
        }
        public void HotkeyButton_LostFocus(object sender, RoutedEventArgs e)
        {
            settingsLock.EnterWriteLock();
            try
            {
                if (sender is not Button button || button.Tag is not string id)
                    return;

                // Если это текущая активная кнопка, сбрасываем её состояние
                if (id == currentHotkeyButtonId)
                {
                    currentHotkeyButtonId = null;

                    if (!TryGetSetting<HotkeySetting>(id, out var hotkey))
                        return;

                    hotkey.UpdateUI();
                }
            }
            finally
            {
                settingsLock.ExitWriteLock();
            }
        }

        public void OnHotkeyPressed(IHotkeySettings setting, bool oldUseFormCapture)
        {
            if (oldUseFormCapture && !settingsPage.IsWindowFocused())
                return;

            switch (setting.Id)
            {
                case "ExitApp":
                    {
                        settingsPage.CloseApplication();
                        break;
                    }
                case "AutoConnect":
                    {
                        settingsPage.ReloadConnectionServ();
                        break;
                    }
                default:
                    break;
            }
        }
        public void ComboBox_OnSelectionChanged(ComboBoxSetting setting, string selectedValue)
        {
            switch (setting.Id)
            {
                case "Microphones":
                    {
                        settingsPage.SelectMicrophoneByName(selectedValue);
                        break;
                    }
                default:
                    break;
            }
        }

        void AddComboBox(string id, string defaultValue, string description, Dictionary<string, string> options) => currentSettings[id] = new ComboBoxSetting(id, description, defaultValue, options, this);
        void AddCheckBox(string id, bool isChecked, string description) => currentSettings[id] = new CheckboxSetting(id, description, isChecked, this);
        void AddHotkey(string id, Key key, ModifierKeys modifierKeys, string description, bool useFormCapture = true) => currentSettings[id] = new HotkeySetting(id, description, key, modifierKeys, this, useFormCapture);
        void SaveCurrentSettings()
        {
            lastSettings.Clear();

            foreach (var pair in currentSettings)
            {
                var setting = CloneSetting(pair.Value);

                lastSettings[pair.Key] = setting;

                if (setting is IHotkeySettings hotkey)
                    globalHotKeyManager.AddorUpdateHotkey(hotkey);
            }
        }

        void CreateUIs(StackPanel panel)
        {
            panel.Children.Clear();

            foreach (var setting in currentSettings.Values)
            {
                setting.CreateUI(panel);
            }
        }
        void UpdateUIs()
        {
            foreach (var setting in currentSettings.Values)
            {
                setting.UpdateUI();
            }
        }

        bool CheckForHotkeyDuplicates(string currentHotkeyId, Key newKey, ModifierKeys newModifiers)
        {
            ResetHotkeyDuplicates();

            bool isDuplicate = false;

            foreach (var setting in currentSettings.Values.OfType<HotkeySetting>())
            {
                if (setting.Id != currentHotkeyId &&
                    setting.Key == newKey &&
                    setting.Modifiers == newModifiers)
                {
                    isDuplicate = true;
                    setting.IsDuplicate = true;
                }
            }

            return isDuplicate;
        }
        bool CheckDuplicates()
        {
            foreach (var setting in currentSettings.Values)
            {
                switch (setting)
                {
                    case HotkeySetting hotkey:
                        {
                            if (hotkey.IsDuplicate)
                                return true;
                            break;
                        }
                }
            }

            return false;
        }
        void ResetHotkeyDuplicates()
        {
            foreach (var setting in currentSettings.Values)
            {
                if (setting is HotkeySetting hotkey)
                    hotkey.IsDuplicate = false;
            }
        }

        bool HasChanges() => currentSettings.Values.Any(setting => setting.HasChanges());
        bool HasChangesDefaultSettings() => currentSettings.Values.Any(setting => setting.HasChangesDefaultSettings());

        ISetting CloneSetting(ISetting setting)
        {
            return setting switch
            {
                HotkeySetting hotkey => new HotkeySetting(hotkey.Id, hotkey.Description, hotkey.Key, hotkey.Modifiers, hotkey.ButtonUI, hotkey.CaptureModeCheckBox, this, hotkey.UseFormCapture),
                CheckboxSetting checkbox => new CheckboxSetting(checkbox.Id, checkbox.Description, checkbox.IsChecked, checkbox.CheckBoxUI, this),
                ComboBoxSetting comboBox => new ComboBoxSetting(comboBox.Id, comboBox.Description, comboBox.DefaultValue, comboBox.Options, comboBox.ComboBoxUI, this),
                _ => throw new NotSupportedException("Unsupported setting type")
            };
        }
        bool TryGetSetting<T>(string key, out T value)
        {
            value = default!;
            if (currentSettings.TryGetValue(key, out var obj) && obj is T typedValue)
            {
                value = typedValue;
                return true;
            }
            return false;
        }
        bool TryGetSettingLast<T>(string key, out T value)
        {
            value = default!;
            if (lastSettings.TryGetValue(key, out var obj) && obj is T typedValue)
            {
                value = typedValue;
                return true;
            }
            return false;
        }
    }

    public interface ISettingsPage
    {
        void SelectMicrophoneByName(string name);
        void ReloadConnectionServ();
        void CloseApplication();
        bool IsWindowFocused();
    }
    public partial class SettingsPage : Page, ISettingsPage
    {
        SettingsManager settingsManager;
        IMainWindow mainWindow;

        public void StartProperties(IMainWindow mainWindow_)
        {
            mainWindow = mainWindow_;
            InitializeComponent();

            settingsManager = new(this);
            settingsManager.LoadDefaultSettingsButtons(SettingsPanel);
        }

        public void OnMicrophonesInfo(Dictionary<string, string> values)
        {
            if (!settingsManager.ChangeComboboxValues("Microphones", values))
                mainWindow.Make_ErrorMessage("Settings", "ChangeComboboxValues is false");
        }
        public void SelectMicrophoneByName(string name) => mainWindow.SelectMicrophoneByName(name);
        public void ReloadConnectionServ() => mainWindow.ReloadConnectionServ();
        public void CloseApplication() => mainWindow.CloseApplication();

        public bool IsWindowFocused() => mainWindow.IsWindowFocused();

        void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (!settingsManager.TrySaveChanges(out var error_text))
            {
                mainWindow.Make_ErrorMessage("Settings", error_text ?? "unknown_error");
            }
            else mainWindow.Make_NotifyMessage("Settings", "Настройки сохранены успешно!", 1000);
        }
        void ResetButton_Click(object sender, RoutedEventArgs e)
        {
            if (!settingsManager.TryResetSettingsToLast(out var error_text))
            {
                mainWindow.Make_ErrorMessage("Settings", error_text ?? "unknown_error");
            }
            else mainWindow.Make_NotifyMessage("Settings", "Настройки сброшены к сохраненным значениям!", 1000);
        }
        void ResetDefaultButton_Click(object sender, RoutedEventArgs e)
        {
            if (!settingsManager.TryResetSettingsToDefault(out var error_text))
            {
                mainWindow.Make_ErrorMessage("Settings", error_text ?? "unknown_error");
            }
            else mainWindow.Make_NotifyMessage("Settings", "Настройки сброшены к значениям по умолчанию!", 1000);
        }
    }
}
