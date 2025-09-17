using System;
using System.Collections.Frozen;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Xml.Linq;

namespace SoZvon.UI.Room_Pages
{
    public interface ISettingsRepository
    {
        void SaveSetting(string id, string value);
        string GetSetting(string id, string defaultValue = "");
        Dictionary<string, string> GetAllSettings();
        void SaveHotkey(string id, Key key, ModifierKeys modifiers, bool useFormCapture);
        (Key Key, ModifierKeys Modifiers, bool UseFormCapture) GetHotkey(string id);
        void DeleteSetting(string id);
        void ClearAllSettings();
    }
    public interface ISettingsService
    {
        void ChangeHasInvalidKey(bool value);

        List<ISetting> GetSettings();
        void LoadSettings();

        (bool success, string error_text) TrySaveSettings();
        (bool success, string error_text) TryResetToLast();
        (bool success, string error_text) TryResetToDefault();

        void UpdateSetting<T>(string id, T value);
    }
    public interface ISettingsUIManager
    {
        void InitializeUI(StackPanel panel, List<ISetting> settings);
        void UpdateUI();
        void RegisterUIElement(string settingId, FrameworkElement element);

        void ComboBox_OnSelectionChanged(ComboBoxSetting setting, string selectedValue);

        void HotkeyButton_Click(object sender, RoutedEventArgs e);
        void HotkeyButton_PreviewKeyDown(object sender, KeyEventArgs e);
        void HotkeyButton_LostFocus(object sender, RoutedEventArgs e);

        void OnHotkeyPressed(IHotkeySettings setting, bool oldUseFormCapture);
    }

    public class XmlSettingsRepository : ISettingsRepository
    {
        readonly string xmlFilePath = "settings.xml";
        readonly XDocument xmlDocument;
        readonly object lockOperations = new();

        public XmlSettingsRepository() => xmlDocument = LoadOrCreateXml();

        XDocument LoadOrCreateXml()
        {
            lock (lockOperations)
            {
                if (File.Exists(xmlFilePath))
                {
                    try
                    {
                        return XDocument.Load(xmlFilePath);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error loading XML settings: {ex.Message}");
                        return CreateDefaultXml();
                    }
                }
                else
                {
                    return CreateDefaultXml();
                }
            }
        }
        XDocument CreateDefaultXml()
        {
            var doc = new XDocument(
                new XElement("Settings",
                    new XElement("Theme", "light"),
                    new XElement("Microphones", "auto"),
                    new XElement("NotifyApp", "false"),
                    new XElement("ServerAutoConnect", "false"),
                    new XElement("Hotkeys",
                        new XElement("MicToggle",
                            new XElement("Key", "M"),
                            new XElement("Modifiers", "Control"),
                            new XElement("Capture", "false")
                        ),
                        new XElement("ExitApp",
                            new XElement("Key", "Q"),
                            new XElement("Modifiers", "Control+Alt"),
                            new XElement("Capture", "true")
                        ),
                        new XElement("AutoConnect",
                            new XElement("Key", "A"),
                            new XElement("Modifiers", "Control+Shift+Alt"),
                            new XElement("Capture", "true")
                        )
                    )
                )
            );

            SaveXml(doc);
            return doc;
        }
        void SaveXml(XDocument doc)
        {
            lock (lockOperations)
            {
                try
                {
                    doc.Save(xmlFilePath);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error saving XML settings: {ex.Message}");
                }
            }
        }

        public void SaveSetting(string id, string value)
        {
            lock (lockOperations)
            {
                var element = xmlDocument.Root?.Element(id);
                if (element != null)
                {
                    element.Value = value;
                }
                else
                {
                    xmlDocument.Root?.Add(new XElement(id, value));
                }
                SaveXml(xmlDocument);
            }
        }
        public string GetSetting(string id, string defaultValue = "")
        {
            lock (lockOperations)
            {
                return xmlDocument.Root?.Element(id)?.Value ?? defaultValue;
            }
        }
        public Dictionary<string, string> GetAllSettings()
        {
            var settings = new Dictionary<string, string>();

            lock (lockOperations)
            {
                foreach (var element in xmlDocument.Root?.Elements() ?? [])
                {
                    if (element.Name != "Hotkeys") // Исключаем горячие клавиши
                    {
                        settings[element.Name.LocalName] = element.Value;
                    }
                }
            }

            return settings;
        }

        // Методы для работы с горячими клавишами
        public void SaveHotkey(string id, Key key, ModifierKeys modifiers, bool useFormCapture)
        {
            lock (lockOperations)
            {
                var hotkeysElement = xmlDocument.Root?.Element("Hotkeys");
                var hotkeyElement = hotkeysElement?.Element(id);

                if (hotkeyElement == null)
                {
                    hotkeyElement = new XElement(id,
                        new XElement("Key", key.ToString()),
                        new XElement("Modifiers", ModifiersToString(modifiers)),
                        new XElement("Capture", useFormCapture.ToString().ToLower())
                    );
                    hotkeysElement?.Add(hotkeyElement);
                }
                else
                {
                    hotkeyElement.Element("Key")?.SetValue(key.ToString());
                    hotkeyElement.Element("Modifiers")?.SetValue(ModifiersToString(modifiers));
                    hotkeyElement.Element("Capture")?.SetValue(useFormCapture.ToString().ToLower());
                }

                SaveXml(xmlDocument);
            }
        }
        public (Key Key, ModifierKeys Modifiers, bool UseFormCapture) GetHotkey(string id)
        {
            lock (lockOperations)
            {
                if (xmlDocument.Root?.Element("Hotkeys")?.Element(id) is not XElement hotkeyElement)
                    return (Key.None, ModifierKeys.None, true);

                var keyStr = hotkeyElement.Element("Key")?.Value ?? "None";
                var modifiersStr = hotkeyElement.Element("Modifiers")?.Value ?? "";
                var captureStr = hotkeyElement.Element("Capture")?.Value ?? "true";

                Enum.TryParse<Key>(keyStr, true, out var key);
                var modifiers = StringToModifiers(modifiersStr);
                var useFormCapture = bool.Parse(captureStr);

                return (key, modifiers, useFormCapture);
            }
        }

        static string ModifiersToString(ModifierKeys modifiers)
        {
            List<string> parts = [];

            if (modifiers.HasFlag(ModifierKeys.Control))
                parts.Add("Control");
            if (modifiers.HasFlag(ModifierKeys.Alt))
                parts.Add("Alt");
            if (modifiers.HasFlag(ModifierKeys.Shift))
                parts.Add("Shift");
            if (modifiers.HasFlag(ModifierKeys.Windows))
                parts.Add("Windows");

            return string.Join("+", parts);
        }
        static ModifierKeys StringToModifiers(string modifiersStr)
        {
            ModifierKeys modifiers = ModifierKeys.None;

            if (string.IsNullOrEmpty(modifiersStr))
                return modifiers;

            var parts = modifiersStr.Split('+');
            foreach (var part in parts)
            {
                var trimmed = part.Trim();
                if (trimmed.Equals("Control", StringComparison.OrdinalIgnoreCase))
                    modifiers |= ModifierKeys.Control;
                else if (trimmed.Equals("Alt", StringComparison.OrdinalIgnoreCase))
                    modifiers |= ModifierKeys.Alt;
                else if (trimmed.Equals("Shift", StringComparison.OrdinalIgnoreCase))
                    modifiers |= ModifierKeys.Shift;
                else if (trimmed.Equals("Windows", StringComparison.OrdinalIgnoreCase))
                    modifiers |= ModifierKeys.Windows;
            }

            return modifiers;
        }
        static string GetDefaultValue(string settingName)
        {
            return settingName switch
            {
                "Theme" => "light",
                "Microphones" => "auto",
                "NotifyApp" => "false",
                "ServerAutoConnect" => "false",
                _ => ""
            };
        }

        public void DeleteSetting(string id)
        {
            lock (lockOperations)
            {
                var element = xmlDocument.Root?.Element(id);
                element?.Remove();
                SaveXml(xmlDocument);
            }
        }
        public void ClearAllSettings()
        {
            lock (lockOperations)
            {
                // Сохраняем только структуру, удаляем значения
                foreach (var element in xmlDocument.Root?.Elements() ?? [])
                {
                    if (element.Name != "Hotkeys")
                    {
                        element.Value = GetDefaultValue(element.Name.LocalName);
                    }
                }

                // Сбрасываем горячие клавиши к значениям по умолчанию
                var defaultHotkeys = CreateDefaultXml().Root?.Element("Hotkeys");

                if (defaultHotkeys != null)
                {
                    xmlDocument.Root?.Element("Hotkeys")?.ReplaceWith(defaultHotkeys);
                }

                SaveXml(xmlDocument);
            }
        }
    }
    public class SettingsService(XmlSettingsRepository saveRepository) : ISettingsService
    {
        readonly XmlSettingsRepository saveRepository = saveRepository;
        readonly Dictionary<string, ISetting> currentSettings = [];
        readonly Dictionary<string, ISetting> lastSettings = [];
        readonly ReaderWriterLockSlim settingsLock = new();

        bool isLoading = false;
        bool hasInvalidKeys = false;

        public void ChangeHasInvalidKey(bool value)
        {
            settingsLock.EnterWriteLock();
            try
            {
                hasInvalidKeys = value;
            }
            finally
            {
                settingsLock.ExitWriteLock();
            }
        }

        public T GetSetting<T>(string id, T defaultValue = default!)
        {
            settingsLock.EnterReadLock();
            try
            {
                if (currentSettings.TryGetValue(id, out var setting))
                {
                    return setting switch
                    {
                        ComboBoxSetting comboBox when typeof(T) == typeof(string) => (T)(object)comboBox.SelectedValue,
                        CheckboxSetting checkbox when typeof(T) == typeof(bool) => (T)(object)checkbox.IsChecked,
                        HotkeySetting hotkey when typeof(T) == typeof((Key, ModifierKeys, bool)) =>
                            (T)(object)(hotkey.Key, hotkey.Modifiers, hotkey.UseFormCapture),
                        _ => defaultValue
                    };
                }
                return defaultValue;
            }
            finally
            {
                settingsLock.ExitReadLock();
            }
        }
        public List<ISetting> GetSettings()
        {
            settingsLock.EnterReadLock();
            try
            {
                return [.. currentSettings.Values];
            }
            finally
            {
                settingsLock.ExitReadLock();
            }
        }
        public void UpdateSetting<T>(string id, T value)
        {
            settingsLock.EnterWriteLock();
            try
            {
                if (currentSettings.TryGetValue(id, out var setting))
                {
                    switch (setting)
                    {
                        case ComboBoxSetting comboBox when value is string stringValue:
                            comboBox.SelectedValue = stringValue;
                            break;
                        case CheckboxSetting checkbox when value is bool boolValue:
                            checkbox.IsChecked = boolValue;
                            break;
                        case HotkeySetting hotkey when value is ValueTuple<Key, ModifierKeys, bool, bool> tupleValue:
                            hotkey.Key = tupleValue.Item1;
                            hotkey.Modifiers = tupleValue.Item2;
                            hotkey.UseFormCapture = tupleValue.Item3; 
                            hotkey.IsDuplicate = tupleValue.Item4;
                            break;
                    }
                }
            }
            finally
            {
                settingsLock.ExitWriteLock();
            }
        }
        public void LoadSettings()
        {
            settingsLock.EnterWriteLock();
            try
            {
                isLoading = true;

                // Загрузка обычных настроек
                var theme = saveRepository.GetSetting("Theme", "light");
                var microphones = saveRepository.GetSetting("Microphones", "auto");
                var notifyApp = bool.Parse(saveRepository.GetSetting("NotifyApp", "false"));
                var serverAutoConnect = bool.Parse(saveRepository.GetSetting("ServerAutoConnect", "false"));

                // Создание объектов настроек
                currentSettings["Theme"] = new ComboBoxSetting("Theme", "Тема оформления", theme, new() { ["light"] = "Светлая", ["dark"] = "Темная" }, null);
                currentSettings["Microphones"] = new ComboBoxSetting("Microphones", "Микрофон", microphones, new() { ["auto"] = "По умолчанию" }, null);

                currentSettings["NotifyApp"] = new CheckboxSetting("NotifyApp", "Включить уведомления", notifyApp, null);
                currentSettings["ServerAutoConnect"] = new CheckboxSetting("ServerAutoConnect", "Автоподключение к серверу", serverAutoConnect, null);

                // Загрузка горячих клавиш
                var micToggleHotkey = saveRepository.GetHotkey("MicToggle");
                var exitAppHotkey = saveRepository.GetHotkey("ExitApp");
                var autoConnectHotkey = saveRepository.GetHotkey("AutoConnect");

                // Создание объектов горячих клавиш
                currentSettings["MicToggle"] = new HotkeySetting("MicToggle", "Вкл/Выкл микрофон",
                    micToggleHotkey.Key, micToggleHotkey.Modifiers, null, null, null, micToggleHotkey.UseFormCapture);
                currentSettings["ExitApp"] = new HotkeySetting("ExitApp", "Выход из приложения",
                    exitAppHotkey.Key, exitAppHotkey.Modifiers, null, null, null, exitAppHotkey.UseFormCapture);
                currentSettings["AutoConnect"] = new HotkeySetting("AutoConnect", "Переподключиться к серверу",
                    autoConnectHotkey.Key, autoConnectHotkey.Modifiers, null, null, null, autoConnectHotkey.UseFormCapture);

                SaveCurrentState();
            }
            finally
            {
                isLoading = false;
                settingsLock.ExitWriteLock();
            }
        }

        bool HasChanges() => currentSettings.Values.Any(setting => setting.HasChanges());
        bool HasChangesDefaultSettings() => currentSettings.Values.Any(setting => setting.HasChangesDefaultSettings());

        void SaveCurrentState()
        {
            lastSettings.Clear();

            foreach (var pair in currentSettings)
            {
                lastSettings[pair.Key] = CloneSetting(pair.Value);
            }
        }
        void SaveSettingToRepository(ISetting setting)
        {
            if (isLoading)
                return;

            switch (setting)
            {
                case ComboBoxSetting comboBox:
                    saveRepository.SaveSetting(comboBox.Id, comboBox.SelectedValue);
                    break;
                case CheckboxSetting checkbox:
                    saveRepository.SaveSetting(checkbox.Id, checkbox.IsChecked.ToString().ToLower());
                    break;
                case HotkeySetting hotkey:
                    saveRepository.SaveHotkey(hotkey.Id, hotkey.Key, hotkey.Modifiers, hotkey.UseFormCapture);
                    break;
            }
        }

        public (bool success, string error_text) TrySaveSettings()
        {
            settingsLock.EnterWriteLock();
            try
            {
                if (hasInvalidKeys)
                    return (false, "Есть забаненные символы");

                if (!HasChanges())
                    return (false, "Настройки соответствуют сохраненным");

                foreach (var setting in currentSettings.Values)
                {
                    SaveSettingToRepository(setting);
                    setting.SaveCurrentState();
                }

                SaveCurrentState();
                return (false, string.Empty);
            }
            finally
            {
                settingsLock.ExitWriteLock();
            }
        }
        public (bool success, string error_text) TryResetToLast()
        {
            settingsLock.EnterWriteLock();
            try
            {
                if (!HasChanges())
                    return (false, "Настройки соответствуют сохраненным");

                foreach (var setting in currentSettings.Values)
                {
                    setting.ResetToOriginal();
                }

                return (true, string.Empty);
            }
            finally
            {
                settingsLock.ExitWriteLock();
            }
        }
        public (bool success, string error_text) TryResetToDefault()
        {
            settingsLock.EnterWriteLock();
            try
            {
                if (!HasChangesDefaultSettings())
                    return (false, "Настройки соответствуют установленным по умолчанию");

                saveRepository.ClearAllSettings();
                LoadSettings(); // Перезагружаем настройки по умолчанию
                return (true, string.Empty);
            }
            finally
            {
                settingsLock.ExitWriteLock();
            }
        }

        static ISetting CloneSetting(ISetting setting)
        {
            return setting switch 
            {
                HotkeySetting hotkey => new HotkeySetting(hotkey.Id, hotkey.Description, hotkey.Key, hotkey.Modifiers, null, null, null, hotkey.UseFormCapture),
                CheckboxSetting checkbox => new CheckboxSetting(checkbox.Id, checkbox.Description, checkbox.IsChecked, null),
                ComboBoxSetting comboBox => new ComboBoxSetting(comboBox.Id, comboBox.Description, comboBox.SelectedValue, comboBox.Options, null),
                _ => throw new NotSupportedException("Unsupported setting type")
            };
        }
    }
    public class SettingsUIManager(ISettingsService settingsService) : ISettingsUIManager
    {
        readonly ISettingsService settingsService = settingsService;
        readonly Dictionary<string, FrameworkElement> _uiElements = [];

        readonly FrozenSet<Key> skipKeys = new HashSet<Key> { Key.LeftCtrl, Key.RightCtrl, Key.LeftAlt, Key.RightAlt, Key.LeftShift, Key.RightShift }.ToFrozenSet();
        readonly FrozenSet<Key> bannedKeys = new HashSet<Key> { Key.Escape, Key.Apps, Key.System, Key.LWin, Key.RWin }.ToFrozenSet();

        string currentHotkeyId = string.Empty;

        public void InitializeUI(StackPanel panel, List<ISetting> settings)
        {
            panel.Children.Clear();

            // Создаем UI элементы для всех настроек
            foreach (var setting in settings)
            {
                var uiElement = CreateUIElement(setting);
                panel.Children.Add(uiElement);

                _uiElements[setting.Id] = uiElement;
            }
        }
        public void UpdateUI()
        {
            //if (_uiElements.TryGetValue(id, out var element))
            //{
            //    setting.UpdateUI();
            //}
        }
        public void RegisterUIElement(string settingId, FrameworkElement element)
        {
            _uiElements[settingId] = element;
        }
        static StackPanel CreateUIElement(ISetting setting)
        {
            return setting switch
            {
                ComboBoxSetting comboBox => comboBox.CreateUI(),
                CheckboxSetting checkbox => checkbox.CreateUI(),
                HotkeySetting hotkey => hotkey.CreateUI(),
                _ => null!
            };
        }

        public void ComboBox_OnSelectionChanged(ComboBoxSetting setting, string selectedValue)
        {
            settingsService.UpdateSetting(setting.Id, selectedValue);
            // Дополнительная логика если нужно
        }

        public void HotkeyButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button || button.Tag is not string id)
                return;

            currentHotkeyId = id;
            button.Content = "Нажмите клавишу...";
            button.Focus();
        }
        public void HotkeyButton_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            Key newKey = e.Key;
            ModifierKeys newModifiers = Keyboard.Modifiers;

            if (currentHotkeyId == string.Empty || skipKeys.Contains(newKey))
                return;

            if (bannedKeys.Contains(newKey))
            {
                settingsService.ChangeHasInvalidKey(true);
                return;
            }

            settingsService.UpdateSetting(currentHotkeyId, (newKey, newModifiers, false));
            settingsService.ChangeHasInvalidKey(false);
            currentHotkeyId = string.Empty;

            Keyboard.ClearFocus();
            e.Handled = true;
        }
        public void HotkeyButton_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button || button.Tag is not string id)
                return;
            else if (id == currentHotkeyId)
                currentHotkeyId = string.Empty;
        }
        public void OnHotkeyPressed(IHotkeySettings setting, bool oldUseFormCapture)
        {
            // Логика обработки горячих клавиш
        }
    }
}
