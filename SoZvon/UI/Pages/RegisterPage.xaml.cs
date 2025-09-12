using Visibility = System.Windows.Visibility;
using System.Windows.Controls;

namespace SoZvon.UI.Pages
{
    public partial class RegisterPage : Page
    {
        IMainWindow mainWindow;

        // Стартовое состояние страницы
        public void StartProperties(IMainWindow mainWindow_)
        {
            mainWindow = mainWindow_;

            InitializeComponent();

            Register_Button_RegPage.MouseUp += mainWindow.AnyButton_UpMouse;
            Register_Button_RegPage.MouseDown += mainWindow.AnyButton_DownMouse;
            Register_Button_RegPage.MouseEnter += mainWindow.AnyButton_EnterLeaveMouse;
            Register_Button_RegPage.MouseLeave += mainWindow.AnyButton_EnterLeaveMouse;

            Exit_Button_RegPage.MouseUp += mainWindow.AnyButton_UpMouse;
            Exit_Button_RegPage.MouseDown += mainWindow.AnyButton_DownMouse;
            Exit_Button_RegPage.MouseEnter += mainWindow.AnyButton_EnterLeaveMouse;
            Exit_Button_RegPage.MouseLeave += mainWindow.AnyButton_EnterLeaveMouse;
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
    }
}
