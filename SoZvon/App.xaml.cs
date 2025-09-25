using System.Windows;

namespace SoZvon
{
    public partial class App : Application
    {
        public Main_Thread.My_User User { get; private set; } = null!;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            User = new Main_Thread.My_User();
        }
    }

}
