using SoZvon.UI.SubClasses;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace SoZvon.UI
{
    public enum Button_Color_Type : byte { Light = 0, Medium = 1, Strong = 2 }
    public interface IButtonInfo
    {
        Color CurrentColor(Button_Color_Type type);
    }
    public record InfoButton_With_State(Dictionary<bool, string> Texts, Dictionary<Button_Color_Type, Color> Colors, Dictionary<Button_Color_Type, Color> Colors_Additional) : InfoButton(Colors)
    {
        public bool State
        {
            get { lock (lock_state) return _state; }
            set { lock (lock_state) _state = value; }
        }
        public string CurrentText => Texts[State];

        private bool _state = false;
        private readonly object lock_state = new();
        internal override Dictionary<Button_Color_Type, Color> CurrentColorDict()
        {
            if (State) return Colors_Additional;
            else return Colors;
        }
    }
    public record InfoButton(Dictionary<Button_Color_Type, Color> Colors) : IButtonInfo
    {
        public Color CurrentColor(Button_Color_Type type) => CurrentColorDict()[type];
        internal virtual Dictionary<Button_Color_Type, Color> CurrentColorDict() => Colors;
    }

    public partial class My_Buttons
    {
        public bool CanPressButton = true;

        Dictionary<string, IButtonInfo> buttons = null!;
        Dictionary<string, IButtonInfo> buttons_with_state = null!;
        readonly IMainWindow mainWindow;

        string _active_button = "";
        string _active_button_room = "";

        readonly object lock_state = new();
        readonly object lock_buttons_dict = new();

        public string ActiveButton
        {
            get { lock (lock_state) return _active_button; }
            set { lock (lock_state) _active_button = value; }
        }
        public string ActiveButton_Room
        {
            get { lock (lock_state) return _active_button_room; }
            set { lock (lock_state) _active_button_room = value; }
        }
        
        public My_Buttons(IMainWindow mainWindow_)
        {
            mainWindow = mainWindow_;
            Init_Buttons_Info();
            Init_Buttons_With_State_Info();
        }

        void Init_Buttons_Info()
        {
            buttons = new Dictionary<string, IButtonInfo>
            {
                {
                    "Login_Button", new InfoButton(new()
                    {
                        [Button_Color_Type.Light] = Color.FromRgb(255, 221, 58),
                        [Button_Color_Type.Medium] = Color.FromRgb(241, 211, 67),
                        [Button_Color_Type.Strong] = Color.FromRgb(230, 197, 40)
                    })
                },

                {
                    "Reload_Connection_Button", new InfoButton(new()
                    {
                        [Button_Color_Type.Light] = Color.FromRgb(255, 250, 224),
                        [Button_Color_Type.Medium] = Color.FromRgb(208, 204, 183),
                        [Button_Color_Type.Strong] = Color.FromRgb(175, 172, 152)
                    })
                },

                {
                    "SettingsOpen_Button", new InfoButton(new()
                    {
                        [Button_Color_Type.Light] = Color.FromRgb(255, 250, 224),
                        [Button_Color_Type.Medium] = Color.FromRgb(208, 204, 183),
                        [Button_Color_Type.Strong] = Color.FromRgb(175, 172, 152)
                    })
                },

                {
                    "Register_Button_LogPage", new InfoButton(new()
                    {
                        [Button_Color_Type.Light] = Color.FromRgb(253, 232, 134),
                        [Button_Color_Type.Medium] = Color.FromRgb(230, 214, 125),
                        [Button_Color_Type.Strong] = Color.FromRgb(220, 202, 116)
                    })
                },

                {
                    "Register_Button_RegPage", new InfoButton(new()
                    {
                        [Button_Color_Type.Light] = Color.FromRgb(253, 232, 134),
                        [Button_Color_Type.Medium] = Color.FromRgb(230, 214, 125),
                        [Button_Color_Type.Strong] = Color.FromRgb(220, 202, 116)
                    })
                },

                {
                    "Exit_Button_RegPage", new InfoButton(new()
                    {
                        [Button_Color_Type.Light] = Color.FromRgb(255, 228, 155),
                        [Button_Color_Type.Medium] = Color.FromRgb(230, 204, 134),
                        [Button_Color_Type.Strong] = Color.FromRgb(204, 180, 116)
                    })
                },

                {
                    "Add_Room", new InfoButton(new()
                    {
                        [Button_Color_Type.Light] = Color.FromRgb(226, 176, 92),
                        [Button_Color_Type.Medium] = Color.FromRgb(214, 167, 88),
                        [Button_Color_Type.Strong] = Color.FromRgb(189, 147, 79)
                    })
                },

                {
                    "Delete_Room", new InfoButton(new()
                    {
                        [Button_Color_Type.Light] = Color.FromRgb(236, 118, 63),
                        [Button_Color_Type.Medium] = Color.FromRgb(222, 105, 70),
                        [Button_Color_Type.Strong] = Color.FromRgb(204, 79, 43)
                    })
                },

                {
                    "Close_Error", new InfoButton(new()
                    {
                        [Button_Color_Type.Light] = Color.FromRgb(236, 226, 201),
                        [Button_Color_Type.Medium] = Color.FromRgb(218, 209, 188),
                        [Button_Color_Type.Strong] = Color.FromRgb(189, 181, 163)
                    })
                },

                {
                    "Grid_Tags_People_Button", new InfoButton(new()
                    {
                        [Button_Color_Type.Light] = Color.FromRgb(240, 240, 240),
                        [Button_Color_Type.Medium] = Color.FromRgb(222, 222, 222),
                        [Button_Color_Type.Strong] = Color.FromRgb(195, 195, 195)
                    })
                },

                {
                    "Room_Name_Button", new InfoButton(new()
                    {
                        [Button_Color_Type.Light] = Color.FromRgb(255, 219, 158),
                        [Button_Color_Type.Medium] = Color.FromRgb(245, 208, 145),
                        [Button_Color_Type.Strong] = Color.FromRgb(239, 195, 119)
                    })
                }
            };
        }
        void Init_Buttons_With_State_Info()
        {
            buttons_with_state = new Dictionary<string, IButtonInfo>
            {
                {
                    "Room_Button",

                    new InfoButton_With_State(
                        Texts: new()
                        {
                            [false] = "Войти в комнату",
                            [true] = "Выйти из комнаты"
                        },

                        Colors: new()
                        {
                            [Button_Color_Type.Light] = Color.FromRgb(236, 172, 63),
                            [Button_Color_Type.Medium] = Color.FromRgb(220, 166, 74),
                            [Button_Color_Type.Strong] = Color.FromRgb(214, 154, 49)
                        },

                        Colors_Additional: new()
                        {
                            [Button_Color_Type.Light] = Color.FromRgb(236, 118, 63),
                            [Button_Color_Type.Medium] = Color.FromRgb(222, 105, 70),
                            [Button_Color_Type.Strong] = Color.FromRgb(204, 79, 43)
                        }
                    )
                },

                {
                    "Join_VoiceChat_Button",

                    new InfoButton_With_State(
                        Texts: new()
                        {
                            [false] = "Войти",
                            [true] = "Выйти",
                        },

                        Colors: new()
                        {
                            [Button_Color_Type.Light] = Color.FromRgb(236, 172, 63),
                            [Button_Color_Type.Medium] = Color.FromRgb(220, 166, 74),
                            [Button_Color_Type.Strong] = Color.FromRgb(214, 154, 49)
                        },

                        Colors_Additional: new()
                        {
                            [Button_Color_Type.Light] = Color.FromRgb(236, 118, 63),
                            [Button_Color_Type.Medium] = Color.FromRgb(222, 105, 70),
                            [Button_Color_Type.Strong] = Color.FromRgb(204, 79, 43)
                        }
                    )
                },

                {
                    "Speak_Button",

                    new InfoButton_With_State(
                        Texts: new()
                        {
                            [false] = "Говорить",
                            [true] = "Молчать"
                        },

                        Colors: new()
                        {
                            [Button_Color_Type.Light] = Color.FromRgb(236, 172, 63),
                            [Button_Color_Type.Medium] = Color.FromRgb(220, 166, 74),
                            [Button_Color_Type.Strong] = Color.FromRgb(214, 154, 49)
                        },

                        Colors_Additional: new()
                        {
                            [Button_Color_Type.Light] = Color.FromRgb(236, 118, 63),
                            [Button_Color_Type.Medium] = Color.FromRgb(222, 105, 70),
                            [Button_Color_Type.Strong] = Color.FromRgb(204, 79, 43)
                        }
                    )
                }
            };
        }
        
        public string Get_Active_Button_Room() => ActiveButton_Room;
        public void Set_Active_Button_Room(string room_name) => ActiveButton_Room = room_name;
        
        public string Get_Active_Button() => ActiveButton;
        public void Set_Active_Button(string room_name) => ActiveButton = room_name;

        public bool Get_Button_State(string button_name, out bool state)
        {
            lock (lock_buttons_dict)
            {
                if (buttons_with_state.TryGetValue(button_name, out var value) && value is InfoButton_With_State infoButton)
                {
                    state = infoButton.State;
                    return true;
                }
            }

            state = false;
            return false;
        }
        public void Change_Button_State(string button_name, bool state = false)
        {
            lock (lock_buttons_dict)
            {
                if (buttons_with_state.TryGetValue(button_name, out var value) && value is InfoButton_With_State infoButton)
                    infoButton.State = state;
            }
        }

        public bool Get_Button_Appearence(string name_button, Button_Color_Type button_color_type, out Color color, out string? text)
        {
            text = null;
            color = Color.FromRgb(0, 0, 0);

            lock (lock_buttons_dict)
            {
                if (buttons.TryGetValue(name_button, out var button))
                {
                    color = button.CurrentColor(button_color_type);
                    return true;
                }
                else if(buttons_with_state.TryGetValue(name_button, out var button_with_state) && button_with_state is InfoButton_With_State infoButton)
                {
                    color = infoButton.CurrentColor(button_color_type);
                    text = infoButton.CurrentText;
                    return true;
                }
            }

            return true;
        }

        //Нужно запускать в потоке UI, а то ошибка будет
        public static bool Change_Button_Appearence(Grid? button_grid, Color color, string? text = null)
        {   
            if (button_grid is not null)
            {
                if (button_grid.FindElementByTag<Rectangle>("Background") is Rectangle rectangle) 
                    rectangle.Fill = new SolidColorBrush(color);

                if (text is not null && button_grid.FindElementByTag<TextBlock>("Text") is TextBlock textblock) 
                    textblock.Text = text;

                return true;
            }
            
            return false;
        }

        public void Fast_Button_Appearence_Change(string button_name, Button_Color_Type button_color_type_to_change, string button_tag = "")
        {
            if (mainWindow.FindButtonGrid(button_name, button_tag) is not Grid button_grid)
                return;

            Get_Button_Appearence(button_name, button_color_type_to_change, out Color color, out _);
            Change_Button_Appearence(button_grid, color);
        }
        public void Fast_Button_Appearence_Change(string button_name, Button_Color_Type button_color_type_to_change, bool button_state_to_change)
        {
            if (mainWindow.FindButtonGrid(button_name) is not Grid button_grid)
                return;

            Change_Button_State(button_name, button_state_to_change);
            Get_Button_Appearence(button_name, button_color_type_to_change, out Color color, out string? text);
            Change_Button_Appearence(button_grid, color, text);
        }

        public void ResetButtonsAppearence()
        {
            foreach (var button_name in buttons_with_state.Keys.ToList())
                Fast_Button_Appearence_Change(button_name, Button_Color_Type.Light, false);
        }
    }
}
