namespace SoZvon.SettingsLogicManager.SettingsLogic
{
    public class ComboBoxSetting : SettingBase
    {
        public string SelectedValue { get; set; }
        public string OldSelectedValue { get; set; }
        public string DefaultSelectedValue { get; set; }

        public Dictionary<string, string> Options { get; private set; }

        public ComboBoxSetting(ComboBoxSetting comboboxSetting) : base(comboboxSetting.Id, comboboxSetting.Description)
        {
            SelectedValue = comboboxSetting.SelectedValue;
            OldSelectedValue = comboboxSetting.OldSelectedValue;
            DefaultSelectedValue = comboboxSetting.DefaultSelectedValue;
            Options = comboboxSetting.Options;
        }
        public ComboBoxSetting(string id, string description, string currentValue, string defaultValue, Dictionary<string, string> options) : base(id, description)
        {
            SelectedValue = currentValue;
            OldSelectedValue = currentValue;
            DefaultSelectedValue = defaultValue;
            Options = options;
        }

        public override bool HasChanges() => SelectedValue != OldSelectedValue;
        public override bool HasChangesDefaultSettings() => SelectedValue != DefaultSelectedValue;

        public override void ResetToOriginal() => SelectedValue = OldSelectedValue;
        public override void ResetToDefault() => SelectedValue = DefaultSelectedValue;
        public override void SaveCurrentState()
        {
            if (OldSelectedValue.Equals(SelectedValue))
                return;

            OldSelectedValue = SelectedValue;
        }

        public void ChangeComboboxValues(Dictionary<string, string> values)
        {
            Options = new Dictionary<string, string>(values);
        }
    }
}
