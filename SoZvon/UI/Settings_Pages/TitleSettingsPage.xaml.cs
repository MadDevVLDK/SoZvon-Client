using System.Windows.Controls;

namespace SoZvon.UI.Room_Pages
{
    public partial class TitleSettingsPage : Page
    {
        IMainWindow mainWindow;
         
        // Стартовое состояние страницы
        public void StartProperties(IMainWindow mainWindow_)
        {
            mainWindow = mainWindow_;

            InitializeComponent();

            MainSettings.MouseUp += mainWindow.AnyButton_UpMouse;
            MainSettings.MouseDown += mainWindow.AnyButton_DownMouse;

            HotkeySettings.MouseUp += mainWindow.AnyButton_UpMouse;
            HotkeySettings.MouseDown += mainWindow.AnyButton_DownMouse;

            //AccountSettings.MouseUp += mainWindow.AnyButton_UpMouse;
            //AccountSettings.MouseDown += mainWindow.AnyButton_DownMouse;
        }
    }
}
