using System.Windows.Controls;

namespace SoZvon.UI.Room_Pages
{
    public partial class RoomPanelPage: Page
    {
        IMainWindow mainWindow;

        // Стартовое состояние страницы
        public void StartProperties(IMainWindow mainWindow_)
        {
            mainWindow = mainWindow_;

            InitializeComponent();

            Room_Button.MouseUp += mainWindow.AnyButton_UpMouse;
            Room_Button.MouseDown += mainWindow.AnyButton_DownMouse;
            Room_Button.MouseEnter += mainWindow.AnyButton_EnterLeaveMouse;
            Room_Button.MouseLeave += mainWindow.AnyButton_EnterLeaveMouse;

            Add_Room.MouseUp += mainWindow.AnyButton_UpMouse;
            Add_Room.MouseDown += mainWindow.AnyButton_DownMouse;
            Add_Room.MouseEnter += mainWindow.AnyButton_EnterLeaveMouse;
            Add_Room.MouseLeave += mainWindow.AnyButton_EnterLeaveMouse;

            Delete_Room.MouseUp += mainWindow.AnyButton_UpMouse;
            Delete_Room.MouseDown += mainWindow.AnyButton_DownMouse;
            Delete_Room.MouseEnter += mainWindow.AnyButton_EnterLeaveMouse;
            Delete_Room.MouseLeave += mainWindow.AnyButton_EnterLeaveMouse;
        }
    }
}
