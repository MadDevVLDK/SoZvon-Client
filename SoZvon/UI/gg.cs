using System.Collections.Frozen;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Xml.Linq;
using Keys = System.Windows.Forms.Keys;

namespace SoZvon.UI.Room_Pages
{
    public interface ISettingsService
    {
        void ChangeHasInvalidKey(bool value);

        List<ISetting> GetSettings();
        void StartSettings();

        (bool success, string error_text) TrySaveSettings();
        (bool success, string error_text) TryResetToLast();
        (bool success, string error_text) TryResetToDefault();

        void OnHotkeyPressed(string Id, bool OldUseFormCapture);

        void UpdateSetting<T>(string id, T value);
    }
    public interface ISettingsUIManager
    {
        List<ISettingUI> MakeUIFromISetting(List<ISetting> settings);
        void InitializeUI(StackPanel panel, List<ISettingUI> settings);

        void UpdateUI();

        void UpdateMicrophoneOptions(Dictionary<string, string> options);
        void ComboBox_OnSelectionChanged(string id, string selectedValue);

        void HotkeyButton_Click(object sender, RoutedEventArgs e);
        void HotkeyButton_PreviewKeyDown(object sender, KeyEventArgs e);
        void HotkeyButton_LostFocus(object sender, RoutedEventArgs e);

        void CheckBox_Changed(string id, bool value);
        void HotkeyCheckBox_Changed(string id, bool value);

        void OnHotkeyPressed(string id);
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
        public void ClearAndAddRangeHotkey(List<IHotkeySettings> hotkeySettings)
        {
            lock (currentKeys_lock)
            {
                NeedCheck = false;

                currentKeys.Clear();

                foreach (var hotkey in hotkeySettings)
                {
                    currentKeys[hotkey.Id] = hotkey;
                }

                NeedCheck = true;
            }
                
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
    public class XmlSettingsRepository
    {
        readonly string xmlFilePath = "settings.xml";
        readonly object lockOperations = new();

        XDocument xmlDocument = null!;

        Dictionary<string, string> defaultSettings = null!;
        Dictionary<string, Tuple<Key, ModifierKeys, bool>> defaultHotkeys = null!;

        public void StartProperties(Dictionary<string, string> _defaultSettings, Dictionary<string, Tuple<Key, ModifierKeys, bool>> _defaultHotkeys)
        {
            lock (lockOperations)
            {
                defaultSettings = _defaultSettings;
                defaultHotkeys = _defaultHotkeys;

                xmlDocument = LoadOrCreateXml();
                EnsureDefaultValues();
            }
        }
        
        XDocument LoadOrCreateXml()
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
        XDocument CreateDefaultXml()
        {
            var rootElement = new XElement("Settings");

            // Добавляем обычные настройки
            if (defaultSettings != null)
            {
                foreach (var setting in defaultSettings)
                {
                    rootElement.Add(new XElement(setting.Key, setting.Value));
                }
            }

            // Добавляем горячие клавиши
            if (defaultHotkeys != null && defaultHotkeys.Count > 0)
            {
                var hotkeysElement = new XElement("Hotkeys");

                foreach (var hotkey in defaultHotkeys)
                {
                    var hotkeyElement = new XElement(hotkey.Key);

                    // Предполагаем, что Tuple содержит 3 элемента
                    hotkeyElement.Add(new XElement("Key", hotkey.Value.Item1.ToString() ?? ""));
                    hotkeyElement.Add(new XElement("Modifiers", ModifiersToString(hotkey.Value.Item2)));
                    hotkeyElement.Add(new XElement("Capture", hotkey.Value.Item3.ToString().ToLower()));

                    hotkeysElement.Add(hotkeyElement);
                }

                rootElement.Add(hotkeysElement);
            }

            var doc = new XDocument(rootElement);
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
        void EnsureDefaultValues()
        {
            bool needsSave = false;

            // Проверяем и добавляем отсутствующие обычные настройки
            foreach (var setting in defaultSettings)
            {
                var element = xmlDocument.Root?.Element(setting.Key);
                if (element == null)
                {
                    xmlDocument.Root?.Add(new XElement(setting.Key, setting.Value));
                    needsSave = true;
                }
            }

            // Проверяем и добавляем отсутствующие горячие клавиши
            var hotkeysElement = xmlDocument.Root?.Element("Hotkeys");
            if (hotkeysElement == null)
            {
                hotkeysElement = new XElement("Hotkeys");
                xmlDocument.Root?.Add(hotkeysElement);
                needsSave = true;
            }

            foreach (var hotkey in defaultHotkeys)
            {
                var hotkeyElement = hotkeysElement.Element(hotkey.Key);
                if (hotkeyElement == null)
                {
                    hotkeyElement = new XElement(hotkey.Key,
                        new XElement("Key", hotkey.Value.Item1.ToString()),
                        new XElement("Modifiers", ModifiersToString(hotkey.Value.Item2)),
                        new XElement("Capture", hotkey.Value.Item3.ToString().ToLower())
                    );
                    hotkeysElement.Add(hotkeyElement);
                    needsSave = true;
                }
                else
                {
                    //доделать парсинг: Проверка каждого значения на валидность ("Key" = Key, "Modifiers" = ModifierKeys, "Capture" = bool)
                    bool hotkeyNeedsFix = false;

                    // Проверка Key
                    var keyElement = hotkeyElement.Element("Key");
                    if (keyElement == null)
                    {
                        keyElement = new XElement("Key", hotkey.Value.Item1.ToString());
                        hotkeyElement.Add(keyElement);
                        hotkeyNeedsFix = true;
                    }
                    else if (!IsValidKey(keyElement.Value))
                    {
                        keyElement.Value = hotkey.Value.Item1.ToString();
                        hotkeyNeedsFix = true;
                    }

                    // Проверка Modifiers
                    var modifiersElement = hotkeyElement.Element("Modifiers");
                    if (modifiersElement == null)
                    {
                        modifiersElement = new XElement("Modifiers", ModifiersToString(hotkey.Value.Item2));
                        hotkeyElement.Add(modifiersElement);
                        hotkeyNeedsFix = true;
                    }
                    else if (!IsValidModifiers(modifiersElement.Value, out var parsedModifiers))
                    {
                        modifiersElement.Value = ModifiersToString(hotkey.Value.Item2);
                        hotkeyNeedsFix = true;
                    }

                    // Проверка Capture
                    var captureElement = hotkeyElement.Element("Capture");
                    if (captureElement == null)
                    {
                        captureElement = new XElement("Capture", hotkey.Value.Item3.ToString().ToLower());
                        hotkeyElement.Add(captureElement);
                        hotkeyNeedsFix = true;
                    }
                    else if (!bool.TryParse(captureElement.Value, out var captureValue) || captureValue != hotkey.Value.Item3)
                    {
                        captureElement.Value = hotkey.Value.Item3.ToString().ToLower();
                        hotkeyNeedsFix = true;
                    }

                    if (hotkeyNeedsFix)
                    {
                        needsSave = true;
                    }
                }
            }

            if (needsSave)
            {
                SaveXml(xmlDocument);
            }
        }

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
        public Tuple<Key, ModifierKeys, bool> GetHotkey(string id)
        {
            lock (lockOperations)
            {
                if (xmlDocument.Root?.Element("Hotkeys")?.Element(id) is not XElement hotkeyElement)
                    return new(Key.None, ModifierKeys.None, true);

                var keyStr = hotkeyElement.Element("Key")?.Value ?? "None";
                var modifiersStr = hotkeyElement.Element("Modifiers")?.Value ?? "";
                var captureStr = hotkeyElement.Element("Capture")?.Value ?? "true";

                Enum.TryParse<Key>(keyStr, true, out var key);
                var modifiers = StringToModifiers(modifiersStr);

                if(!bool.TryParse(captureStr, out bool result)) 
                    result = false;

                var useFormCapture = result;

                return new(key, modifiers, useFormCapture);
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
        public void DeleteSetting(string id)
        {
            lock (lockOperations)
            {
                var element = xmlDocument.Root?.Element(id);
                element?.Remove();
                SaveXml(xmlDocument);
            }
        }
        public void ClearAllSettings(Dictionary<string, string> defaultSettings, Dictionary<string, Tuple<Key, ModifierKeys, bool>> defaultHotkeys)
        {
            lock (lockOperations)
            {
                // Сбрасываем обычные настройки к дефолтным значениям
                foreach (var setting in defaultSettings)
                {
                    var element = xmlDocument.Root?.Element(setting.Key);
                    if (element != null)
                    {
                        element.Value = setting.Value;
                    }
                }

                // Сбрасываем горячие клавиши к дефолтным значениям
                xmlDocument.Root?.Element("Hotkeys")?.Remove();

                // Создаем новый элемент с дефолтными горячими клавишами
                var newHotkeysElement = new XElement("Hotkeys");
                foreach (var hotkey in defaultHotkeys)
                {
                    newHotkeysElement.Add(new XElement(hotkey.Key,
                        new XElement("Key", hotkey.Value.Item1.ToString()),
                        new XElement("Modifiers", ModifiersToString(hotkey.Value.Item2)),
                        new XElement("Capture", hotkey.Value.Item3.ToString().ToLower())
                    ));
                }

                xmlDocument.Root?.Add(newHotkeysElement);
                SaveXml(xmlDocument);
            }
        }

        static bool IsValidKey(string keyString)
        {
            try
            {
                // Пытаемся распарсить строку как Key
                var key = (Key)Enum.Parse(typeof(Key), keyString, true);
                return Enum.IsDefined(typeof(Key), key);
            }
            catch
            {
                return false;
            }
        }
        static bool IsValidModifiers(string modifiersString, out ModifierKeys modifiers)
        {
            modifiers = ModifierKeys.None;

            if (string.IsNullOrEmpty(modifiersString))
                return true;

            try
            {
                var parts = modifiersString.Split('+');
                foreach (var part in parts)
                {
                    var trimmedPart = part.Trim();
                    if (string.IsNullOrEmpty(trimmedPart))
                        continue;

                    switch (trimmedPart.ToLower())
                    {
                        case "control": modifiers |= ModifierKeys.Control; break;
                        case "ctrl": modifiers |= ModifierKeys.Control; break;
                        case "alt": modifiers |= ModifierKeys.Alt; break;
                        case "shift": modifiers |= ModifierKeys.Shift; break;
                        case "win": modifiers |= ModifierKeys.Windows; break;
                        case "windows": modifiers |= ModifierKeys.Windows; break;
                        default: return false; // Неизвестный модификатор
                    }
                }
                return true;
            }
            catch
            {
                return false;
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
    }
    public class SettingsService(ISettingsPage settingsPage) : ISettingsService
    {
        readonly ISettingsPage settingsPage = settingsPage;

        readonly GlobalHotKeyManager hotKeyManager = new();
        readonly XmlSettingsRepository saveRepository = new();

        readonly Dictionary<string, ISetting> currentSettings = [];
        readonly Dictionary<string, ISetting> lastSettings = [];

        readonly Dictionary<string, string> defaultSettings = [];
        readonly Dictionary<string, Tuple<Key, ModifierKeys, bool>> defaultHotkeys = [];

        readonly ReaderWriterLockSlim settingsLock = new();

        bool isLoading = false;
        bool hasInvalidKeys = false;

        public void StartSettings()
        {
            settingsLock.EnterWriteLock();
            try
            {
                LoadSettings();
                LoadUI();

                var hotkeys = currentSettings.Values.OfType<IHotkeySettings>().ToList();
                
                hotKeyManager.ClearAndAddRangeHotkey(hotkeys);

                _ = hotKeyManager.ReadKeysAsync();
                hotKeyManager.StartChecking();
            }
            finally
            {
                settingsLock.ExitWriteLock();
            }
        }
        void LoadSettings()
        {
            isLoading = true;

            // Назначение настроек по умолчанию
            AddDefaultValueSetting("Theme", "light");
            AddDefaultValueSetting("Microphones", "auto");
            AddDefaultValueSetting("NotifyApp", "false");
            AddDefaultValueSetting("ServerAutoConnect", "false");

            AddDefaultValueHotkeySetting("MicToggle", new(Key.M, ModifierKeys.Control, false));
            AddDefaultValueHotkeySetting("ExitApp", new(Key.Q, ModifierKeys.Control | ModifierKeys.Alt, true));
            AddDefaultValueHotkeySetting("Reconnect", new(Key.A, ModifierKeys.Control | ModifierKeys.Shift | ModifierKeys.Alt, true));

            saveRepository.StartProperties(defaultSettings.ToDictionary(), defaultHotkeys.ToDictionary());

            // Создание объектов настроек
            MakeFastComboBoxSetting("Theme", "Тема оформления", new() { ["light"] = "Светлая", ["dark"] = "Темная" });
            MakeFastComboBoxSetting("Microphones", "Микрофон", new() { ["auto"] = "По умолчанию" });

            MakeFastCheckBoxSetting("NotifyApp", "Включить уведомления");
            MakeFastCheckBoxSetting("ServerAutoConnect", "Автоподключение к серверу");

            MakeFastHotkeySetting("MicToggle", "Вкл/Выкл микрофон");
            MakeFastHotkeySetting("ExitApp", "Выход из приложения");
            MakeFastHotkeySetting("Reconnect", "Переподключиться к серверу");

            SaveCurrentStates();
            CheckForDuplicateHotkeys(); // Проверяем дубликаты после загрузки

            isLoading = false;
        }
        void LoadUI() => settingsPage.MakeSettingsUI([.. currentSettings.Values]);

        void AddDefaultValueSetting(string id, string defaultValue)
        {
            defaultSettings[id] = defaultValue;
        }
        void AddDefaultValueHotkeySetting(string id, Tuple<Key, ModifierKeys, bool> defaultValue)
        {
            defaultHotkeys[id] = defaultValue;
        }

        public string GetDefaultSetting(string id)
        {
            return defaultSettings.TryGetValue(id, out var value) ? value : "";
        }
        public Tuple<Key, ModifierKeys, bool> GetDefaultHotkey(string id)
        {
            return defaultHotkeys.TryGetValue(id, out var value) ? value : new(Key.None, ModifierKeys.None, true);
        }

        void MakeFastComboBoxSetting(string id, string description, Dictionary<string, string> values)
        {
            var defaultValue = GetDefaultSetting(id);
            var value = saveRepository.GetSetting(id, defaultValue);

            currentSettings[id] = new ComboBoxSetting(id, description, value, defaultValue, values);
        }
        void MakeFastCheckBoxSetting(string id, string description)
        {
            var defaultValue = GetDefaultSetting(id);
            var value = saveRepository.GetSetting(id, defaultValue);

            currentSettings[id] = new CheckboxSetting(id, description, bool.Parse(value), bool.Parse(defaultValue));
        }
        void MakeFastHotkeySetting(string id, string description)
        {
            var defaultValue = GetDefaultHotkey(id);
            var value = saveRepository.GetHotkey(id);

            currentSettings[id] = new HotkeySetting(id, description, value, defaultValue, this);
        }

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
                        case HotkeySetting hotkey when value is bool val:
                            hotkey.UseFormCapture = val;
                            hotkey.IsDuplicate = false;
                            break;
                        default:
                            throw new NotSupportedException("Unsupported setting type");
                    }

                    // Проверка дубликатов после обновления горячих клавиш
                    if (setting is HotkeySetting)
                    {
                        CheckForDuplicateHotkeys();
                    }
                }
            }
            finally
            {
                settingsLock.ExitWriteLock();
            }
        }        

        bool HasChanges() => currentSettings.Values.Any(setting => setting.HasChanges());
        bool HasChangesDefaultSettings() => currentSettings.Values.Any(setting => setting.HasChangesDefaultSettings());
        bool CheckForDuplicateHotkeys()
        {
            var hotkeySettings = currentSettings.Values.OfType<HotkeySetting>().ToList();

            for (int i = 0; i < hotkeySettings.Count; i++)
            {
                for (int j = i + 1; j < hotkeySettings.Count; j++)
                {
                    var setting1 = hotkeySettings[i];
                    var setting2 = hotkeySettings[j];

                    if (setting1.Key == setting2.Key && setting1.Modifiers == setting2.Modifiers && setting1.Key != Key.None)
                    {
                        setting1.IsDuplicate = true;
                        setting2.IsDuplicate = true;
                        return true;
                    }
                }
            }

            // Сбросить флаги дубликатов, если дубликатов нет
            foreach (var hotkey in hotkeySettings)
            {
                hotkey.IsDuplicate = false;
            }

            return false;
        }

        void SaveCurrentStates()
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

                if (CheckForDuplicateHotkeys())
                    return (false, "Обнаружены дублирующиеся горячие клавиши");

                if (!HasChanges())
                    return (false, "Настройки соответствуют сохраненным");

                List<IHotkeySettings> hotkeys = [];

                foreach (var setting in currentSettings.Values)
                {
                    setting.SaveCurrentState();
                    SaveSettingToRepository(setting);

                    if (setting is IHotkeySettings hotkey)
                        hotkeys.Add(hotkey);
                }

                SaveCurrentStates();
                hotKeyManager.ClearAndAddRangeHotkey(hotkeys);

                return (true, string.Empty);
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
                    //SaveSettingToRepository(setting);
                }

                settingsPage.UpdateUI();

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

                foreach (var setting in currentSettings.Values)
                {
                    setting.ResetToDefault();
                }

                //saveRepository.ClearAllSettings();
                settingsPage.UpdateUI();

                return (true, string.Empty);
            }
            finally
            {
                settingsLock.ExitWriteLock();
            }
        }

        public void OnHotkeyPressed(string Id, bool OldUseFormCapture)
        {
            settingsLock.EnterReadLock();
            try
            {
                settingsPage.OnHotkeyPressed(Id, OldUseFormCapture);
            }
            finally
            {
                settingsLock.ExitReadLock();
            }
        }

        static ISetting CloneSetting(ISetting setting)
        {
            return setting switch
            {
                HotkeySetting hotkey => new HotkeySetting(hotkey),
                CheckboxSetting checkbox => new CheckboxSetting(checkbox),
                ComboBoxSetting comboBox => new ComboBoxSetting(comboBox),
                _ => throw new NotSupportedException("Unsupported setting type")
            };
        }
    }
    public class SettingsUIManager(ISettingsPage settingsPage) : ISettingsUIManager
    {
        readonly ISettingsPage settingsPage = settingsPage;
        readonly Dictionary<string, ISettingUI> settingUIs = [];

        readonly FrozenSet<Key> skipKeys = new HashSet<Key> { Key.LeftCtrl, Key.RightCtrl, Key.LeftAlt, Key.RightAlt, Key.LeftShift, Key.RightShift, Key.LWin, Key.RWin }.ToFrozenSet();
        readonly FrozenSet<Key> bannedKeys = new HashSet<Key> { Key.Escape, Key.Apps, Key.System }.ToFrozenSet();

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
        public void UpdateUI()
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
            settingUIs[id].UpdateUI();
        }

        public void HotkeyCheckBox_Changed(string id, bool value)
        {
            settingsPage.UpdateSetting(id, value);
            settingUIs[id].UpdateUI();
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

            // Проверка на дубликаты
            bool isDuplicate = CheckIfHotkeyExists(newKey, newModifiers, currentHotkeyId);

            // Обновляем настройку
            settingsPage.UpdateSetting(currentHotkeyId, (newKey, newModifiers, false, isDuplicate));
            UpdateUI();

            settingsPage.ChangeHasInvalidKey(false);
            currentHotkeyId = string.Empty;
            Keyboard.ClearFocus();
            e.Handled = true;
        }
        public void HotkeyButton_LostFocus(object sender, RoutedEventArgs e)
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
                    MessageBox.Show($"Hotkey pressed: {id}");
                    break;
                default:
                    throw new Exception("WTF");
            }
        }

        bool CheckIfHotkeyExists(Key key, ModifierKeys modifiers, string currentId)
        {
            var settings = settingsPage.GetSettings();
            var hotkeySettings = settings.OfType<HotkeySetting>().Where(h => h.Id != currentId);

            return hotkeySettings.Any(h => h.Key == key && h.Modifiers == modifiers && key != Key.None);
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
