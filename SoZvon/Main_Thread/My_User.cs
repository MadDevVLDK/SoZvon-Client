using SoZvon.SubClasses;
using System.Text;
using System.Threading.Channels;

namespace SoZvon.Main_Thread
{
    public partial class My_User
    {
        readonly ReaderWriterLockSlim userLock = new();

        public string? Login { get; private set; }
        public string? Password { get; private set; }
        public string? Room_Name { get; private set; }

        void SetLoginPassport(string login, string password)
        {
            userLock.EnterWriteLock();
            try
            {
                Login = login;
                Password = password;
            }
            finally
            {
                userLock.ExitWriteLock();
            }
        }
        bool IsLoginNull(out string login)
        {
            userLock.EnterWriteLock();
            try
            {
                login = Login ?? "";
                return string.IsNullOrEmpty(Login);
            }
            finally
            {
                userLock.ExitWriteLock();
            }
        }
        bool IsRoomNameNull(out string roomName)
        {
            userLock.EnterWriteLock();
            try
            {
                roomName = Room_Name ?? "";
                return string.IsNullOrEmpty(Room_Name);
            }
            finally
            {
                userLock.ExitWriteLock();
            }
        }
        void SetRoomName(string? value)
        {
            userLock.EnterWriteLock();
            try
            {
                Room_Name = value;
            }
            finally
            {
                userLock.ExitWriteLock();
            }
        }
        void ClearValues()
        {
            userLock.EnterWriteLock();
            try
            {
                Login = null;
                Password = null;
                Room_Name = null;
            }
            finally
            {
                userLock.ExitWriteLock();
            }
        }
    }
    public partial class My_User
    {
        readonly Channel<Action_Interfaces> Interfaces_Channel = Channel.CreateBounded<Action_Interfaces>(new BoundedChannelOptions(2000) { FullMode = BoundedChannelFullMode.Wait });

        async Task Interfaces_Channel_Thread(CancellationToken cancellationToken)
        {
            try
            {
                await foreach (Action_Interfaces action_IUser in Interfaces_Channel.Reader.ReadAllAsync(cancellationToken))
                {
                    try
                    {
                        Action action = InterpretateActionInterfaces(action_IUser);
                        OnAction(action);
                    }
                    catch (OperationCanceledException) { }
                    catch (My_Exception ex)
                    {
                        Make_ErrorMessage(ex.Title ?? action_IUser.Action.ToString(), ex.Message);
                    }
                }
            }
            catch (OperationCanceledException) { return; }
        }
        Action InterpretateActionInterfaces(Action_Interfaces action_IUser)
        {
            Action action;

            var dict = action_IUser.Params;

            switch (action_IUser.Action)
            {
                case ActionToIUser.MessageNotifyOccurred:
                    {
                        if (dict.Count != 2 || !dict.TryGetValue<string>("title", out var title) || !dict.TryGetValue<string>("text", out var text))
                            throw new My_Exception("no valid params");

                        action = () => Make_NotifyMessage(title, text);
                        break;
                    }
                case ActionToIUser.MessageErrorOccurred:
                    {
                        if (dict.Count != 2 || !dict.TryGetValue<string>("title", out var title) || !dict.TryGetValue<string>("text", out var text))
                            throw new My_Exception("no valid params");

                        action = () => Make_ErrorMessage(title, text);
                        break;
                    }
                case ActionToIUser.LogNotifyOccurred:
                    {
                        if (dict.Count != 1 || !dict.TryGetValue<string>("text", out var text))
                            throw new My_Exception("no valid params");

                        action = () => OnIUserAction(InterfaceToSend.IApplicationUI, ActionFromIUser.ShowNotifyLog, new() {
                            ["text"] = text
                        });
                        break;
                    }
                case ActionToIUser.LogErrorOccurred:
                    {
                        if (dict.Count != 1 || !dict.TryGetValue<string>("text", out var text))
                            throw new My_Exception("no valid params");

                        action = () => OnIUserAction(InterfaceToSend.IApplicationUI, ActionFromIUser.ShowErrorLog, new() {
                            ["text"] = text
                        });
                        break;
                    }
                case ActionToIUser.ApplicationExit:
                    {
                        if (dict.Count != 0)
                            throw new My_Exception("no valid params");

                        action = () =>
                        {
                            OnIUserAction(InterfaceToSend.IServerConnection, ActionFromIUser.OnCloseApplication, []);
                            OnIUserAction(InterfaceToSend.IVoiceManager, ActionFromIUser.OnCloseApplication, []);
                            OnIUserAction(InterfaceToSend.IManagerAPI, ActionFromIUser.OnCloseApplication, []);

                            cts.Cancel();
                            recieved_raw_messages_channel.Writer.Complete();
                            buttons_function_caller_channel.Writer.Complete();
                        };
                        break;
                    }
                case ActionToIUser.ServerNotifyOccured:
                    {
                        if (dict.Count != 1 || !dict.TryGetValue<NotificationServer>("notification", out var notification))
                            throw new My_Exception("no valid params");

                        action = () => MakeNotificationServer(notification);
                        break;
                    }
                case ActionToIUser.MessageRecieved:
                    {
                        if (dict.Count != 1 || !dict.TryGetValue<Message>("message", out var message))
                            throw new My_Exception("no valid params");

                        action = () => ReceiveRawMessage(message);
                        break;
                    }
                case ActionToIUser.IsConnectedChanged:
                    {
                        if (dict.Count != 1 || !dict.TryGetValue<bool>("value", out var value))
                            throw new My_Exception("no valid params");

                        action = () =>
                        {
                            if (!value)
                                ClearValuesOnLostConnection();

                            OnIUserAction(InterfaceToSend.IApplicationUI, ActionFromIUser.OnIsConnectedChange, new() {
                                ["value"] = value
                            });
                        };
                        break;
                    }
                case ActionToIUser.ConnectionClosedVoiceChat:
                    {
                        if (dict.Count != 0)
                            throw new My_Exception("no valid params");

                        action = () => OnIUserAction(InterfaceToSend.IApplicationUI, ActionFromIUser.OnExitVoiceChat, []);
                        break;
                    }
                case ActionToIUser.TagsTextChange:
                    {
                        if (dict.Count != 1 || !dict.TryGetValue<string>("text", out var text))
                            throw new My_Exception("no valid params");

                        action = () => TagsTextChange(text);
                        break;
                    }
                case ActionToIUser.UpdateIP:
                    {
                        if (dict.Count != 1 || !dict.TryGetValue<string>("ip", out var ip))
                            throw new My_Exception("no valid params");

                        action = () => {
                            OnIUserAction(InterfaceToSend.IServerConnection, ActionFromIUser.OnChangeIp, new() { ["ip"] = ip });
                            OnIUserAction(InterfaceToSend.IVoiceManager, ActionFromIUser.OnChangeIp, new() { ["ip"] = ip });
                            OnIUserAction(InterfaceToSend.IManagerAPI, ActionFromIUser.OnChangeIp, new() { ["ip"] = ip });
                        };
                        break;
                    }
                case ActionToIUser.SetOperationId:
                    {
                        if (dict.Count != 2 || !dict.TryGetValue<string>("fileName", out var fileName) || !dict.TryGetValue<string>("id", out var id))
                            throw new My_Exception("no valid params");

                        action = () => OnIUserAction(InterfaceToSend.IApplicationUI, ActionFromIUser.SetOperationId, new() { 
                            ["fileName"] = fileName, 
                            ["id"] = id 
                        });
                        break;
                    }
                case ActionToIUser.DownloadFile:
                    {
                        if (dict.Count != 2 || !dict.TryGetValue<string>("filename", out var filename) || !dict.TryGetValue<string>("saveFolder", out var saveFolder))
                            throw new My_Exception("no valid params");

                        action = () => OnIUserAction(InterfaceToSend.IManagerAPI, ActionFromIUser.DownloadFile, new() {
                            ["filename"] = filename,
                            ["saveFolder"] = saveFolder
                        });
                        break;
                    }
                case ActionToIUser.UploadFile:
                    {
                        if (dict.Count != 1 || !dict.TryGetValue<string>("filename", out var filename))
                            throw new My_Exception("no valid params");

                        action = () => OnIUserAction(InterfaceToSend.IManagerAPI, ActionFromIUser.UploadFile, new() {
                            ["filename"] = filename
                        });
                        break;
                    }
                case ActionToIUser.GetInfoFile:
                    {
                        if (dict.Count != 1 || !dict.TryGetValue<string>("filename", out var filename))
                            throw new My_Exception("no valid params");

                        action = () => OnIUserAction(InterfaceToSend.IManagerAPI, ActionFromIUser.GetInfoFile, new() {
                            ["filename"] = filename
                        });
                        break;
                    }
                case ActionToIUser.CancelOperation:
                    {
                        if (dict.Count != 1 || !dict.TryGetValue<string>("operationID", out var operationID))
                            throw new My_Exception("no valid params");

                        action = () => OnIUserAction(InterfaceToSend.IManagerAPI, ActionFromIUser.CancelOperation, new() {
                            ["operationID"] = operationID
                        });
                        break;
                    }
                case ActionToIUser.OnFocusTagTextblock:
                    {
                        if (dict.Count != 2 || !dict.TryGetValue<bool>("GotFocus", out var GotFocus) || !dict.TryGetValue<string>("text", out var text))
                            throw new My_Exception("no valid params");

                        action = () =>
                        {
                            if (GotFocus)
                            {
                                if (IsRoomNameNull(out string roomName))
                                    throw new My_Exception("Room_Name is null");

                                if (!roomManager.TryGetRoom(roomName, out Room? room) || room is null)
                                    return;

                                OnIUserAction(InterfaceToSend.IApplicationUI, ActionFromIUser.ShowUsersTags, new()
                                {
                                    ["users"] = room.GetUsers(),
                                    ["text"] = text
                                });
                            }
                            else
                            {
                                OnIUserAction(InterfaceToSend.IApplicationUI, ActionFromIUser.HideUsersTags, []);
                            }
                        };
                        break;
                    }
                case ActionToIUser.OnSendingMessageTextBox:
                    {
                        if (dict.Count != 3 || !dict.TryGetValue<string>("reciever", out var reciever) || !dict.TryGetValue<string>("text", out var text))
                            throw new My_Exception("no valid params");

                        if (!dict.TryGetValue<My_FileInfo[]>("filesInfos", out var filesInfos))
                            throw new My_Exception("no valid params");

                        action = () => OnSendingMessageTextBox(reciever, text, filesInfos);
                        break;
                    }
                case ActionToIUser.OnProgressHandler:
                    {
                        if (dict.Count != 3 || !dict.TryGetValue<string>("fileName", out var fileName) || !dict.TryGetValue<int>("percent", out var percent))
                            throw new My_Exception("no valid params");

                        if (!dict.TryGetValue<long>("fileSize", out var fileSize))
                            throw new My_Exception("no valid params");

                        action = () => OnIUserAction(InterfaceToSend.IApplicationUI, ActionFromIUser.OnProgressHandler, new() {
                            ["fileName"] = fileName,
                            ["percent"] = percent,
                            ["fileSize"] = fileSize,
                        });
                        break;
                    }
                case ActionToIUser.OnFileInfoHandler:
                    {
                        if (dict.Count != 2 || !dict.TryGetValue<string>("fileName", out var fileName) || !dict.TryGetValue<long>("fileSize", out var fileSize))
                            throw new My_Exception("no valid params");

                        action = () => OnIUserAction(InterfaceToSend.IApplicationUI, ActionFromIUser.OnFileInfoHandler, new() {
                            ["fileName"] = fileName,
                            ["fileSize"] = fileSize
                        });
                        break;
                    }
                case ActionToIUser.OnErrorHandler:
                    {
                        if (dict.Count != 2 || !dict.TryGetValue<string>("fileName", out var fileName) || !dict.TryGetValue<string>("text", out var text))
                            throw new My_Exception("no valid params");

                        action = () => OnIUserAction(InterfaceToSend.IApplicationUI, ActionFromIUser.OnErrorHandler, new() {
                            ["fileName"] = fileName,
                            ["text"] = text
                        });
                        break;
                    }
                case ActionToIUser.OnUploadErrorHandler:
                    {
                        if (dict.Count != 2 || !dict.TryGetValue<string>("fileName", out var fileName) || !dict.TryGetValue<string>("text", out var text))
                            throw new My_Exception("no valid params");

                        action = () => OnIUserAction(InterfaceToSend.IApplicationUI, ActionFromIUser.OnUploadErrorHandler, new() {
                            ["fileName"] = fileName,
                            ["text"] = text
                        });
                        break;
                    }
                case ActionToIUser.GetMicrophonesInfo:
                    {
                        if (dict.Count != 0)
                            throw new My_Exception("no valid params");

                        action = () =>
                        {
                            var microphones = voiceManager.GetMicrophoneDevices();
                            OnIUserAction(InterfaceToSend.IApplicationUI, ActionFromIUser.OnMicrophonesInfo, new() { ["microphones"] = microphones });
                        };
                        break;
                    }
                case ActionToIUser.OnMicrophonesInfo:
                    {
                        if (dict.Count != 1 || !dict.TryGetValue<Dictionary<string, string>>("microphones", out var microphones))
                            throw new My_Exception("no valid params");

                        action = () => OnIUserAction(InterfaceToSend.IApplicationUI, ActionFromIUser.OnMicrophonesInfo, new() { ["microphones"] = microphones });
                        break;
                    }
                case ActionToIUser.SelectMicrophoneByName:
                    {
                        if (dict.Count != 1 || !dict.TryGetValue<string>("microphone", out var microphone))
                            throw new My_Exception("no valid params");

                        action = () => OnIUserAction(InterfaceToSend.IVoiceManager, ActionFromIUser.OnSelectMicrophoneByName, new() { ["microphone"] = microphone });
                        break;
                    }
                case ActionToIUser.ReloadConnectionServer:
                    {
                        if (dict.Count != 0)
                            throw new My_Exception("no valid params");

                        action = () => OnIUserAction(InterfaceToSend.IServerConnection, ActionFromIUser.ReloadConnectionServer, []);
                        break;
                    }
                default:
                    throw new My_Exception("no valid ActionFromIUser");
            }

            return action;
        }
        public async void OnInterfacesAction(ActionToIUser action_IUser, Dictionary<string, object> dict) => await Interfaces_Channel.Writer.WriteAsync(new(action_IUser, dict));
    }
    public partial class My_User : IUser
    {
        internal readonly ServerAPIManager.IManagerAPI managerAPI;
        internal readonly VoiceChatManager.IVoiceManager voiceManager;
        internal readonly UI.IApplicationUI applicationUI;
        internal readonly ServerConnectionManager.IServerConnection serverConnection;
        internal readonly RoomManager roomManager = new();

        readonly CancellationTokenSource cts = new();
        readonly Channel<Action> all_actions_channel = Channel.CreateBounded<Action>(new BoundedChannelOptions(2000) { FullMode = BoundedChannelFullMode.Wait });
        readonly Channel<Message> recieved_raw_messages_channel = Channel.CreateBounded<Message>(new BoundedChannelOptions(2000) { FullMode = BoundedChannelFullMode.Wait });

        public My_User()
        {
            applicationUI = new UI.MainWindow(this);
            voiceManager = new VoiceChatManager.VoiceManager(this);
            serverConnection = new ServerConnectionManager.ConnectionManager(this);
            managerAPI = new ServerAPIManager.API_Manager(this);

            StartProperties();
        }

        void StartProperties()
        {
            //Главный поток интерпритации сообщений
            _ = Main_Thread(cts.Token);
            _ = Read_Messages_Thread(cts.Token);
            _ = ButtonsFunctionCaller(cts.Token);
            _ = Interfaces_Channel_Thread(cts.Token);

            //Стартовые настройки
            OnIUserAction(InterfaceToSend.IApplicationUI, ActionFromIUser.OnStart, []);
            OnIUserAction(InterfaceToSend.IServerConnection, ActionFromIUser.OnStart, []);
        }

        void OnIUserAction(InterfaceToSend reciever, ActionFromIUser actionIUser, Dictionary<string, object> dict)
        {
            switch (reciever)
            {
                case InterfaceToSend.IApplicationUI:
                    {
                        applicationUI.OnIUserAction(actionIUser, dict);
                        break;
                    }
                case InterfaceToSend.IServerConnection:
                    {
                        serverConnection.OnIUserAction(actionIUser, dict);
                        break;
                    }
                case InterfaceToSend.IVoiceManager:
                    {
                        voiceManager.OnIUserAction(actionIUser, dict);
                        break;
                    }
                case InterfaceToSend.IManagerAPI:
                    {
                        managerAPI.OnIUserAction(actionIUser, dict);
                        break;
                    }
                default:
                    throw new My_Exception("OnIUserAction", "InterfaceToSend is incorrect");
            }
        }

        async Task Main_Thread(CancellationToken cancellationToken)
        {
            try
            {
                await foreach (Action action in all_actions_channel.Reader.ReadAllAsync(cancellationToken))
                {
                    try
                    {
                        action();
                    }
                    catch (OperationCanceledException) { }
                    catch (My_Exception ex)
                    {
                        Make_ErrorMessage(ex.Title ?? "Error_Main_Thread_My_User", ex.Message.ToString());
                    }
                    catch (Exception ex)
                    {
                        Make_ErrorMessage("Error_Main_Thread_My_User", ex.Message.ToString());
                    }
                }
            }
            catch (OperationCanceledException) { return; }
        }
        async void OnAction(Action action) => await all_actions_channel.Writer.WriteAsync(action, cts.Token);

        async Task Read_Messages_Thread(CancellationToken cancellationToken)
        {
            try
            {
                await foreach (Message msg in recieved_raw_messages_channel.Reader.ReadAllAsync(cancellationToken))
                {
                    try
                    {
                        OnAction(() => ReadMessage(msg));
                    }
                    catch (OperationCanceledException) { }
                    catch (Exception ex)
                    {
                        Make_ErrorMessage("Error_Read_Messages_Thread_My_User", ex.Message.ToString());
                    }
                }
            }
            catch (OperationCanceledException) { return; }
        }
        async void ReceiveRawMessage(Message message)
        {
            await recieved_raw_messages_channel.Writer.WriteAsync(message, cts.Token);
        }
        void SendMessage(Message msg) => OnIUserAction(InterfaceToSend.IServerConnection, ActionFromIUser.OnSendingMessage, new() { ["message"] = msg });
        void SendMessage(Guid guid, CommandText commandText, params object?[]? args) => SendMessage(Message.MakeMessage(guid.ToByteArray(), new(commandText), args));
        void ReadMessage(Message message)
        {
            List<byte> all_list_bytes = [.. message.message_data.Skip(4)];

            if (!MessageInfo.ReadMessageInfo(ref all_list_bytes, ref message))
                throw new My_Exception("ReadMessageInfo is false");

            CommandText commandText = message.message_info.CommandText;

            try
            {
                MessageInfo message_info = message.message_info;

                switch (commandText)
                {
                    case CommandText.ShowRooms:
                        {
                            List<Room> rooms = [];

                            for (; ; )
                            {
                                if (all_list_bytes.Count == 0) { break; }

                                string name_room = MessageInfo.Read_String_Bytes(ref all_list_bytes);
                                short num_users_in_room = MessageInfo.Read_Int16_Bytes(ref all_list_bytes);
                                string login_creator = MessageInfo.Read_String_Bytes(ref all_list_bytes);

                                rooms.Add(new(name_room, num_users_in_room, login_creator));
                            }

                            roomManager.ClearRoomsAddRange(rooms);

                            //ОНИ УДАЛЯЮТСЯ ИЗ СПИСКА СНАЧАЛА, А ПОТОМ ДОБАВЛЯЮТСЯ
                            OnIUserAction(InterfaceToSend.IApplicationUI, ActionFromIUser.ShowRoomsOnScreen, new() {
                                ["rooms"] = rooms
                            });
                            break;
                        }
                    case CommandText.PeopleRoom:
                        {
                            if (IsRoomNameNull(out string roomName))
                                throw new ArgumentException("Room_Name is null");

                            List<Room_User> users_list = [];

                            while (all_list_bytes.Count > 0)
                            {
                                string userLogin = MessageInfo.Read_String_Bytes(ref all_list_bytes);
                                string userName = MessageInfo.Read_String_Bytes(ref all_list_bytes);
                                bool hasVoiceChat = MessageInfo.Read_MyBool_Bytes(ref all_list_bytes);

                                users_list.Add(new(userLogin, userName, roomName, hasVoiceChat));
                            }

                            //ОНИ УДАЛЯЮТСЯ ИЗ СПИСКА СНАЧАЛА, А ПОТОМ ДОБАВЛЯЮТСЯ
                            roomManager.FindRoomClearUsersAddRange(roomName, users_list);

                            OnIUserAction(InterfaceToSend.IApplicationUI, ActionFromIUser.ShowUsersOnScreen, new() {
                                ["users"] = users_list
                            });
                            break;
                        }
                    case CommandText.ReplyOk:
                        {
                            Guid guid = MessageInfo.Read_Guid_Bytes(ref all_list_bytes);
                            message.dateTime = MessageInfo.Read_DateTime_Bytes(ref all_list_bytes);

                            Message msg = Message.FindMessage(msg_ => msg_.Id.ToString() == guid.ToString()) 
                                           ?? throw new My_Exception("Server_Ok_Error", $"ID Message --> {guid}. Ошибка, сообщения с таким ID не существует");

                            if (msg.message_info.CommandText == CommandText.Notification_Cl)
                                break;

                            InterpretateConfirmationServer(msg, message);
                            break;
                        }
                    case CommandText.ReplyError:
                        {
                            Guid guid = MessageInfo.Read_Guid_Bytes(ref all_list_bytes);
                            string text_error = MessageInfo.Read_String_Bytes(ref all_list_bytes);

                            Message? msg = Message.FindMessage(msg_ => msg_.Id.ToString() == guid.ToString());
                            CommandText command_text = msg?.message_info.CommandText ?? CommandText.ReplyError;

                            string error_title = "Unknown_Error";

                            if (command_text is not CommandText.ReplyError)
                            {
                                if (command_text == CommandText.LogIn) error_title = "Login_Error";
                                else if (command_text == CommandText.Register) error_title = "Register_Error";
                                else if (command_text == CommandText.EnterRoom) error_title = "Room_Error";
                                else if (command_text == CommandText.ExitRoom) error_title = "Room_Error";
                                else if (command_text == CommandText.AddRoom) error_title = "Add_Room_Error";
                                else if (command_text == CommandText.DeleteRoom) error_title = "Delete_Room_Error";
                            }

                            Make_ErrorMessage(error_title, text_error);
                            break;
                        }
                    case CommandText.Notification_Serv:
                        {
                            InterpretateNotification(message);
                            break;
                        }
                    case CommandText.Info:
                        {
                            string recieved_message = MessageInfo.Read_String_Bytes(ref all_list_bytes);
                            DateTime dt = MessageInfo.Read_DateTime_Bytes(ref all_list_bytes);

                            OnIUserAction(InterfaceToSend.IApplicationUI, ActionFromIUser.Show_SERVER_MessageOnScreen, new() {
                                ["guid"] = message.Id,
                                ["date"] = dt,
                                ["text"] = recieved_message
                            });
                            break;
                        }
                    case CommandText.All_Serv or CommandText.Private_Serv:
                        {
                            string login_sender = MessageInfo.Read_String_Bytes(ref all_list_bytes);
                            string name_sender = MessageInfo.Read_String_Bytes(ref all_list_bytes);

                            string temp_files = MessageInfo.Read_String_Bytes(ref all_list_bytes);
                            string[] files_pathes = string.IsNullOrEmpty(temp_files) ? [] : temp_files.Split('|');

                            string text = MessageInfo.Read_String_Bytes(ref all_list_bytes);
                            DateTime dt = MessageInfo.Read_DateTime_Bytes(ref all_list_bytes);

                            var filesInfos = InterpretateMassFilesPathes(files_pathes);

                            var dict = new Dictionary<string, object>() {
                                ["dateTime"] = dt,
                                ["guid"] = message.Id,
                                ["text"] = text,
                                ["filesInfos"] = filesInfos
                            };

                            if (IsLoginNull(out var login))
                                throw new My_Exception(commandText.ToString(), "Login is null");

                            if (login_sender != login)
                            {
                                dict.Add("sender", name_sender);
                                dict.Add("IsPublic", (MessageFromUser)message_info.CommandText);

                                OnIUserAction(InterfaceToSend.IApplicationUI, ActionFromIUser.Show_USER_MessageOnScreen, dict);
                            }
                            else
                            {
                                dict.Add("reciever", "");

                                OnIUserAction(InterfaceToSend.IApplicationUI, ActionFromIUser.OnSendingUserMessage, dict);
                            }
                            break;
                        }
                    default: 
                        break;
                }
            }
            catch (My_Exception ex)
            {
                Make_ErrorMessage(ex.Title ?? commandText.ToString(), ex.Message);
            }
            finally
            {
                message.AddMessageToHistory();
            }
        }
        void InterpretateConfirmationServer(Message message, Message message_ok)
        {
            List<byte> all_list_bytes = [.. message.message_data.Skip(MessageInfo.lenght_message_head)];
            CommandText commandText = message.message_info.CommandText;

            try
            {
                switch (commandText)
                {
                    case CommandText.LogIn:
                        {
                            string login = MessageInfo.Read_String_Bytes(ref all_list_bytes);
                            string password = MessageInfo.Read_String_Bytes(ref all_list_bytes);

                            if (string.IsNullOrEmpty(login))
                                throw new My_Exception("login is IsNullOrEmpty");

                            if (string.IsNullOrEmpty(password))
                                throw new My_Exception("password is IsNullOrEmpty");

                            SetLoginPassport(login, password);

                            OnIUserAction(InterfaceToSend.IApplicationUI, ActionFromIUser.OnLogin, []);
                            OnIUserAction(InterfaceToSend.IServerConnection, ActionFromIUser.OnLogin, []);
                            OnIUserAction(InterfaceToSend.IVoiceManager, ActionFromIUser.OnLogin, new() { ["guid"] = message_ok.Id });
                            OnIUserAction(InterfaceToSend.IManagerAPI, ActionFromIUser.OnLogin, new() { ["guid"] = message_ok.Id });
                            break;
                        }
                    case CommandText.Register:
                        {
                            string login = MessageInfo.Read_String_Bytes(ref all_list_bytes);
                            string password = MessageInfo.Read_String_Bytes(ref all_list_bytes);

                            if (string.IsNullOrEmpty(login))
                                throw new My_Exception("login is IsNullOrEmpty");

                            if (string.IsNullOrEmpty(password))
                                throw new My_Exception("password is IsNullOrEmpty");

                            OnIUserAction(InterfaceToSend.IApplicationUI, ActionFromIUser.OnRegister, new() {
                                ["login"] = login,
                                ["password"] = password
                            });
                            break;
                        }
                    case CommandText.EnterRoom:
                        {
                            string room_name = MessageInfo.Read_String_Bytes(ref all_list_bytes);

                            if (string.IsNullOrEmpty(room_name))
                                throw new My_Exception("room_name is IsNullOrEmpty");

                            SetRoomName(room_name);

                            OnIUserAction(InterfaceToSend.IApplicationUI, ActionFromIUser.OnEnterRoom, new() {
                                ["room_name"] = room_name
                            });
                            break;
                        }
                    case CommandText.ExitRoom:
                        {
                            string exit_room_name = MessageInfo.Read_String_Bytes(ref all_list_bytes);

                            if (IsRoomNameNull(out var roomName))
                                throw new ArgumentException("Room_Name is null");

                            if (roomName != exit_room_name)
                                throw new My_Exception("Room_Name != room_name");

                            SetRoomName(null);

                            OnIUserAction(InterfaceToSend.IApplicationUI, ActionFromIUser.OnExitRoom, []);
                            break;
                        }
                    case CommandText.AddRoom:
                        {
                            Make_NotifyMessage("Room_Adding", "Room was succesfully added");
                            break;
                        }
                    case CommandText.DeleteRoom:
                        {
                            Make_NotifyMessage("Room_Deleting", "Room was succesfully deleted");
                            break;
                        }
                    case CommandText.All_Cl or CommandText.Private_Cl:
                        {
                            OnIUserAction(InterfaceToSend.IApplicationUI, ActionFromIUser.OnUserMessage, new() {
                                ["message"] = message
                            });
                            break;
                        }
                    default: 
                        throw new My_Exception("InterpretateConfirmationServer", "commandText is not correct");
                }
            }
            catch (My_Exception ex)
            {
                throw new My_Exception(ex.Title ?? commandText.ToString(), ex.Message);
            }
        }
        void InterpretateNotification(Message message)
        {
            List<byte> all_list_bytes = [.. message.message_data.Skip(MessageInfo.lenght_message_head)];

            TypeNotification typeNotification = (TypeNotification)MessageInfo.Read_Byte_Bytes(ref all_list_bytes);

            try
            {
                switch (typeNotification)
                {
                    case TypeNotification.Texting:
                        {
                            string login = MessageInfo.Read_String_Bytes(ref all_list_bytes);

                            if (IsRoomNameNull(out var roomName))
                                throw new ArgumentException("Room_Name is null");

                            if (!roomManager.GetUserFromRoom(roomName, login, out Room_User? user))
                                throw new My_Exception("GetUserFromRoom is false");

                            if (user is null)
                                throw new My_Exception("user is null");

                            OnIUserAction(InterfaceToSend.IApplicationUI, ActionFromIUser.OnUserTexting, new() {
                                ["user"] = user
                            });
                            break;
                        }
                    case TypeNotification.UploadingFile:
                        {
                            string login = MessageInfo.Read_String_Bytes(ref all_list_bytes);
                            string name_file = MessageInfo.Read_String_Bytes(ref all_list_bytes);

                            OnIUserAction(InterfaceToSend.IApplicationUI, ActionFromIUser.NotificationOnFileLoadingToServer, new() {
                                ["file_name"] = name_file
                            });
                            break;
                        }
                    case TypeNotification.EndUploadingFile:
                        {
                            string login = MessageInfo.Read_String_Bytes(ref all_list_bytes);
                            string name_file = MessageInfo.Read_String_Bytes(ref all_list_bytes);

                            OnIUserAction(InterfaceToSend.IApplicationUI, ActionFromIUser.NotificationOnReadyFileToDownload, new() {
                                ["file_name"] = name_file
                            });
                            break;
                        }
                    case TypeNotification.JoinRoom:
                        {
                            string login = MessageInfo.Read_String_Bytes(ref all_list_bytes);
                            string name = MessageInfo.Read_String_Bytes(ref all_list_bytes);
                            bool isEnteringRoom = typeNotification is TypeNotification.JoinRoom;

                            if (IsRoomNameNull(out string roomName))
                                throw new ArgumentException("Room_Name is null");

                            Room_User user = new(login, name, roomName, false);

                            if (!roomManager.ExecuteWithRoom(roomName, (room) => room.AddUser(user)))
                                throw new My_Exception($"AddUser is false");

                            OnIUserAction(InterfaceToSend.IApplicationUI, ActionFromIUser.ShowUserOnScreen, new() {
                                ["user"] = user
                            });
                            break;
                        }
                    case TypeNotification.ExitRoom:
                        {
                            string login = MessageInfo.Read_String_Bytes(ref all_list_bytes);
                            bool isEnteringRoom = typeNotification is TypeNotification.JoinRoom;

                            if (IsRoomNameNull(out string roomName))
                                throw new ArgumentException("Room_Name is null");

                            if (!roomManager.ExecuteWithRoom(roomName, (room) => room.RemoveUser(login)))
                                throw new My_Exception($"AddUser is false");

                            OnIUserAction(InterfaceToSend.IApplicationUI, ActionFromIUser.OnUserExitRoom, new() {
                                ["login"] = login
                            });
                            break;
                        }
                    case TypeNotification.JoinVoiceChat or TypeNotification.ExitVoiceChat:
                        {
                            string login = MessageInfo.Read_String_Bytes(ref all_list_bytes);
                            bool isEnteringVoiceChat = typeNotification is TypeNotification.JoinVoiceChat;
                            Room_User? user = null;

                            if (IsRoomNameNull(out string roomName))
                                throw new ArgumentException("Room_Name is null");

                            bool roomOperationSuccessful = roomManager.ExecuteWithRoom(
                                roomName,
                                (room) => {
                                    if (!room.ChangeUserInVoiceChat(login, isEnteringVoiceChat, out user))
                                        throw new My_Exception($"ChangeUserInVoiceChat is false");
                                }
                            );

                            if (!roomOperationSuccessful)
                                throw new My_Exception($"ExecuteWithRoom is false");

                            if (user is null)
                                throw new My_Exception($"user is null");

                            var type = isEnteringVoiceChat ? ActionFromIUser.OnUserEnterVoiceChat : ActionFromIUser.OnUserExitVoiceChat;

                            OnIUserAction(InterfaceToSend.IApplicationUI, type, new() {
                                ["user"] = user
                            });
                            break;
                        }
                    case TypeNotification.AddOrChangeRoom:
                        {
                            string name_room = MessageInfo.Read_String_Bytes(ref all_list_bytes);
                            short num_users_in_room = MessageInfo.Read_Int16_Bytes(ref all_list_bytes);
                            string login_creator = MessageInfo.Read_String_Bytes(ref all_list_bytes);

                            Room room = roomManager.GetOrCreateRoom(name_room, num_users_in_room, login_creator, out bool IsNewRoom);

                            var type = IsNewRoom ? ActionFromIUser.ShowRoomOnScreen : ActionFromIUser.UpdateRoomOnScreen;

                            OnIUserAction(InterfaceToSend.IApplicationUI, type, new() {
                                ["room"] = room
                            });
                            break;
                        }
                    case TypeNotification.DeleteRoom:
                        {
                            string name_room = MessageInfo.Read_String_Bytes(ref all_list_bytes);

                            OnIUserAction(InterfaceToSend.IApplicationUI, ActionFromIUser.DeleteRoomOnScreen, new() {
                                ["roomName"] = name_room
                            });
                            break;
                        }
                    default: 
                        throw new My_Exception("InterpretateNotification", "typeNotification is not correct");
                }
            }
            catch(My_Exception ex)
            {
                throw new My_Exception(ex.Title ?? typeNotification.ToString(), ex.Message);
            }
        }

        void Make_ErrorMessage(string title, string text) => OnIUserAction(InterfaceToSend.IApplicationUI, ActionFromIUser.ShowErrorMessage, new() {
            ["title"] = title,
            ["message"] = text
        });
        void Make_NotifyMessage(string title, string text) => OnIUserAction(InterfaceToSend.IApplicationUI, ActionFromIUser.ShowNotifyMessage, new() {
            ["title"] = title,
            ["message"] = text
        });
        void MakeNotificationServer(NotificationServer notification)
        {
            List<object> args = [];

            var dict = notification.Dict;
            var type = notification.Type;
            args.Add((byte)type);

            switch (type)
            {
                case TypeNotification.UploadingFile:
                    {
                        if (!dict.TryGetValue<string>("name_file", out var name_file))
                            return;
                        if (!dict.TryGetValue<short>("percentage", out var percentage))
                            return;

                        args.Add(name_file);
                        args.Add(percentage);
                        break;
                    }
                case TypeNotification.EndUploadingFile:
                    {
                        if (!dict.TryGetValue<string>("name_file", out var name_file)) return;

                        args.Add(name_file);
                        break;
                    }
            }

            SendMessage(Guid.NewGuid(), CommandText.Notification_Cl, [.. args]);
        }

        void OnSendingMessageTextBox(string reciever, string text, My_FileInfo[] filesInfos)
        {
            if (IsLoginNull(out string login))
                throw new My_Exception("Sending_Error", "login is null");

            if (text.Length == 0)
                throw new My_Exception("Sending_Error", "emptiness");
            else if (text.Length > 1500)
                throw new My_Exception("Sending_Error","max length is 1500 letters");
            else if (reciever == login)
                throw new My_Exception("Sending_Error", "you can`t send private msg to yourself");

            if (IsRoomNameNull(out string roomName))
                throw new My_Exception("Sending_Error", "you are not in the room");

            CommandText commandText = CommandText.All_Cl;

            if (reciever != "")
            {
                bool has_user = false;
                bool operation_room = roomManager.ExecuteWithRoom(roomName, (room) => has_user = room.HasUser(reciever));

                if (!operation_room || !has_user)
                    throw new My_Exception("Sending_Error", "there is no such user to send message");

                commandText = CommandText.Private_Cl;
            }

            Guid guid = Guid.NewGuid();

            OnIUserAction(InterfaceToSend.IApplicationUI, ActionFromIUser.OnSendingUserMessage, new() {
                ["dateTime"] = DateTime.MinValue,
                ["guid"] = guid,
                ["text"] = text,
                ["reciever"] = reciever,
                ["filesInfos"] = filesInfos
            });

            string? files_pathes = GetFilesPathes(filesInfos);

            if (commandText is CommandText.Private_Cl)
            {
                SendMessage(guid, commandText, reciever, files_pathes, text);
            }
            else SendMessage(guid, commandText, files_pathes, text);
        }
        void TagsTextChange(string text)
        {
            if (IsRoomNameNull(out string roomName))
                throw new My_Exception("Room_Name is null");

            if (!roomManager.GetUsersInRoom(roomName, out List<Room_User>? users))
                throw new My_Exception("GetUsersInRoom is false");

            if (users is null)
                throw new My_Exception("users is false");

            OnIUserAction(InterfaceToSend.IApplicationUI, ActionFromIUser.UpdateUsersTags, new() {
                ["users"] = users,
                ["text"] = text
            });
        }

        static string? GetFilesPathes(My_FileInfo[] file_infos)
        {
            if (file_infos.Length != 0)
            {
                var sb = new StringBuilder();

                foreach (var file_items in file_infos)
                {
                    sb.Append(file_items.Name);
                    sb.Append(file_items.Extension);
                    sb.Append('|');
                }

                sb.Remove(sb.Length - 1, 1);

                return sb.ToString();
            }

            return null;
        }
        static My_FileInfo[] InterpretateMassFilesPathes(string[] files_pathes)
        {
            List<My_FileInfo> filesInfos = [];

            if (files_pathes is not [])
            {
                foreach (var file_path in files_pathes)
                {
                    var fileInfo = My_FileInfo.GetFileInfo(
                        filePath: $"{My_FileInfo.sozvon_papka}\\{file_path}",
                        isFromHistoryMsg: true
                    );

                    filesInfos.Add(fileInfo);
                }
            }

            return [.. filesInfos];
        }

        void ClearValuesOnLostConnection()
        {
            ClearValues();

            Message.ClearMessagesHistory();
            roomManager.ClearRooms();
        }
    }
}
