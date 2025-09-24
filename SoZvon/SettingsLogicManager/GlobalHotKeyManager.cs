using DllImport = System.Runtime.InteropServices.DllImportAttribute;
using Keys = System.Windows.Forms.Keys;
using Key = System.Windows.Input.Key;
using ModifierKeys = System.Windows.Input.ModifierKeys;

namespace SoZvon.SettingsLogicManager
{
    using IHotkeySettings = SettingsLogic.IHotkeySettings;

    public class HotkeyState
    {
        public IHotkeySettings Settings { get; set; }
        public DateTime FirstPressTime { get; set; }
        public double TimeSinceFirstPress { get; set; } // ms
        public double TimeSinceLastRepeat { get; set; } // ms
        public int RepeatCount { get; set; }
    }
    public class GlobalHotKeyManager
    {
        [DllImport("user32.dll")]
        static extern short GetAsyncKeyState(Keys vkey);

        readonly Dictionary<string, HotkeyState> activeHotkeys = [];
        readonly Dictionary<string, IHotkeySettings> registeredHotkeys = [];

        readonly CancellationTokenSource cts = new();
        readonly object currentKeys_lock = new();

        bool NeedCheck = false;

        public void ClearAndAddRangeHotkey(List<IHotkeySettings> hotkeySettings)
        {
            lock (currentKeys_lock)
            {
                NeedCheck = false;
                registeredHotkeys.Clear();
                activeHotkeys.Clear();

                foreach (var hotkey in hotkeySettings)
                {
                    registeredHotkeys[hotkey.Id] = hotkey;
                }

                NeedCheck = true;
            }
        }

        public async Task ReadKeysAsync()
        {
            try
            {
                var lastCheckTime = DateTime.Now;
                var pressedKeys = new HashSet<int>();

                while (!cts.IsCancellationRequested)
                {
                    var currentTime = DateTime.Now;
                    var elapsed = (currentTime - lastCheckTime).TotalMilliseconds;

                    lock (currentKeys_lock)
                    {
                        if (NeedCheck)
                        {
                            // Обновляем состояние всех активных горячих клавиш (для auto-repeat)
                            UpdateActiveHotkeys(elapsed);

                            // Проверяем все зарегистрированные горячие клавиши
                            CheckRegisteredHotkeys(currentTime);
                        }
                    }

                    lastCheckTime = currentTime;
                    await Task.Delay(10, cts.Token);
                }
            }
            catch (OperationCanceledException) { }
            catch { }
        }

        void UpdateActiveHotkeys(double elapsedMs)
        {
            var keysToRemove = new List<string>();

            foreach (var kvp in activeHotkeys)
            {
                var state = kvp.Value;
                var settings = state.Settings;

                // Автоматически удаляем если клавиша больше не нажата
                if (!IsKeyCombinationPressed(settings.OldKey, settings.OldModifiers))
                {
                    keysToRemove.Add(kvp.Key);
                    continue;
                }

                if (!settings.SupportsAutoRepeat)
                    continue;

                state.TimeSinceFirstPress += elapsedMs;
                state.TimeSinceLastRepeat += elapsedMs;

                // Проверяем, прошла ли начальная задержка
                if (state.TimeSinceFirstPress < settings.InitialRepeatDelay)
                    continue;

                // Проверяем, прошла ли задержка между повторениями
                if (state.TimeSinceLastRepeat < settings.RepeatInterval)
                    continue;

                // settings.OnHotkeyPressed() надо true в параметр
                settings.OnHotkeyPressed();
                state.TimeSinceLastRepeat = 0;
                state.RepeatCount++;
            }

            // Удаляем неактивные горячие клавиши
            foreach (var key in keysToRemove)
            {
                activeHotkeys.Remove(key);
            }
        }
        void CheckRegisteredHotkeys(DateTime currentTime)
        {
            foreach (var settings in registeredHotkeys.Values)
            {
                var hotkeyKey = settings.Id;

                if (IsKeyCombinationPressed(settings.OldKey, settings.OldModifiers))
                {
                    // Если горячая клавиша нажата впервые
                    if (!activeHotkeys.ContainsKey(hotkeyKey))
                    {
                        var newState = new HotkeyState
                        {
                            Settings = settings,
                            FirstPressTime = currentTime,
                            TimeSinceFirstPress = 0,
                            TimeSinceLastRepeat = 0,
                            RepeatCount = 0
                        };

                        activeHotkeys[hotkeyKey] = newState;

                        // Выполняем действие при первом нажатии
                        // settings.OnHotkeyPressed() надо false в параметр
                        settings.OnHotkeyPressed();
                    }
                }
                else
                {
                    activeHotkeys.Remove(hotkeyKey);
                }
            }
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
            return IsKeyPressed((Keys)System.Windows.Input.KeyInterop.VirtualKeyFromKey(key));
        }

        public void StartChecking()
        {
            lock (currentKeys_lock)
            {
                NeedCheck = true;
            }
        }
        public void StopChecking()
        {
            lock (currentKeys_lock)
            {
                NeedCheck = false;
                activeHotkeys.Clear();
            }
        }

        public void Dispose()
        {
            cts.Cancel();
            cts.Dispose();
            activeHotkeys.Clear();
            registeredHotkeys.Clear();
        }
    }
}
