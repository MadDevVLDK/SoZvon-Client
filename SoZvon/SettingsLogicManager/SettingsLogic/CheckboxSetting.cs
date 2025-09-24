namespace SoZvon.SettingsLogicManager.SettingsLogic
{
    public class CheckboxSetting : SettingBase
    {
        public bool IsChecked { get; set; }
        public bool OldIsChecked { get; set; }
        public bool DefaultIsChecked { get; set; }

        public CheckboxSetting(CheckboxSetting checkboxSetting) : base(checkboxSetting.Id, checkboxSetting.Description)
        {
            IsChecked = checkboxSetting.IsChecked;
            OldIsChecked = checkboxSetting.OldIsChecked;
            DefaultIsChecked = checkboxSetting.DefaultIsChecked;
        }
        public CheckboxSetting(string id, string description, bool currentValue, bool defaultValue) : base(id, description)
        {
            IsChecked = currentValue;
            OldIsChecked = currentValue;
            DefaultIsChecked = defaultValue;
        }

        public override bool HasChanges() => IsChecked != OldIsChecked;
        public override bool HasChangesDefaultSettings() => IsChecked != DefaultIsChecked;

        public override void ResetToOriginal() => IsChecked = OldIsChecked;
        public override void ResetToDefault() => IsChecked = DefaultIsChecked;
        public override void SaveCurrentState() => OldIsChecked = IsChecked;
    }
}
