using SoZvon.SubClasses;
using SoZvon.UI.Pages;
using SoZvon.UI.Room_Pages;
using System.Windows.Controls;
using System.Windows.Input;

namespace SoZvon.UI
{
    public interface IMainWindow
    {
        System.Windows.ResourceDictionary Resources { get; }
        StackPanel errorStackPanel_ref { get; }

        Frame mainFrame_ref { get; }
        Frame rightPanel_ref { get; }
        Frame leftPanel_ref { get; }

        LogInPage login_page { get; }
        RegisterPage register_page { get; }
        RoomPanelPage room_panel_page { get; }
        RoomPage room_page { get; }
        SettingsPage settings_page { get; }
        TitleSettingsPage titleSettings_page { get; }

        Task MakeAction_Form(Action action);
        Task<T> MakeAction_Form_Dispatcher<T>(Func<T> action);

        void AnyButton_UpMouse(object sender, MouseButtonEventArgs e);
        void AnyButton_DownMouse(object sender, MouseButtonEventArgs e);
        void AnyButton_EnterLeaveMouse(object sender, MouseEventArgs e);
        
        void DownloadFile(string filenameDownload, string savePath);
        void UploadFile(string filenameUpload);
        void GetInfoFile(string filenameUpload);
        void CanselOperation(string operationID);
        Grid CreateFilesContainer(List<My_FileInfo> fileInfos);

        void OnTextBoxMessages(string temp_reciever, string text, My_FileInfo[] file_pathes);
        void ShowPeopleTagsOnPanel(List<Room_User> room_Users, string text);
        void TextBoxTextChange(string text);
        void Make_NotifyMessage(string title, string text, int time = 2000);
        void Make_ErrorMessage(string title, string text, int time = 2000);
        void IsFocusable_TagTextblock(bool GotFocus, string text);

        void ReloadConnectionServ();
        void CloseApplication();
        void SelectMicrophoneByName(string name);

        void UpdateSetting<T>(string id, T value);
        void ChangeHasInvalidKeySetting(bool value);
        void TrySaveSettings();
        void TryResetToLastSettings();
        void TryResetToDefaultSettings();

        void MakeNotificationServer(TypeNotification typeNotification, Dictionary<string, object> dict);
        Grid? FindButtonGrid(string name_button, string tag_button = "");
        bool IsWindowFocused();

        void ChangeIP(string ip);
        void ResetButtonsAppearence();
    }
}
