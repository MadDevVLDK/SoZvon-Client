using System.Windows.Input;
using System.Xml.Linq;
using System.IO;

namespace SoZvon.SettingsLogicManager
{
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

                if (!bool.TryParse(captureStr, out bool result))
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
}
