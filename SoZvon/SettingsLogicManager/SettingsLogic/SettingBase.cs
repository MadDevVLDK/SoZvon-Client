namespace SoZvon.SettingsLogicManager.SettingsLogic
{
    public abstract class SettingBase(string id, string description) : ISetting
    {
        public string Id { get; } = id;
        public string Description { get; } = description;

        public abstract bool HasChanges();
        public abstract bool HasChangesDefaultSettings();

        public abstract void ResetToOriginal();
        public abstract void ResetToDefault();
        public abstract void SaveCurrentState();
    }
}
