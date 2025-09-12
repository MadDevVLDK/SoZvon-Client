namespace SoZvon.Main_Thread
{
    enum InterfaceToSend : byte
    {
        IServerConnection,
        IManagerAPI,
        IApplicationUI,
        IVoiceManager
    }
    public enum ActionFromIUser : byte
    {
        ShowNotifyLog, // string text
        ShowErrorLog, // string text
        ShowErrorMessage, // string title, string message
        ShowNotifyMessage, // string title, string message

        OnStart,
        OnLogin,
        OnRegister, // string login, string password
        OnEnterRoom, // string room_name
        OnExitRoom,
        OnUserMessage, // Message message
        OnUserTexting, // Room_User user
        OnUserExitRoom, // string login
        OnUserEnterVoiceChat, // Room_User user
        OnUserExitVoiceChat, // Room_User user
        OnExitVoiceChat,
        OnEnterVoiceChat, // Room_User user
        OnSpeakingVoiceChat, // bool isSpeaking
        OnSendingMessage, // Message message
        OnSendingUserMessage, // My_FileInfo[] filesInfos
        OnSendingMessageTextBox, // string reciever, string text, My_FileInfo[] filesInfos
        OnIsConnectedChange, // bool value
        OnChangeIp, // string ip
        OnCloseApplication,

        //Show_MY_MessageOnScreen, // Guid guid, DateTime dateTime, string text, string reciever, string[] files_pathes
        Show_SERVER_MessageOnScreen, // Guid guid, DateTime dateTime, string text
        Show_USER_MessageOnScreen, // Guid guid, DateTime dateTime, string text, string sender, string[] files_pathes, MessageFromUser IsPublic = MessageFromUser.Public

        ShowUsersOnScreen, // List<Room_User> users
        ShowUserOnScreen, // Room_User user

        ShowRoomsOnScreen, // List<Room> rooms
        ShowRoomOnScreen, // Room room
        UpdateRoomOnScreen, // Room room
        DeleteRoomOnScreen, // string roomName

        NotificationOnReadyFileToDownload, // string file_name 
        NotificationOnFileLoadingToServer, // string file_name 

        ShowUsersTags, // List<Room_User> users, string text
        HideUsersTags,
        UpdateUsersTags, // List<Room_User> users, string text

        OnLoginButton, // string login, string password
        OnRegisterButtonLogPage,
        OnExitButtonRegPage,
        OnSettingsOpenButton,
        OnRoomNameButton, // List<Room> rooms, string active_room_button, string room_name_button_pressed
        OnGridTagsPeopleButton, // string grid_tags_people_name_pressed
        OnCloseErrorButton, // string tag_error

        MakeConnectionServerWithAction, // int timeout_millisecond, Action action
        ReloadConnectionServer,

        SetOperationId, // string id
        CancelOperation, //string operationID

        GetInfoFile, //
        UploadFile, //
        DownloadFile, //

        OnProgressHandler,
        OnFileInfoHandler,
        OnErrorHandler,
        OnUploadErrorHandler,

        OnSelectMicrophoneByName,
        GetMicrophonesInfo,
        OnMicrophonesInfo,
    }
    public enum ActionToIUser : byte
    {
        MessageErrorOccurred, // string title, string message
        MessageNotifyOccurred, // string title, string message
        LogNotifyOccurred, // string text, Color color
        LogErrorOccurred, // string text, Color color

        ServerNotifyOccured, // string title, string message
        ApplicationExit,
        MessageRecieved, // Message message
        IsConnectedChanged, // bool value
        ConnectionClosedVoiceChat,
        TagsTextChange, // string text
        UpdateIP, // string ip
        OnFocusTagTextblock, // bool GotFocus, string text
        OnSendingMessageTextBox, // string reciever, string text, My_FileInfo[] filesInfos
        GetInfoFile, //
        UploadFile, //
        DownloadFile, //
        SetOperationId, // string id
        CancelOperation, //string operationID

        OnProgressHandler,
        OnFileInfoHandler,
        OnErrorHandler,
        OnUploadErrorHandler,
        GetMicrophonesInfo,
        OnMicrophonesInfo,
        SelectMicrophoneByName,
        ReloadConnectionServ
    }
    record Action_IUser(ActionFromIUser Action, Dictionary<string, object> Params);
    record Action_Interfaces(ActionToIUser Action, Dictionary<string, object> Params);
}
