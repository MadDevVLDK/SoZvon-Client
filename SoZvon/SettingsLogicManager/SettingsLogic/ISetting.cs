namespace SoZvon.SettingsLogicManager.SettingsLogic
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
}
