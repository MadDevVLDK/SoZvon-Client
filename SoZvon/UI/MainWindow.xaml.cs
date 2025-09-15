using SoZvon.SubClasses;
using SoZvon.UI.SubClasses;
using System;
using System.Drawing.Drawing2D;
using System.Threading.Channels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

using Action_IUser = SoZvon.Main_Thread.Action_IUser;
using ActionFromIUser = SoZvon.Main_Thread.ActionFromIUser;
using ActionToIUser = SoZvon.Main_Thread.ActionToIUser;
using Color = System.Windows.Media.Color;
using Colors = System.Windows.Media.Colors;
using SolidColorBrush = System.Windows.Media.SolidColorBrush;

namespace SoZvon.UI
{
    public partial class MainWindow
    {
        readonly Channel<Action_IUser> UserUI_Channel = Channel.CreateBounded<Action_IUser>(new BoundedChannelOptions(2000) { FullMode = BoundedChannelFullMode.Wait });
        readonly Dictionary<string, Color> users_colors = [];
        static readonly Color[] color_mass =
        [
            Color.FromRgb(0, 191, 255),   // Яркий синий (Deep Sky Blue) — отличная видимость
            Color.FromRgb(30, 144, 255),  // Dodger Blue — чуть темнее, но очень чёткий
            Color.FromRgb(255, 99, 71),   // Tomato — насыщенный красно-оранжевый
            Color.FromRgb(255, 20, 147),  // Deep Pink — яркий и контрастный
            Color.FromRgb(139, 0, 139),    // Dark Magenta — тёмный, но хорошо различимый
            Color.FromRgb(0, 255, 127),   // Spring Green — может сливаться при малом размере текста
            Color.FromRgb(64, 224, 208),  // Turquoise — хорош, но менее контрастен
            Color.FromRgb(255, 127, 80),  // Coral — светлее, чем Tomato
        ];

        async Task UserUI_Channel_Thread(CancellationToken cancellationToken)
        {
            try
            {
                await foreach (Action_IUser action_IUser in UserUI_Channel.Reader.ReadAllAsync(cancellationToken))
                {
                    try
                    {
                        Action action = InterpretateActionIUser(action_IUser);
                        MakeAction_Form(action);
                    }
                    catch (OperationCanceledException) { }
                    catch (My_Exception ex)
                    {
                        MakeAction_Form(() => Make_ErrorMessage(ex.Title ?? action_IUser.Action.ToString(), ex.Message));
                    }
                }
            }
            catch (OperationCanceledException) { return; }
        }
        Action InterpretateActionIUser(Action_IUser action_IUser)
        {
            Action action;

            var dict = action_IUser.Params;

            switch (action_IUser.Action)
            {
                case ActionFromIUser.ShowNotifyLog:
                    {
                        if (dict.Count != 1 || !dict.TryGetValue<string>("text", out var text))
                            throw new My_Exception("no valid params");

                        action = () => Change_Log_Text(text, Colors.Green);
                        break;
                    }
                case ActionFromIUser.ShowErrorLog:
                    {
                        if (dict.Count != 1 || !dict.TryGetValue<string>("text", out var text))
                            throw new My_Exception("no valid params");

                        action = () => Change_Log_Text(text, Colors.Red);
                        break;
                    }
                case ActionFromIUser.ShowErrorMessage:
                    {
                        if (dict.Count != 2 || !dict.TryGetValue<string>("title", out var title) || !dict.TryGetValue<string>("message", out var message))
                            throw new My_Exception("no valid params");

                        action = () => Make_ErrorMessage(title, message);
                        break;
                    }
                case ActionFromIUser.ShowNotifyMessage:
                    {
                        if (dict.Count != 2 || !dict.TryGetValue<string>("title", out var title) || !dict.TryGetValue<string>("message", out var message))
                            throw new My_Exception("no valid params");

                        action = () => Make_NotifyMessage(title, message);
                        break;
                    }
                case ActionFromIUser.OnStart:
                    {
                        if (dict.Count != 0)
                            throw new My_Exception("no valid params");

                        action = OnStart;
                        break;
                    }
                case ActionFromIUser.OnLogin:
                    {
                        if (dict.Count != 0)
                            throw new My_Exception("no valid params");

                        action = OnLogin;
                        break;
                    }
                case ActionFromIUser.OnRegister:
                    {
                        if (dict.Count != 2 || !dict.TryGetValue<string>("login", out var login) || !dict.TryGetValue<string>("password", out var password))
                            throw new My_Exception("no valid params");

                        action = () => OnRegister(login, password);
                        break;
                    }
                case ActionFromIUser.OnEnterRoom:
                    {
                        if (dict.Count != 1 || !dict.TryGetValue<string>("room_name", out var room_name))
                            throw new My_Exception("no valid params");

                        action = () => OnEnterRoom(room_name);
                        break;
                    }
                case ActionFromIUser.OnEnterVoiceChat:
                    {
                        if (dict.Count != 1 || !dict.TryGetValue<Room_User>("user", out var user))
                            throw new My_Exception("no valid params");

                        action = () =>
                        {
                            OnEnterExitVoiceChat(true);
                            UserVoiceChatAddToPanel(user);
                        };
                        break;
                    }
                case ActionFromIUser.OnExitRoom:
                    {
                        if (dict.Count != 0)
                            throw new My_Exception("no valid params");

                        action = OnExitRoom;
                        break;
                    }
                case ActionFromIUser.OnExitVoiceChat:
                    {
                        if (dict.Count != 0)
                            throw new My_Exception("no valid params");

                        action = () =>
                        {
                            OnSpeakingVoiceChat(false);
                            OnEnterExitVoiceChat(false);
                        };
                        break;
                    }
                case ActionFromIUser.OnSpeakingVoiceChat:
                    {
                        if (dict.Count != 1 || !dict.TryGetValue<bool>("isSpeaking", out var isSpeaking))
                            throw new My_Exception("no valid params");

                        action = () => OnSpeakingVoiceChat(isSpeaking);
                        break;
                    }
                case ActionFromIUser.OnUserMessage:
                    {
                        if (dict.Count != 1 || !dict.TryGetValue<Message>("message", out var message))
                            throw new My_Exception("no valid params");

                        action = () => OnUserMessages(message);
                        break;
                    }
                case ActionFromIUser.OnUserTexting:
                    {
                        if (dict.Count != 1 || !dict.TryGetValue<Room_User>("user", out var user))
                            throw new My_Exception("no valid params");

                        action = () => my_Actions.UserTexting(user);
                        break;
                    }
                case ActionFromIUser.OnUserExitRoom:
                    {
                        if (dict.Count != 1 || !dict.TryGetValue<string>("login", out var login))
                            throw new My_Exception("no valid params");

                        action = () =>
                        {
                            UserDeleteOnPanel(login);
                            UserVoiceChatDeleteOnPanel(login);
                        };
                        break;
                    }
                case ActionFromIUser.OnUserEnterVoiceChat:
                    {
                        if (dict.Count != 1 || !dict.TryGetValue<Room_User>("user", out var user))
                            throw new My_Exception("no valid params");

                        action = () => UserVoiceChatAddToPanel(user);
                        break;
                    }
                case ActionFromIUser.OnUserExitVoiceChat:
                    {
                        if (dict.Count != 1 || !dict.TryGetValue<Room_User>("user", out var user))
                            throw new My_Exception("no valid params");

                        action = () => UserVoiceChatDeleteOnPanel(user.Login);
                        break;
                    }
                case ActionFromIUser.OnSendingUserMessage:
                    {
                        if (dict.Count != 5 || !dict.TryGetValue<DateTime>("dateTime", out var dateTime) || !dict.TryGetValue<Guid>("guid", out var guid))
                            throw new My_Exception("no valid params");

                        if (!dict.TryGetValue<string>("text", out var text) || !dict.TryGetValue<string>("reciever", out var reciever) || !dict.TryGetValue<My_FileInfo[]>("filesInfos", out var filesInfos))
                            throw new My_Exception("no valid params");

                        action = () =>
                        {
                            Show_MY_MessageOnScreen(dateTime, guid, text, reciever, filesInfos);
                            
                            if(dateTime == DateTime.MinValue)
                                room_page.On_Sending_Text(filesInfos);
                        };
                        break;
                    }
                case ActionFromIUser.Show_SERVER_MessageOnScreen:
                    {
                        if (dict.Count != 3 || !dict.TryGetValue<DateTime>("date", out var date) || !dict.TryGetValue<Guid>("guid", out var guid))
                            throw new My_Exception("no valid params");

                        if (!dict.TryGetValue<string>("text", out var text))
                            throw new My_Exception("no valid params");

                        action = () => Show_SERVER_MessageOnScreen(guid, date, text);
                        break;
                    }
                case ActionFromIUser.Show_USER_MessageOnScreen:
                    {
                        if (dict.Count != 6 || !dict.TryGetValue<DateTime>("dateTime", out var dateTime) || !dict.TryGetValue<Guid>("guid", out var guid))
                            throw new My_Exception("no valid params");

                        if (!dict.TryGetValue<string>("text", out var text) || !dict.TryGetValue<string>("sender", out var sender))
                            throw new My_Exception("no valid params");

                        if (!dict.TryGetValue<My_FileInfo[]>("filesInfos", out var filesInfos) || !dict.TryGetValue<MessageFromUser>("IsPublic", out var IsPublic))
                            throw new My_Exception("no valid params");

                        if (!users_colors.TryGetValue(sender, out Color login_color))
                        {
                            login_color = color_mass[new Random().Next(0, color_mass.Length)];
                            users_colors.Add(sender, login_color);
                        }
                        
                        action = () => Show_CLIENT_MessageOnScreen(dateTime, guid, login_color, text, sender, filesInfos, IsPublic);
                        break;
                    }
                case ActionFromIUser.ShowUserOnScreen:
                    {
                        if (dict.Count != 1 || !dict.TryGetValue<Room_User>("user", out var user))
                            throw new My_Exception("no valid params");

                        action = () => UserAddToPanel(user);
                        break;
                    }
                case ActionFromIUser.ShowUsersOnScreen:
                    {
                        if (dict.Count != 1 || !dict.TryGetValue<List<Room_User>>("users", out var users))
                            throw new My_Exception("no valid params");

                        action = () =>
                        {
                            UsersAddToPanel(users);
                            ShowPeopleTagsOnPanel(users, "");
                        };
                        break;
                    }
                case ActionFromIUser.ShowRoomsOnScreen:
                    {
                        if (dict.Count != 1 || !dict.TryGetValue<List<Room>>("rooms", out var rooms))
                            throw new My_Exception("no valid params");

                        action = () => RoomsAddToPanel(rooms);
                        break;
                    }
                case ActionFromIUser.ShowRoomOnScreen:
                    {
                        if (dict.Count != 1 || !dict.TryGetValue<Room>("room", out var room))
                            throw new My_Exception("no valid params");
                        
                        action = () => RoomAddToPanel(room);
                        break;
                    }
                case ActionFromIUser.UpdateRoomOnScreen:
                    {
                        if (dict.Count != 1 || !dict.TryGetValue<Room>("room", out var room))
                            throw new My_Exception("no valid params");

                        action = () => RoomChangeOnPanel(room);
                        break;
                    }
                case ActionFromIUser.DeleteRoomOnScreen:
                    {
                        if (dict.Count != 1 || !dict.TryGetValue<string>("roomName", out var roomName))
                            throw new My_Exception("no valid params");

                        action = () => RoomDeleteOnPanel(roomName);
                        break;
                    }
                case ActionFromIUser.NotificationOnReadyFileToDownload:
                    {
                        if (dict.Count != 1 || !dict.TryGetValue<string>("file_name", out var file_name))
                            throw new My_Exception("no valid params");

                        action = () => filesManager.CallOnReadyFileToDownload(file_name);
                        break;
                    }
                case ActionFromIUser.NotificationOnFileLoadingToServer:
                    {
                        if (dict.Count != 1 || !dict.TryGetValue<string>("file_name", out var file_name))
                            throw new My_Exception("no valid params");

                        action = () => filesManager.CallOnFileLoadingToServer(file_name);
                        break;
                    }
                case ActionFromIUser.ShowUsersTags:
                    {
                        if (dict.Count != 2 || !dict.TryGetValue<List<Room_User>>("users", out var users) || !dict.TryGetValue<string>("text", out var text))
                            throw new My_Exception("no valid params");

                        action = () =>
                        {
                            ChangeVisibility_Grid_PeopleTags(Visibility.Visible);
                            ShowPeopleTagsOnPanel(users, text);
                        };
                        break;
                    }
                case ActionFromIUser.HideUsersTags:
                    {
                        if (dict.Count != 0)
                            throw new My_Exception("no valid params");

                        action = () => ChangeVisibility_Grid_PeopleTags(Visibility.Collapsed);
                        break;
                    }
                case ActionFromIUser.UpdateUsersTags:
                    {
                        if (dict.Count != 2 || !dict.TryGetValue<List<Room_User>>("users", out var users) || !dict.TryGetValue<string>("text", out var text))
                            throw new My_Exception("no valid params");

                        action = () => ShowPeopleTagsOnPanel(users, text);
                        break;
                    }
                case ActionFromIUser.OnLoginButton:
                    {
                        if (dict.Count != 2 || !dict.TryGetValue<string>("login", out var login) || !dict.TryGetValue<string>("password", out var password))
                            throw new My_Exception("no valid params");

                        action = () => On_Login_Button(login, password);
                        break;
                    }
                case ActionFromIUser.OnRegisterButtonLogPage:
                    {
                        if (dict.Count != 0)
                            throw new My_Exception("no valid params");

                        action = On_Register_Button_LogPage;
                        break;
                    }
                case ActionFromIUser.OnExitButtonRegPage:
                    {
                        if (dict.Count != 0)
                            throw new My_Exception("no valid params");

                        action = On_Exit_Button_RegPage;
                        break;
                    }
                case ActionFromIUser.OnSettingsOpenButton:
                    {
                        if (dict.Count != 0)
                            throw new My_Exception("no valid params");

                        action = On_SettingsOpen_Button;
                        break;
                    }
                case ActionFromIUser.OnRoomNameButton:
                    {
                        if (dict.Count != 2 ||  !dict.TryGetValue<string>("active_room_button", out var active_room_button))
                            throw new My_Exception("no valid params");

                        if (!dict.TryGetValue<string>("room_name_button_pressed", out var room_name_button_pressed))
                            throw new My_Exception("no valid params");

                        action = () => On_Room_Name_Button(active_room_button, room_name_button_pressed);
                        break;
                    }
                case ActionFromIUser.OnGridTagsPeopleButton:
                    {
                        if (dict.Count != 1 || !dict.TryGetValue<string>("grid_tags_people_name_pressed", out var grid_tags_people_name_pressed))
                            throw new My_Exception("no valid params");

                        action = () => On_Grid_Tags_People_Button(grid_tags_people_name_pressed);
                        break;
                    }
                case ActionFromIUser.OnCloseErrorButton:
                    {
                        if (dict.Count != 1 || !dict.TryGetValue<string>("tag_error", out var tag_error))
                            throw new My_Exception("no valid params");

                        action = () => notifyMsgManager.CloseNotifyWithTag(tag_error);
                        break;
                    }
                case ActionFromIUser.OnIsConnectedChange:
                    {
                        if (dict.Count != 1 || !dict.TryGetValue<bool>("value", out var value))
                            throw new My_Exception("no valid params");

                        action = () =>
                        {
                            if (!value)
                            {
                                my_Actions.Navigate_MainFrame_To(Page_Type.LogInPage);

                                OnExitRoom();

                                my_Actions.DeleteAll();
                            }
                        };
                        break;
                    }
                case ActionFromIUser.SetOperationId:
                    {
                        if (dict.Count != 2 || !dict.TryGetValue<string>("fileName", out var fileName) || !dict.TryGetValue<string>("id", out var id))
                            throw new My_Exception("no valid params");

                        action = () => filesManager.SetOperationID(fileName, id);
                        break;
                    }
                case ActionFromIUser.OnProgressHandler:
                    {
                        if (dict.Count != 3 || !dict.TryGetValue<string>("fileName", out var fileName) || !dict.TryGetValue<int>("percent", out var percent))
                            throw new My_Exception("no valid params");

                        if (!dict.TryGetValue<long>("fileSize", out var fileSize))
                            throw new My_Exception("no valid params");

                        action = () => filesManager.OnProgressHandler(fileName, percent, fileSize);
                        break;
                    }
                case ActionFromIUser.OnFileInfoHandler:
                    {
                        if (dict.Count != 2 || !dict.TryGetValue<string>("fileName", out var fileName) || !dict.TryGetValue<long>("fileSize", out var fileSize))
                            throw new My_Exception("no valid params");

                        action = () => filesManager.OnFileInfoHandler(fileName, fileSize);
                        break;
                    }
                case ActionFromIUser.OnErrorHandler:
                    {
                        if (dict.Count != 2 || !dict.TryGetValue<string>("fileName", out var fileName) || !dict.TryGetValue<string>("text", out var text))
                            throw new My_Exception("no valid params");

                        action = () => filesManager.OnErrorHandler(fileName, text);
                        break;
                    }
                case ActionFromIUser.OnUploadErrorHandler:
                    {
                        if (dict.Count != 2 || !dict.TryGetValue<string>("fileName", out var fileName) || !dict.TryGetValue<string>("text", out var text))
                            throw new My_Exception("no valid params");

                        action = () => filesManager.OnUploadErrorHandler(fileName, text);
                        break;
                    }
                case ActionFromIUser.OnMicrophonesInfo:
                    {
                        if (dict.Count != 1 || !dict.TryGetValue<Dictionary<string, string>>("microphones", out var microphones))
                            throw new My_Exception("no valid params");

                        action = () => settings_page.OnMicrophonesInfo(microphones);
                        break;
                    }
                default: 
                    throw new My_Exception("no valid ActionFromIUser");
            }

            return action;
        }
        public async void OnIUserAction(ActionFromIUser action_IUser, Dictionary<string, object> dict) => await UserUI_Channel.Writer.WriteAsync(new(action_IUser, dict));
    }
    public partial class MainWindow : Window, IApplicationUI, IMainWindow
    {
        public Main_Thread.IUser User { get; }

        readonly CancellationTokenSource cts = new();
        readonly Channel<Action> form_current_actions_channel = Channel.CreateBounded<Action>(new BoundedChannelOptions(2000) { FullMode = BoundedChannelFullMode.Wait });

        public StackPanel errorStackPanel_ref { get; private set; } = null!;

        public Frame mainFrame_ref { get; private set; } = null!;
        public Frame rightPanel_ref { get; private set; } = null!;
        public Frame leftPanel_ref { get; private set; } = null!;

        public Pages.LogInPage login_page { get; private set; } = null!;
        public Pages.RegisterPage register_page { get; private set; } = null!;
        public Room_Pages.RoomPanelPage room_panel_page { get; private set; } = null!;
        public Room_Pages.RoomPage room_page { get; private set; } = null!;
        public Room_Pages.SettingsPage settings_page { get; private set; } = null!;
        public Room_Pages.TitleSettingsPage titleSettings_page { get; private set; } = null!;

        My_Buttons my_Buttons = null!;
        My_Actions my_Actions = null!;
        NotifyMsgManager notifyMsgManager = null!;
        ReesterWindows reesterWindows = null!;
        My_Timer buttonTimer = null!;
        FilesManager filesManager = null!;

        public MainWindow(Main_Thread.IUser user_)
        {
            User = user_;

            Visibility = Visibility.Hidden;
            WindowState = WindowState.Maximized;
            WindowStyle = WindowStyle.SingleBorderWindow;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;

            _ = Main_Thread(cts.Token);
            _ = Pressing_Button_Thread(cts.Token);
            _ = UserUI_Channel_Thread(cts.Token);

            InitializeComponent();

            InitializeSubClasses();
        }

        public void OnStart()
        {
            MakeAction_Form(() => {
                Visibility = Visibility.Visible;
                GetDataReesterWindows();
            });
        }

        void InitializeSubClasses()
        {
            errorStackPanel_ref = Error_StackPanel;
            mainFrame_ref = MainFrame;
            rightPanel_ref = RightPanel;
            leftPanel_ref = LeftPanel;

            login_page = new();
            register_page = new();
            room_panel_page = new();
            room_page = new();
            settings_page = new();
            titleSettings_page = new();

            my_Buttons = new(this);
            notifyMsgManager = new(this);
            my_Actions = new(this);
            reesterWindows = new(@"Software\SoZvon");
            buttonTimer = new(0.35);
            filesManager = new(this);

            login_page.StartProperties(this);
            register_page.StartProperties(this);
            room_panel_page.StartProperties(this);
            room_page.StartProperties(this);
            settings_page.StartProperties(this);
            titleSettings_page.StartProperties(this);

            this.StartProperties();
        }
        void StartProperties()
        {
            my_Actions.Navigate_MainFrame_To(Page_Type.LogInPage);
            my_Actions.Navigate_LeftPanel_To(Page_Type.RoomPanelPage);
            my_Actions.Navigate_RightPanel_To(Page_Type.RoomPage);

            Textbox_PrivateMsg_IsEnabled(false);
            Textbox_IsEnabled(false);

            ChangeVisibility_Grid_PeopleTags(Visibility.Collapsed);

            MinWidth = Min_RightPanel_Size + offset_margin_MainGrid_Room + Min_LeftPanel_Size;
            MinHeight = 660;

            SizeChanged += MainWindow_SizeChanged;
            MouseLeftButtonUp += (sender, e) => { my_Buttons.Set_Active_Button(""); };
            buttonTimer.SetAcionOnTick(() => my_Buttons.CanPressButton = true);
        }
        void GetDataReesterWindows()
        {
            reesterWindows.GetDataReesterWindows(out string login, out string password, out string ip);

            bool login_password_valid = login is not null && password is not null;
            bool ip_valid = ip is not null;

            if (login_password_valid)
            {
                login_page.TextBox_Login.Text = login;
                login_page.TextBox_Password.Text = password;
            }

            if (ip_valid)
            {
                login_page.TextBox_IP.Text = ip;
            }

            login_page.Remember_Me_Sign.IsChecked = login_password_valid || ip_valid;
        }

        async Task Main_Thread(CancellationToken cancellationToken)
        {
            try
            {
                await foreach (Action action in form_current_actions_channel.Reader.ReadAllAsync(cancellationToken))
                {
                    try
                    {
                        Dispatcher.Invoke(System.Windows.Threading.DispatcherPriority.Render, action);
                    }
                    catch (OperationCanceledException) { }
                    catch (Exception ex)
                    {
                        Make_ErrorMessage("Error_Main_Thread_My_User", ex.Message.ToString());
                    }
                }
            }
            catch (OperationCanceledException) { return; }
        }
        public async void MakeAction_Form(Action action) => await form_current_actions_channel.Writer.WriteAsync(action);
        public async Task<T> MakeAction_Form_Dispatcher<T>(Func<T> action) => await Dispatcher.InvokeAsync(action);        

        public void OnLogin()
        {
            my_Buttons.Set_Active_Button("");
            my_Actions.Navigate_MainFrame_To(Page_Type.None);
        }
        public void OnRegister(string login, string password)
        {
            my_Buttons.Set_Active_Button("");
            my_Actions.Navigate_MainFrame_To(Page_Type.LogInPage);
            login_page.TextBox_Login.Text = login;
            login_page.TextBox_Password.Text = password;

            Make_NotifyMessage("Register_Verification", "Подтвердите свою почту, пройдя по ссылке в отправленном письме");
        }
        public void OnEnterRoom(string room_name)
        {
            room_page.OnEnterRoom(room_name);

            my_Buttons.Fast_Button_Appearence_Change("Room_Button", Button_Color_Type.Light, true);
        }
        public void OnExitRoom()
        {
            room_page.OnExitRoom();
            my_Actions.OnRoomExit();

            my_Buttons.Fast_Button_Appearence_Change("Room_Button", Button_Color_Type.Light, false);

            OnEnterExitVoiceChat(false);
            OnSpeakingVoiceChat(false);

            filesManager.ClearFilesList();
        }
        public void OnUserMessages(Message message) => room_page.OnUserMessages(message);

        public void OnEnterExitVoiceChat(bool IsEntering) => my_Buttons.Fast_Button_Appearence_Change("Join_VoiceChat_Button", Button_Color_Type.Light, IsEntering);
        public void OnSpeakingVoiceChat(bool IsSpeaking) => my_Buttons.Fast_Button_Appearence_Change("Speak_Button", Button_Color_Type.Light, IsSpeaking);
        public void OnTextBoxMessages(string reciever, string text, My_FileInfo[] filesInfos) => User.OnInterfacesAction(ActionToIUser.OnSendingMessageTextBox, new() {
            ["reciever"] = reciever,
            ["text"] = text,
            ["filesInfos"] = filesInfos
        });
        public void MakeNotificationServer(TypeNotification typeNotification, Dictionary<string, object> dict) => User.OnInterfacesAction(ActionToIUser.ServerNotifyOccured, new() {
            ["notification"] = new NotificationServer(typeNotification, dict) 
        });

        public void DownloadFile(string filename, string saveFolder) => User.OnInterfacesAction(ActionToIUser.DownloadFile, new() {
            ["filename"] = filename,
            ["saveFolder"] = saveFolder
        });
        public void UploadFile(string filename) => User.OnInterfacesAction(ActionToIUser.UploadFile, new() {
            ["filename"] = filename
        });
        public void GetInfoFile(string filename) => User.OnInterfacesAction(ActionToIUser.GetInfoFile, new() {
            ["filename"] = filename
        });
        public void CanselOperation(string operationID) => User.OnInterfacesAction(ActionToIUser.CancelOperation, new() {
            ["operationID"] = operationID 
        });

        public Grid CreateFilesContainer(List<My_FileInfo> fileInfos) => filesManager.CreateContainer(fileInfos);

        public void ShowPeopleTagsOnPanel(List<Room_User> room_Users, string text) => my_Actions.ShowPeopleTagsOnPanel(room_Users, text);
        public void UsersAddToPanel(List<Room_User> room_Users) => my_Actions.UsersAddToPanel(room_Users);
        public void UserAddToPanel(Room_User room_user) => my_Actions.UserAddToPanel(room_user);
        public void UserDeleteOnPanel(string id) => my_Actions.UserDeleteOnPanel(id);
        public void UserVoiceChatAddToPanel(Room_User room_user) => my_Actions.UserVoiceChatAddToPanel(room_user);
        public void UserVoiceChatDeleteOnPanel(string id) => my_Actions.UserVoiceChatDeleteOnPanel(id);

        public void RoomsAddToPanel(List<Room> rooms) => my_Actions.RoomsAddToPanel(rooms);
        public void RoomAddToPanel(Room room) => my_Actions.RoomAddToPanel(room);
        public void RoomChangeOnPanel(Room room) => my_Actions.RoomChangeOnPanel(room);
        public void RoomDeleteOnPanel(string id) => my_Actions.RoomDeleteOnPanel(id);

        public void Show_CLIENT_MessageOnScreen(DateTime dateTime, Guid guid, Color login_color, string text, string sender, My_FileInfo[] filesInfos, MessageFromUser IsPublic = MessageFromUser.Public) => my_Actions.Show_CLIENT_MessageOnScreen(dateTime, guid, login_color, text, sender, filesInfos, IsPublic);
        public void Show_SERVER_MessageOnScreen(Guid guid, DateTime date, string text) => my_Actions.Show_SERVER_MessageOnScreen(guid, date, text);
        public void Show_MY_MessageOnScreen(DateTime date, Guid guid, string text, string reciever, My_FileInfo[] image_path) => my_Actions.Show_MY_MessageOnScreen(date, guid, text, reciever, image_path);

        public void Make_ErrorMessage(string title, string text, int time = 2000) => notifyMsgManager.New_NotifyMessage(title, text, Color.FromRgb(255, 0, 0), time);
        public void Make_NotifyMessage(string title, string text, int time = 2000) => notifyMsgManager.New_NotifyMessage(title, text, Color.FromRgb(0, 200, 0), time);
        public void IsFocusable_TagTextblock(object sender, bool GotFocus, bool IsEntered_TagsPeople_TextBox_Grid, bool TabPressed)
        {
            if (!GotFocus)
            {
                bool canProcessHiding = (sender is TextBox or Grid) && (!IsEntered_TagsPeople_TextBox_Grid || TabPressed);

                if (!canProcessHiding) 
                    return;
            }
            else if (!(GotFocus && sender is TextBox && room_page.Grid_Users_Tags.Visibility == Visibility.Collapsed))
            {
                return;
            }

            string text = room_page.Textbox_PrivateMsg.Text;

            User.OnInterfacesAction(ActionToIUser.OnFocusTagTextblock, new() {
                ["GotFocus"] = GotFocus,
                ["text"] = text
            });
        }
        public void ChangeVisibility_Grid_PeopleTags(Visibility visibility) => my_Actions.ChangeVisibility_Grid_PeopleTags(visibility);

        public void SelectMicrophoneByName(string name) => User.OnInterfacesAction(ActionToIUser.SelectMicrophoneByName, new() {
            ["microphone"] = name
        });
        public void ReloadConnectionServ() => User.OnInterfacesAction(ActionToIUser.ReloadConnectionServer, []);
        public void CloseApplication() => Application.Current.Shutdown();

        public void Change_Log_Text(string text, Color color)
        {
            if (Log_Grid.FindElementByTag<TextBlock>("Text") is not TextBlock textBlock) 
                return;

            textBlock.Text = text;
            textBlock.Foreground = new SolidColorBrush(color);
        }
        public void Textbox_PrivateMsg_IsEnabled(bool state) => room_page.Textbox_PrivateMsg_IsEnabled(state);
        public void Textbox_IsEnabled(bool state) => room_page.Textbox_IsEnabled(state);
        public void Chatting_Textbox_SetText(string text) => room_page.Chatting_RichTextBox_SetText(text);

        public void TextBoxTextChange(string text) => User.OnInterfacesAction(ActionToIUser.TagsTextChange, new() {
            ["text"] = text}
        );
        public void ChangeIP(string ip) => User.OnInterfacesAction(ActionToIUser.UpdateIP, new() {
            ["ip"] = ip 
        });
        
    }
    public partial class MainWindow : Window
    {
        const double offset_margin_MainGrid_Room = 47;
        const int Min_RightPanel_Size = 805;
        const int Min_LeftPanel_Size = 190;
        double LeftPanel_percentage = 0.2;

        void MainWindow_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            double totalAvailableWidth = e.NewSize.Width - offset_margin_MainGrid_Room;
            double newWidth_RoomPanel = Math.Max(LeftPanel_percentage * totalAvailableWidth, Min_LeftPanel_Size);

            if (totalAvailableWidth - newWidth_RoomPanel < Min_RightPanel_Size)
            {
                newWidth_RoomPanel = Math.Max(totalAvailableWidth - Min_RightPanel_Size, Min_LeftPanel_Size);
            }

            MainGrid_RightPanel.Margin = new Thickness(newWidth_RoomPanel + offset_margin_MainGrid_Room, MainGrid_RightPanel.Margin.Top, MainGrid_RightPanel.Margin.Right, MainGrid_RightPanel.Margin.Bottom);
            LeftPanel.Width = newWidth_RoomPanel;

            room_page.ProcessTextChanged();
        }
        void OnDrag_RightLeftPanelThumb(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e)
        {
            double newWidth_RoomPanel = LeftPanel.ActualWidth + e.HorizontalChange;
            double newWidth_RightPanel = MainGrid_RightPanel.ActualWidth - e.HorizontalChange;

            if (newWidth_RoomPanel < Min_LeftPanel_Size || newWidth_RightPanel < Min_RightPanel_Size) 
                return;
            else if (newWidth_RoomPanel > (LeftPanel.ActualWidth + MainGrid_RightPanel.ActualWidth) - Min_RightPanel_Size)
                newWidth_RoomPanel = (LeftPanel.ActualWidth + MainGrid_RightPanel.ActualWidth) - Min_RightPanel_Size;

            LeftPanel_percentage = Math.Round(newWidth_RoomPanel / (newWidth_RoomPanel + newWidth_RightPanel), 4);

            MainGrid_RightPanel.Margin = new Thickness(newWidth_RoomPanel + offset_margin_MainGrid_Room, MainGrid_RightPanel.Margin.Top, MainGrid_RightPanel.Margin.Right, MainGrid_RightPanel.Margin.Bottom);
            LeftPanel.Width = newWidth_RoomPanel;
        }

        public bool IsWindowFocused() => IsActive && IsLoaded && Visibility is Visibility.Visible;
        void FormClosing(object sender, EventArgs e)
        {
            User.OnInterfacesAction(ActionToIUser.ApplicationExit, []);
            cts.Cancel();
        }
    }

    // ФУНКЦИИ СВЯЗАННЫЕ С КНОПКАМИ
    public partial class MainWindow : Window
    {
        readonly Channel<Action> pressing_button_channel = Channel.CreateBounded<Action>(new BoundedChannelOptions(500) { FullMode = BoundedChannelFullMode.Wait });

        async Task Pressing_Button_Thread(CancellationToken cancellationToken)
        {
            try
            {
                await foreach (Action action in pressing_button_channel.Reader.ReadAllAsync(cancellationToken))
                {
                    if (!my_Buttons.CanPressButton) 
                        continue;

                    my_Buttons.CanPressButton = false;

                    try
                    {
                        MakeAction_Form(action);
                    }
                    catch (OperationCanceledException) { }
                    catch (Exception ex)
                    {
                        Make_ErrorMessage("Error_Pressing_Button_Thread_UI", ex.Message.ToString());
                    }

                    buttonTimer.Reset();
                }
            }
            catch (OperationCanceledException) { return; }
        }

        public async void AnyButton_UpMouse(object sender, MouseButtonEventArgs e)
        {
            if (sender is not Grid button) 
                return;

            void action() {
                string active_button_room = my_Buttons.Get_Active_Button_Room();
                string pressed_button = button.Name;

                Dictionary<string, object> dict = [];

                if (button.Tag is string tag)
                {
                    switch (pressed_button)
                    {
                        case "Room_Name_Button":
                            {
                                dict.Add("active_room_button", active_button_room);
                                dict.Add("room_name_button_pressed", tag);

                                break;
                            }
                        case "Grid_Tags_People_Button":
                            {
                                dict.Add("grid_tags_people_name_pressed", tag);
                                break;
                            }
                        case "Close_Error":
                            {
                                dict.Add("name_error", tag);
                                break;
                            }
                    }
                }
                else
                {
                    string active_button = my_Buttons.Get_Active_Button();

                    if (active_button != pressed_button && active_button != "")
                        return;

                    my_Buttons.Fast_Button_Appearence_Change(pressed_button, Button_Color_Type.Medium);

                    switch (pressed_button)
                    {
                        case "Login_Button":
                            {
                                dict.Add("login", login_page.TextBox_Login.Text);
                                dict.Add("password", login_page.TextBox_Password.Text);
                                break;
                            }
                        case "Register_Button_RegPage":
                            {
                                dict.Add("login", register_page.TextBox_Login.Text);
                                dict.Add("password", register_page.TextBox_Password.Text);
                                dict.Add("name", register_page.TextBox_Name.Text);
                                dict.Add("email", register_page.TextBox_Email.Text);
                                break;
                            }
                        case "Room_Button":
                            {
                                my_Buttons.Get_Button_State("Room_Button", out bool button_state);

                                dict.Add("active_button_room", active_button_room);
                                dict.Add("button_state", button_state);
                                break;
                            }
                        case "Add_Room":
                            {
                                string name = Microsoft.VisualBasic.Interaction.InputBox("name");
                                dict.Add("room_name", name);
                                break;
                            }
                        case "Delete_Room":
                            {
                                dict.Add("room_name", active_button_room);
                                break;
                            }
                        case "Join_VoiceChat_Button":
                            {
                                my_Buttons.Get_Button_State("Join_VoiceChat_Button", out bool Join_VoiceChat_Button_state);

                                dict.Add("Join_VoiceChat_Button_state", Join_VoiceChat_Button_state);
                                break;
                            }
                        case "Speak_Button" or "Join_VoiceChat_Button":
                            {
                                my_Buttons.Get_Button_State("Speak_Button", out bool Speak_Button_state);
                                my_Buttons.Get_Button_State("Join_VoiceChat_Button", out bool Join_VoiceChat_Button_state);

                                dict.Add("Speak_Button_state", Speak_Button_state);
                                dict.Add("Join_VoiceChat_Button_state", Join_VoiceChat_Button_state);
                                break;
                            }
                        case "MainSettings":
                            {

                                break;
                            }
                    }
                }

                User.On_Button_Clicked(pressed_button, dict);
            }

            await pressing_button_channel.Writer.WriteAsync(action);
        }
        public void AnyButton_DownMouse(object sender, MouseButtonEventArgs e)
        {
            if (sender is not Grid button)
                return;

            void action() {
                string pressed_button = button.Name;

                if (button.Tag is string my_tag)
                {
                    Button_Color_Type color_Type;

                    switch (pressed_button)
                    {
                        case "Room_Name_Button":
                            {
                                if (!my_Buttons.Get_Button_State("Room_Button", out bool state) || state)
                                    return;

                                foreach (Grid element in room_panel_page.All_Rooms.Children)
                                {
                                    color_Type = Button_Color_Type.Light;

                                    if (element.Tag is not string tag)
                                        continue;

                                    if (my_tag == tag)
                                        color_Type = Button_Color_Type.Strong;

                                    my_Buttons.Fast_Button_Appearence_Change(pressed_button, color_Type, tag);
                                }
                                break;
                            }
                        case "Grid_Tags_People_Button":
                            {
                                foreach (Grid element in room_page.Panel_Users_Tags.Children)
                                {
                                    color_Type = Button_Color_Type.Light;

                                    if (element.Tag is not string tag)
                                        continue;

                                    if (my_tag == tag)
                                        color_Type = Button_Color_Type.Strong;

                                    my_Buttons.Fast_Button_Appearence_Change(pressed_button, color_Type, tag);
                                }
                                break;
                            }
                        case "Close_Error":
                            {
                                my_Buttons.Fast_Button_Appearence_Change(pressed_button, Button_Color_Type.Strong, my_tag);
                                break;
                            }
                        default: return;
                    }
                }
                else
                {
                    string active_button = my_Buttons.Get_Active_Button();

                    if (active_button != pressed_button && active_button != "")
                        return;

                    my_Buttons.Set_Active_Button(pressed_button);
                    my_Buttons.Fast_Button_Appearence_Change(pressed_button, Button_Color_Type.Strong);
                }
            }

            MakeAction_Form(action);
        }
        public void AnyButton_EnterLeaveMouse(object sender, MouseEventArgs e)
        {
            if (sender is not Grid button) 
                return;

            void action()
            {
                Button_Color_Type color_Type = Button_Color_Type.Light;
                string active_button = my_Buttons.Get_Active_Button();
                string pressed_button = button.Name;

                string tag = "";

                if (button.Tag is string _tag)
                {
                    tag = _tag;

                    if (e.RoutedEvent.Name == MouseEnterEvent.Name)
                    {
                        color_Type = Button_Color_Type.Medium;

                        switch (pressed_button)
                        {
                            case "Room_Name_Button":
                                {
                                    string active_button_room = my_Buttons.Get_Active_Button_Room();

                                    my_Buttons.Get_Button_State("Room_Button", out bool state);

                                    if (tag != active_button_room && state)
                                        color_Type = Button_Color_Type.Light;
                                    else if (tag == active_button_room)
                                        color_Type = Button_Color_Type.Strong;

                                    break;
                                }
                            case "Grid_Tags_People_Button":
                                {
                                    if (active_button == tag)
                                        color_Type = Button_Color_Type.Strong;

                                    break;
                                }
                            case "Close_Error": 
                                break;
                            default: 
                                return;
                        }
                    }
                    else
                    {
                        color_Type = Button_Color_Type.Light;

                        switch (pressed_button)
                        {
                            case "Room_Name_Button":
                                {
                                    if (tag == my_Buttons.Get_Active_Button_Room())
                                        color_Type = Button_Color_Type.Strong;

                                    break;
                                }
                            case "Grid_Tags_People_Button":
                                break;
                            case "Close_Error":
                                break;
                            default:
                                return;
                        }
                    }
                }
                else
                {
                    if (active_button != "" && active_button != button.Name && e.LeftButton == MouseButtonState.Pressed)
                        return;

                    if (e.RoutedEvent.Name == MouseEnterEvent.Name)
                    {
                        if (e.LeftButton == MouseButtonState.Pressed && active_button != "")
                            color_Type = Button_Color_Type.Strong;
                        else
                            color_Type = Button_Color_Type.Medium;
                    }
                }

                my_Buttons.Fast_Button_Appearence_Change(button.Name, color_Type, tag);
            }

            MakeAction_Form(action);
        }

        public void On_Login_Button(string login, string password)
        {
            bool need_to_remember = login_page.Remember_Me_Sign.IsChecked ?? false;
            string ip = login_page.TextBox_IP.Text;

            reesterWindows.OnLogin(need_to_remember, login, password, ip);
        }
        public void On_Register_Button_LogPage() => my_Actions.Navigate_MainFrame_To(Page_Type.RegisterPage);
        public void On_Exit_Button_RegPage() => my_Actions.Navigate_MainFrame_To(Page_Type.LogInPage);
        public void On_SettingsOpen_Button()
        {
            //Make_NotifyMessage("Ебои", "Пока не работает, не кликай", 5000);
            //return;
            my_Actions.Navigate_LeftPanel_To(Page_Type.TitleSettingsPage);
            my_Actions.Navigate_RightPanel_To(Page_Type.SettingsPage);
        }
        public void On_Room_Name_Button(string active_room_button, string room_name_button_pressed)
        {
            if (active_room_button == room_name_button_pressed) 
                return;
            else if (active_room_button != "")
                my_Buttons.Fast_Button_Appearence_Change("Room_Name_Button", Button_Color_Type.Light, active_room_button);

            my_Buttons.Fast_Button_Appearence_Change("Room_Name_Button", Button_Color_Type.Strong, room_name_button_pressed);
            my_Buttons.Set_Active_Button_Room(room_name_button_pressed);
        }
        public void On_Grid_Tags_People_Button(string grid_tags_people_name_pressed) => room_page.On_Grid_Tags_People_Button(grid_tags_people_name_pressed);
        public void On_MainSettings_Button(string room_name_button_pressed)
        {
            Button_Color_Type color_Type;


            foreach (Grid setting_grid in titleSettings_page.All_Settings.Children)
            {
                color_Type = Button_Color_Type.Light;

                if (setting_grid.Name == room_name_button_pressed)
                    color_Type = Button_Color_Type.Strong;

                my_Buttons.Fast_Button_Appearence_Change("Room_Name_Button", color_Type, room_name_button_pressed);
            }

            my_Buttons.Set_Active_Button_Room(room_name_button_pressed);
        }

        public Grid? FindButtonGrid(string name_button, string tag_button = "")
        {
            if (name_button == "Grid_Tags_People_Button")
            {
                return room_page.Panel_Users_Tags.FindElementByTag<Grid>(tag_button);
            }
            else if (name_button == "Room_Name_Button")
            {
                return room_panel_page.All_Rooms.FindElementByTag<Grid>(tag_button);
            }
            else if (name_button == "Close_Error")
            {
                return Error_StackPanel.FindElementByTag<Grid>(tag_button);
            }
            else
            {
                return room_page.FindName(name_button) as Grid ??
                       room_panel_page.FindName(name_button) as Grid ??
                       login_page.FindName(name_button) as Grid ??
                       register_page.FindName(name_button) as Grid ??
                       this.FindName(name_button) as Grid;
            }
        }

        public void ResetButtonsAppearence() => my_Buttons.ResetButtonsAppearence();
    }
}