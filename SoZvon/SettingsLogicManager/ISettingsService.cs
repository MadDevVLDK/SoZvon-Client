namespace SoZvon.SettingsLogicManager
{
    public interface ISettingsService
    {
        void OnIUserAction(Main_Thread.ActionFromIUser action_IUser, Dictionary<string, object> dict);

        void OnHotkeyPressed(string Id, bool OldUseFormCapture);
    }
}
