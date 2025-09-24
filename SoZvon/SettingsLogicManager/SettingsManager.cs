using System.Threading.Channels;
using System.Windows.Input;

namespace SoZvon.SettingsLogicManager
{
    using SettingsLogic;
    using SubClasses;

    using Action_IUser = Main_Thread.Action_IUser;
    using ActionFromIUser = Main_Thread.ActionFromIUser;
    using ActionToIUser = Main_Thread.ActionToIUser;
    using IUser = Main_Thread.IUser;

    public partial class SettingsManager
    {
        readonly Channel<Action_IUser> IUser_Channel = Channel.CreateBounded<Action_IUser>(new BoundedChannelOptions(2000) { FullMode = BoundedChannelFullMode.Wait });

        async Task IUser_Channel_Thread(CancellationToken cancellationToken)
        {
            try
            {
                await foreach (Action_IUser action_IUser in IUser_Channel.Reader.ReadAllAsync(cancellationToken))
                {
                    try
                    {
                        InterpretateActionIUser(action_IUser).Invoke();
                    }
                    catch (OperationCanceledException) { }
                    catch (My_Exception ex)
                    {
                        Message_Error(ex.Title ?? action_IUser.Action.ToString(), ex.Message);
                    }
                }
            }
            catch (OperationCanceledException) { return; }
        }
        Action InterpretateActionIUser(Action_IUser action_IUser)
        {
            Action action;

            var dict = action_IUser.Params;

            switch (action_IUser.Action)
            {
                case ActionFromIUser.OnStart:
                    {
                        if (dict.Count != 0)
                            throw new My_Exception("no valid params");

                        action = StartSettings;
                        break;
                    }
                case ActionFromIUser.UpdateSetting:
                    {
                        if (dict.Count != 2 || !dict.TryGetValue<string>("id", out var id) || !dict.TryGetValue("value", out var value))
                            throw new My_Exception("no valid params");

                        action = () => UpdateSetting(id, value);
                        break;
                    }
                case ActionFromIUser.ChangeHasInvalidKeySetting:
                    {
                        if (dict.Count != 1 || !dict.TryGetValue<bool>("value", out var value))
                            throw new My_Exception("no valid params");

                        action = () => ChangeHasInvalidKey(value);
                        break;
                    }
                case ActionFromIUser.TrySaveSettings:
                    {
                        if (dict.Count != 0)
                            throw new My_Exception("no valid params");

                        action = () =>
                        {
                            var (success, error_text) = TrySaveSettings();

                            if (!success)
                            {
                                Message_Error("Settings", error_text ?? "unknown_error");
                            }
                            else Message_Notify("Settings", "Настройки сохранены успешно!");
                        };
                        break;
                    }
                case ActionFromIUser.TryResetToDefaultSettings:
                    {
                        if (dict.Count != 0)
                            throw new My_Exception("no valid params");

                        action = () =>
                        {
                            var(success, error_text) = TryResetToDefault();

                            if (!success)
                            {
                                Message_Error("Settings", error_text ?? "unknown_error");
                            }
                            else Message_Notify("Settings", "Настройки сброшены к сохраненным значениям!");
                        };
                        break;
                    }
                case ActionFromIUser.TryResetToLastSettings:
                    {
                        if (dict.Count != 0)
                            throw new My_Exception("no valid params");

                        action = () =>
                        {
                            var (success, error_text) = TryResetToLast();

                            if (!success)
                            {
                                Message_Error("Settings", error_text ?? "unknown_error");
                            }
                            else Message_Notify("Settings", "Настройки сброшены к значениям по умолчанию!");
                        };
                        break;
                    }
                default:
                    throw new My_Exception("WTF Arguements");
            }

            return action;
        }
        public async void OnIUserAction(ActionFromIUser action_IUser, Dictionary<string, object> dict) => await IUser_Channel.Writer.WriteAsync(new(action_IUser, dict));

        void OnHotkeyPressedSettings(string id, bool UseFormCapture) => User.OnInterfacesAction(ActionToIUser.OnHotkeyPressedSettings, new() {
            ["id"] = id,
            ["UseFormCapture"] = UseFormCapture
        });
        void LoadUI(List<ISetting> settingsUI) => User.OnInterfacesAction(ActionToIUser.MakeUISettings, new() {
            ["settingsUI"] = settingsUI
        });
        void UpdateUISetting(string id) => User.OnInterfacesAction(ActionToIUser.UpdateUISetting, new() {
            ["id"] = id,
        });
        void UpdateUISettings() => User.OnInterfacesAction(ActionToIUser.UpdateUISettings, []);
        void Message_Error(string title, string text) => User.OnInterfacesAction(ActionToIUser.MessageErrorOccurred, new() {
            ["title"] = title,
            ["text"] = text
        });
        void Message_Notify(string title, string text) => User.OnInterfacesAction(ActionToIUser.MessageNotifyOccurred, new() {
            ["title"] = title,
            ["text"] = text
        });
    }
    public partial class SettingsManager : ISettingsService
    {
        readonly IUser User;

        readonly GlobalHotKeyManager hotKeyManager = new();
        readonly XmlSettingsRepository saveRepository = new();

        readonly Dictionary<string, ISetting> currentSettings = [];
        readonly Dictionary<string, ISetting> lastSettings = [];

        readonly Dictionary<string, string> defaultSettings = [];
        readonly Dictionary<string, Tuple<Key, ModifierKeys, bool>> defaultHotkeys = [];

        readonly ReaderWriterLockSlim settingsLock = new();
        readonly CancellationToken cts = new();

        bool isLoading = false;
        bool hasInvalidKeys = false;

        public SettingsManager(IUser user)
        {
            User = user;

            _ = IUser_Channel_Thread(cts);
        }

        public void StartSettings()
        {
            settingsLock.EnterWriteLock();
            try
            {
                LoadSettings();
                LoadUI([.. currentSettings.Values]);

                _ = hotKeyManager.ReadKeysAsync();
                hotKeyManager.ClearAndAddRangeHotkey([.. currentSettings.Values.OfType<IHotkeySettings>()]);
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
            AddDefaultValueHotkeySetting("ExitApp", new(Key.Q, ModifierKeys.Control | ModifierKeys.Alt, false));
            AddDefaultValueHotkeySetting("Reconnect", new(Key.A, ModifierKeys.Control | ModifierKeys.Shift | ModifierKeys.Alt, true));
            AddDefaultValueHotkeySetting("AppSoundUp", new(Key.Up, ModifierKeys.Control | ModifierKeys.Shift, false));
            AddDefaultValueHotkeySetting("AppSoundDown", new(Key.Down, ModifierKeys.Control | ModifierKeys.Shift, false));

            saveRepository.StartProperties(defaultSettings.ToDictionary(), defaultHotkeys.ToDictionary());

            // Создание объектов настроек
            MakeFastComboBoxSetting("Theme", "Тема оформления", new() { ["light"] = "Светлая", ["dark"] = "Темная" });
            MakeFastComboBoxSetting("Microphones", "Микрофон", new() { ["auto"] = "По умолчанию" });

            MakeFastCheckBoxSetting("NotifyApp", "Включить уведомления");
            MakeFastCheckBoxSetting("ServerAutoConnect", "Автоподключение к серверу");

            MakeFastHotkeySetting("MicToggle", "Вкл/Выкл микрофон");
            MakeFastHotkeySetting("ExitApp", "Выход из приложения");
            MakeFastHotkeySetting("Reconnect", "Переподключиться к серверу");
            MakeFastHotkeySetting("AppSoundUp", "Увеличить звук в приложении", true, 300, 150);
            MakeFastHotkeySetting("AppSoundDown", "Уменьшить звук в приложении", true, 300, 150);

            SaveCurrentStates();
            CheckForDuplicateHotkeys(); // Проверяем дубликаты после загрузки

            isLoading = false;
        }

        void AddDefaultValueSetting(string id, string defaultValue)
        {
            defaultSettings[id] = defaultValue;
        }
        void AddDefaultValueHotkeySetting(string id, Tuple<Key, ModifierKeys, bool> defaultValue)
        {
            defaultHotkeys[id] = defaultValue;
        }

        string GetDefaultSetting(string id) => defaultSettings.TryGetValue(id, out var value) ? value : default!;
        Tuple<Key, ModifierKeys, bool> GetDefaultHotkey(string id) => defaultHotkeys.TryGetValue(id, out var value) ? value : default!;

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
        void MakeFastHotkeySetting(string id, string description, bool supportsAutoRepeat = false, int initialRepeatDelay = 300, int repeatInterval = 50)
        {
            var defaultValue = GetDefaultHotkey(id);
            var value = saveRepository.GetHotkey(id);

            currentSettings[id] = new HotkeySetting(id, description, value, defaultValue, supportsAutoRepeat, initialRepeatDelay, repeatInterval, this);
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
        bool CheckIfHotkeyExists(string id, Key key, ModifierKeys modifiers)
        {
            var hotkeySettings = currentSettings.Values.OfType<HotkeySetting>().Where(h => h.Id != id);

            return hotkeySettings.Any(h => h.Key == key && h.Modifiers == modifiers && key != Key.None);
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
                        case HotkeySetting hotkey when value is ValueTuple<Key, ModifierKeys, bool> tupleValue:
                            hotkey.Key = tupleValue.Item1;
                            hotkey.Modifiers = tupleValue.Item2;
                            hotkey.UseFormCapture = tupleValue.Item3;
                            hotkey.IsDuplicate = CheckIfHotkeyExists(id, tupleValue.Item1, tupleValue.Item2);
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

                    UpdateUISetting(id);
                }
            }
            finally
            {
                settingsLock.ExitWriteLock();
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

                UpdateUISettings();

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
                UpdateUISettings();

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
                OnHotkeyPressedSettings(Id, OldUseFormCapture);
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
}
