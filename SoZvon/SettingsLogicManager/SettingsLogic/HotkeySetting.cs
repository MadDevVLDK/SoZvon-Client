using System.Windows.Input;

namespace SoZvon.SettingsLogicManager.SettingsLogic
{
    public class HotkeySetting : SettingBase, IHotkeySettings
    {
        readonly ISettingsService settingsService;

        public bool IsDuplicate { get; set; } = false;
        public bool SupportsAutoRepeat { get; }
        public int InitialRepeatDelay { get; }
        public int RepeatInterval { get; }

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

        public void OnHotkeyPressed() => settingsService.OnHotkeyPressed(Id, OldUseFormCapture);
    }
}
