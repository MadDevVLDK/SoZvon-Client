using SoZvon.SubClasses;
using System.Threading.Channels;

namespace SoZvon.Main_Thread
{
    record CallFunction_Params(string Name_Button, Dictionary<string, object> Dict);

    // Все что связанно с кнопками
    public partial class My_User
    {
        readonly HashSet<string> buttons_with_no_connection = ["Exit_Button_RegPage", "Register_Button_LogPage", "Close_Error", "Reload_Connection_Button"];
        readonly Channel<CallFunction_Params> buttons_function_caller_channel = Channel.CreateUnbounded<CallFunction_Params>();
        readonly My_Timer buttonTimer = new(2);

        public async void On_Button_Clicked(string button_name, Dictionary<string, object> dict) => await buttons_function_caller_channel.Writer.WriteAsync(new(button_name, dict), cts.Token);
        async Task ButtonsFunctionCaller(CancellationToken cancellationToken)
        {
            buttonTimer.SetAcionOnTick(() => Make_ErrorMessage("Button_Error", "Error on Pressing Button"));

            try
            {
                await foreach (CallFunction_Params button in buttons_function_caller_channel.Reader.ReadAllAsync(cancellationToken))
                {
                    buttonTimer.Start();
                    try
                    {
                        Action action = InterpretateButtonClick(button.Name_Button, button.Dict);

                        if (!serverConnection.IsConnected && !buttons_with_no_connection.Contains(button.Name_Button))
                        {
                            await serverConnection.New_ConnectionAttempt(1500, () => OnAction(action.Invoke));
                        }
                        else OnAction(action.Invoke);
                    }
                    finally
                    {
                        buttonTimer.Stop();
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                Make_ErrorMessage("ButtonsFunctionCaller", $"Fatal Error: {ex.Message}");
            }
        }
        public Action InterpretateButtonClick(string button_name, Dictionary<string, object> dict)
        {
            Action action;

            switch (button_name)
            {
                case "Login_Button":
                    {
                        if (dict.Count != 2 || !dict.TryGetValue<string>("login", out var login) || !dict.TryGetValue<string>("password", out var password))
                            throw new ArgumentException(button_name);

                        action = () => On_Login_Button(login, password);
                        break;
                    }
                case "Reload_Connection_Button":
                    {
                        if (dict.Count != 0)
                            throw new ArgumentException(button_name);

                        action = On_Reload_Connection_Button;
                        break;
                    }
                case "Register_Button_RegPage":
                    {
                        if (dict.Count != 4 || !dict.TryGetValue<string>("login", out var login) || !dict.TryGetValue<string>("password", out var password))
                            throw new ArgumentException(button_name);

                        if (!dict.TryGetValue<string>("name", out var name) || !dict.TryGetValue<string>("email", out var email))
                            throw new ArgumentException(button_name);

                        action = () => On_Register_Button_RegPage(login, password, name, email);
                        break;
                    }
                case "Register_Button_LogPage":
                    {
                        if (dict.Count != 0)
                            throw new ArgumentException(button_name);

                        action = On_Register_Button_LogPage;
                        break;
                    }
                case "Exit_Button_RegPage":
                    {
                        if (dict.Count != 0)
                            throw new ArgumentException(button_name);

                        action = On_Exit_Button_RegPage;
                        break;
                    }
                case "SettingsOpen_Button":
                    {
                        if (dict.Count != 0)
                            throw new ArgumentException(button_name);

                        action = On_SettingsOpen_Button;
                        break;
                    }
                case "Add_Room":
                    {
                        if (dict.Count != 1 || !dict.TryGetValue<string>("room_name", out var room_name))
                            throw new ArgumentException(button_name);

                        action = () => On_Add_Button(room_name);
                        break;
                    }
                case "Close_Error":
                    {
                        if (dict.Count != 1 || !dict.TryGetValue<string>("name_error", out var name_error))
                            throw new ArgumentException(button_name);

                        action = () => On_Close_Error_Button(name_error);
                        break;
                    }
                case "Room_Button":
                    {
                        if (dict.Count != 2 || !dict.TryGetValue<string>("active_button_room", out var active_button_room) || !dict.TryGetValue<bool>("button_state", out var button_state))
                            throw new ArgumentException(button_name);

                        action = () => On_Room_Button(active_button_room, button_state);
                        break;
                    }
                case "Delete_Room":
                    {
                        if (dict.Count != 1 || !dict.TryGetValue<string>("room_name", out var room_name))
                            throw new ArgumentException(button_name);

                        action = () => On_Delete_Room_Button(room_name);
                        break;
                    }
                case "Room_Name_Button":
                    {
                        if (dict.Count != 2 || !dict.TryGetValue<string>("active_room_button", out var active_room_button) || !dict.TryGetValue<string>("room_name_button_pressed", out var room_name_button_pressed))
                            throw new ArgumentException(button_name);

                        action = () => On_Room_Name_Button(active_room_button, room_name_button_pressed);
                        break;
                    }
                case "Grid_Tags_People_Button":
                    {
                        if (dict.Count != 1 || !dict.TryGetValue<string>("grid_tags_people_name_pressed", out var grid_tags_people_name_pressed))
                            throw new ArgumentException(button_name);

                        action = () => On_Grid_Tags_People_Button(grid_tags_people_name_pressed);
                        break;
                    }
                case "Join_VoiceChat_Button":
                    {
                        if (dict.Count != 1 || !dict.TryGetValue<bool>("Join_VoiceChat_Button_state", out var Join_VoiceChat_Button_state))
                            throw new ArgumentException(button_name);

                        action = () => On_Join_VoiceChat_Button(Join_VoiceChat_Button_state);
                        break;
                    }
                case "Speak_Button":
                    {
                        if (dict.Count != 2 || !dict.TryGetValue<bool>("Join_VoiceChat_Button_state", out var Join_VoiceChat_Button_state) || !dict.TryGetValue<bool>("Speak_Button_state", out var Speak_Button_state))
                            throw new ArgumentException(button_name);

                        action = () => On_Speaking_Button(Join_VoiceChat_Button_state, Speak_Button_state);
                        break;
                    }
                default:
                    throw new ArgumentException("Strange Button Pressed");
            }

            return action;
        }

        // Код при срабатывания нажатий кнопок
        public void On_Login_Button(string login, string password)
        {
            if (login == "" || password == "")
                throw new UserException("Some of the fields are empty");

            OnIUserAction(InterfaceToSend.IApplicationUI, ActionFromIUser.OnLoginButton, new() {
                ["login"] = login,
                ["password"] = password
            });

            SendMessage(Guid.NewGuid(), CommandText.LogIn, login, password);
        }
        public void On_Register_Button_RegPage(string login, string password, string name, string email)
        {
            if (login == "" || password == "" || name == "" || email == "")
                throw new UserException("Some of the fields are empty");
            
            SendMessage(Guid.NewGuid(), CommandText.Register, login, password, name, email);
        }

        public void On_Room_Button(string active_button_room, bool button_state)
        {
            if (active_button_room == "")
                throw new UserException("Empty room name");

            if (!roomManager.TryGetRoom(active_button_room, out Room? room) || room is null)
                throw new UserException("No room with such name");

            var state = button_state ? CommandText.ExitRoom : CommandText.EnterRoom;

            SendMessage(Guid.NewGuid(), state, active_button_room);
        }
        public void On_Delete_Room_Button(string room_name)
        {
            if (room_name == "")
                throw new UserException("Empty room name");
            
            if (!roomManager.TryGetRoom(room_name, out Room? room) || room is null)
                throw new UserException("No room with such name");

            SendMessage(Guid.NewGuid(), CommandText.DeleteRoom, room.Name_Room);
        }
        public void On_Room_Name_Button(string active_room_button, string room_name_button_pressed)
        {
            if (!IsRoomNameNull(out _))
                return;

            OnIUserAction(InterfaceToSend.IApplicationUI, ActionFromIUser.OnRoomNameButton, new() {
                ["active_room_button"] = active_room_button,
                ["room_name_button_pressed"] = room_name_button_pressed
            });
        }

        public void On_Join_VoiceChat_Button(bool Join_VoiceChat_Button_state)
        {
            if (IsRoomNameNull(out var roomName))
                throw new UserException("Join the Room");

            if (IsLoginNull(out var login))
                throw new UserException("Login is null");

            if (!roomManager.GetUserFromRoom(roomName, login, out Room_User user))
                throw new UserException("GetUserFromRoom is false");

            var action = ActionFromIUser.OnUserExitVoiceChat;

            if (Join_VoiceChat_Button_state)
            {
                voiceManager.ExitVoiceChat();
            }
            else
            {
                if (!voiceManager.JoinVoiceChat())
                    throw new UserException("Error in Joining VoiceChat");

                action = ActionFromIUser.OnEnterVoiceChat;
            }

            OnIUserAction(InterfaceToSend.IApplicationUI, action, new() {
                ["user"] = user
            });
        }
        public void On_Speaking_Button(bool Join_VoiceChat_Button_state, bool Speak_Button_state)
        {
            if (IsRoomNameNull(out var _))
                throw new UserException("Join the Room");

            if (Speak_Button_state)
            {
                if (!voiceManager.StopSpeaking())
                    throw new UserException("Error in Ending Speaking");
            }
            else
            {
                if (!Join_VoiceChat_Button_state)
                    throw new UserException("Join Voice Channel");

                if (!voiceManager.StartSpeaking())
                    throw new UserException("Error in Starting Speaking");
            }

            OnIUserAction(InterfaceToSend.IApplicationUI, ActionFromIUser.OnSpeakingVoiceChat, new() {
                ["isSpeaking"] = !Speak_Button_state
            });
        }

        public void On_Add_Button(string room_name) => SendMessage(Guid.NewGuid(), CommandText.AddRoom, room_name);
        public void On_Grid_Tags_People_Button(string grid_tags_people_name_pressed) => OnIUserAction(InterfaceToSend.IApplicationUI, ActionFromIUser.OnGridTagsPeopleButton, new() { ["grid_tags_people_name_pressed"] = grid_tags_people_name_pressed });
        public void On_Register_Button_LogPage() => OnIUserAction(InterfaceToSend.IApplicationUI, ActionFromIUser.OnRegisterButtonLogPage, []);
        public void On_Exit_Button_RegPage() => OnIUserAction(InterfaceToSend.IApplicationUI, ActionFromIUser.OnExitButtonRegPage, []);
        public void On_SettingsOpen_Button() => OnIUserAction(InterfaceToSend.IApplicationUI, ActionFromIUser.OnSettingsOpenButton, []);
        public void On_Close_Error_Button(string tag_error) => OnIUserAction(InterfaceToSend.IApplicationUI, ActionFromIUser.OnCloseErrorButton, new() { ["tag_error"] = tag_error });
        public void On_Reload_Connection_Button() => OnIUserAction(InterfaceToSend.IServerConnection, ActionFromIUser.ReloadConnectionServer, []);
    }
}
