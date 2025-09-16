using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SoZvon.ClientSettingsManager
{
    enum TypeSetting { Hotkey, ComboBox, CheckBox }

    record Setting(string Id, TypeSetting Type, string Value);
    internal class SettingsManager
    {
        readonly Main_Thread.IUser user;
        public SettingsManager(Main_Thread.IUser user)
        {
            this.user = user;

        }
    }
}
