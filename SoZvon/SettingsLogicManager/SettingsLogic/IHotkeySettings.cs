namespace SoZvon.SettingsLogicManager.SettingsLogic
{
    public interface IHotkeySettings
    {
        string Id { get; }
        bool SupportsAutoRepeat { get; }
        int InitialRepeatDelay { get; } // ms
        int RepeatInterval { get; } // ms
        System.Windows.Input.Key OldKey { get; }
        System.Windows.Input.ModifierKeys OldModifiers { get; }
        void OnHotkeyPressed();
    }
}
