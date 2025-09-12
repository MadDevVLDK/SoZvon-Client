using Microsoft.Win32;

namespace SoZvon.UI.SubClasses
{
    public class ReesterWindows(string dbPath)
    {
        private readonly string DB_PATH = dbPath;
        private readonly object DB_lock = new();

        void Create_DB()
        {
            if(Registry.CurrentUser.OpenSubKey(DB_PATH, true) is null)
            {
                var DB_KEY = Registry.CurrentUser.CreateSubKey(DB_PATH, true);

                DB_KEY.SetValue("User_Login", "");
                DB_KEY.SetValue("User_Password", "");
                DB_KEY.SetValue("IP", "");

                DB_KEY.Close();
            }
        }
        public void GetDataReesterWindows(out string user_login, out string user_password, out string ip)
        {
            lock (DB_lock)
            {
                Create_DB();

                if (Registry.CurrentUser.OpenSubKey(DB_PATH, true) is RegistryKey DB_KEY)
                {
                    user_login = DB_KEY.GetValue("User_Login") as string ?? "";
                    user_password = DB_KEY.GetValue("User_Password") as string ?? "";
                    ip = DB_KEY.GetValue("IP") as string ?? "";

                    DB_KEY.Close();
                }
                else
                {
                    user_login = "";
                    user_password = "";
                    ip = "";
                }
            }
        }
        public void OnLogin(bool need_to_remember, string login, string password, string ip)
        {
            lock (DB_lock)
            {
                if(Registry.CurrentUser.OpenSubKey(DB_PATH, true) is not RegistryKey DB_KEY) return;

                if (need_to_remember)
                {
                    DB_KEY.SetValue("User_Login", login);
                    DB_KEY.SetValue("User_Password", password);
                    DB_KEY.SetValue("IP", ip);
                }
                else
                {
                    if (DB_KEY.GetValue("User_Login") is not null && DB_KEY.GetValue("User_Password") is not null)
                    {
                        DB_KEY.DeleteValue("User_Login");
                        DB_KEY.DeleteValue("User_Password");
                        DB_KEY.DeleteValue("IP");
                    }
                }

                DB_KEY?.Close();
            }
        }
    }
}
