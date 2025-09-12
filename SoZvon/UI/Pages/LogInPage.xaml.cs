using System.Windows;
using System.Windows.Controls;

namespace SoZvon.UI.Pages
{
    public partial class LogInPage : Page
    {
        IMainWindow mainWindow;

        // Стартовое состояние страницы
        public void StartProperties(IMainWindow mainWindow_)
        {
            mainWindow = mainWindow_;

            InitializeComponent();

            TextBox_IP.TextChanged += Changed_IP_Text;
            Server_Info.MouseUp += (_, _) => My_Animations.Server_Info_Animation(this);
            
            Register_Button_LogPage.MouseUp += mainWindow.AnyButton_UpMouse;
            Register_Button_LogPage.MouseDown += mainWindow.AnyButton_DownMouse;
            Register_Button_LogPage.MouseEnter += mainWindow.AnyButton_EnterLeaveMouse;
            Register_Button_LogPage.MouseLeave += mainWindow.AnyButton_EnterLeaveMouse;

            Login_Button.MouseUp += mainWindow.AnyButton_UpMouse;
            Login_Button.MouseDown += mainWindow.AnyButton_DownMouse;
            Login_Button.MouseEnter += mainWindow.AnyButton_EnterLeaveMouse;
            Login_Button.MouseLeave += mainWindow.AnyButton_EnterLeaveMouse;

            Reload_Connection_Button.MouseUp += mainWindow.AnyButton_UpMouse;
            Reload_Connection_Button.MouseDown += mainWindow.AnyButton_DownMouse;
            Reload_Connection_Button.MouseEnter += mainWindow.AnyButton_EnterLeaveMouse;
            Reload_Connection_Button.MouseLeave += mainWindow.AnyButton_EnterLeaveMouse;
        }

        // Функция для отображения подсказки
        public void ChangedText_Page(object sender, TextChangedEventArgs e)
        {
            if (sender is not TextBox textBox)
                return;

            if (FindName(textBox.Name + "_Hint") is not TextBox hint_textBox)
                return;

            hint_textBox.Visibility = textBox.Text != "" ? Visibility.Collapsed : Visibility.Visible;
        }

        // Функция при изменении IP сервера
        public void Changed_IP_Text(object sender, TextChangedEventArgs e)
        {
            if (sender is not TextBox textBox) 
                return;

            string ip = textBox.Text == "" ? TextBox_IP_Hint.Text : textBox.Text;
            mainWindow.ChangeIP(ip);

            e.Handled = false;
        }
    }
}
