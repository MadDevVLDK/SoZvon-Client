using SoZvon.SubClasses;
using SoZvon.UI.My_Controls;
using SoZvon.UI.Room_Pages;
using SoZvon.UI.SubClasses;
using System;
using System.Collections;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace SoZvon.UI
{
    public class SelfSortingStringList : IEnumerable<string>
    {
        readonly SortedSet<string> _sortedSet;
        readonly StringComparer _comparer;

        /// <summary> Инициализирует новый экземпляр списка с сортировкой по умолчанию </summary>
        public SelfSortingStringList() : this(StringComparer.Ordinal) { }

        /// <summary> Инициализирует новый экземпляр списка с указанным сравнением строк </summary>
        public SelfSortingStringList(StringComparer comparer)
        {
            _comparer = comparer ?? StringComparer.Ordinal;
            _sortedSet = new SortedSet<string>(_comparer);
        }

        /// <summary> Количество элементов в списке </summary>
        public int Count => _sortedSet.Count;

        /// <summary> Добавляет элемент в список с автоматической сортировкой </summary>
        /// <returns>True если элемент добавлен, false если уже существует</returns>
        public bool Add(string item) => _sortedSet.Add(item);

        /// <summary> Добавляет коллекцию элементов </summary>
        public void AddRange(IEnumerable<string> items)
        {
            foreach (var item in items)
            {
                _sortedSet.Add(item);
            }
        }

        /// <summary> Удаляет элемент из списка </summary>
        /// <returns>True если элемент удален</returns>
        public bool Remove(string item) => _sortedSet.Remove(item);

        /// <summary> Проверяет наличие элемента в списке </summary>
        /// <returns>True если элемент найден</returns>
        public bool Contains(string item) => _sortedSet.Contains(item);

        /// <summary> Очищает список </summary>
        public void Clear() => _sortedSet.Clear();

        /// <summary> Возвращает первый элемент списка </summary>
        public string? First => _sortedSet.Min;

        /// <summary> Возвращает последний элемент списка </summary>
        public string? Last => _sortedSet.Max;

        /// <summary> Получает элементы в указанном диапазоне </summary>
        /// <returns>Коллекция элементов в диапазоне</returns>
        public IEnumerable<string> GetRange(string start, string end) => _sortedSet.GetViewBetween(start, end);

        /// <summary>Поиск элемента с использованием бинарного поиска </summary>
        /// <returns>Индекс элемента или -1 если не найден</returns>
        public int BinarySearch(string item) => _sortedSet.ToList().BinarySearch(item, _comparer);
        
        /// <summary> Возвращает перечислитель </summary>
        public IEnumerator<string> GetEnumerator() => _sortedSet.GetEnumerator();

        /// <summary> Преобразует список в массив </summary>
        public string[] ToArray() => [.. _sortedSet];

        /// <summary> Преобразует список в List </summary>
        public List<string> ToList() => [.. _sortedSet];

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    // Класс для управления UI-действиями в приложении
    public class My_Actions(IMainWindow mainWindow)
    {
        const string format_time = "HH:mm";
        DateTime lastmsg_date = DateTime.MinValue; // Хранит дату последнего сообщения (отображенного на форме в данный момент)

        readonly RoomPage room_page = mainWindow.room_page; // Страница комнаты
        readonly RoomPanelPage room_panel_page = mainWindow.room_panel_page; // Панель комнат

        readonly SelfSortingStringList rooms_names = [];
        readonly SelfSortingStringList users_logins = [];
        readonly SelfSortingStringList users_logins_voice_chat = [];

        // Методы для работы с комнатами
        public void RoomsAddToPanel(List<Room> rooms_on_server)
        {
            // Проверка на пустой список
            if (rooms_on_server.Count == 0)
            {
                mainWindow.Make_ErrorMessage("Room_Error", "There is no rooms on the server");
                return;
            }

            // Очистка текущего списка комнат на форме
            RoomsDeleteOnPanel();            

            // Создание UI элементов для каждой комнаты
            foreach (Room room in rooms_on_server) 
                RoomAddToPanel(room);
        }
        public void RoomsDeleteOnPanel()
        {
            rooms_names.Clear();
            room_panel_page.All_Rooms.Children.Clear();
        }
        public void RoomAddToPanel(Room room)
        {
            // Проверка на пустую комнату
            if (room is null)
            {
                mainWindow.Make_ErrorMessage("Room_Error", "There is an empty room");
                return;
            }

            string tag = room.Name_Room;

            Grid new_grid = new() { Height = 57, Name = "Room_Name_Button", Margin = new(0, -1, 0, 0), Cursor = Cursors.Hand, Tag = tag };

            // Подключение обработчиков событий мыши
            new_grid.MouseLeftButtonUp += mainWindow.AnyButton_UpMouse;
            new_grid.MouseLeftButtonDown += mainWindow.AnyButton_DownMouse;
            new_grid.MouseEnter += mainWindow.AnyButton_EnterLeaveMouse;
            new_grid.MouseLeave += mainWindow.AnyButton_EnterLeaveMouse;

            if (rooms_names.Contains(tag))
                return;

            rooms_names.Add(tag);
            room_panel_page.All_Rooms.Children.Insert(rooms_names.BinarySearch(tag), new_grid);

            // Добавление фона
            Rectangle rect = new()
            {
                Tag = "Background",
                Fill = new SolidColorBrush(Color.FromRgb(255, 219, 158)),
                StrokeThickness = 1,
                Stroke = new SolidColorBrush(Color.FromRgb(162, 141, 106))
            };

            // Настройка текстового блока с информацией о комнате
            TextBlock textblock = new()
            {
                Tag = "Text",
                FontFamily = new FontFamily("Comic Sans MS"),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                TextAlignment = TextAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Foreground = new SolidColorBrush(Colors.Black),
                TextWrapping = TextWrapping.NoWrap,
                FontSize = 20,
                Margin = new Thickness(10, 10, 10, 10),
                Text = tag + ": " + room.Num_Users
            };

            new_grid.Children.Add(rect);
            new_grid.Children.Add(textblock);

            // Добавление подсказки для обрезанного текста
            TextBlockUtils.SetAutoToolTipOnTrimmedText(textblock, true);

        }
        public void RoomChangeOnPanel(Room room)
        {
            // Проверка на пустую комнату
            if (room is null)
            {
                mainWindow.Make_ErrorMessage("Room_Error", "There is an empty room");
                return;
            }

            if(room_panel_page.All_Rooms.FindElementByTag<Grid>(room.Name_Room) is not Grid new_grid)
                return;

            if (new_grid.FindElementByTag<TextBlock>("Text") is not TextBlock textblock)
                return;

            textblock.Text = $"{room.Name_Room}: {room.Num_Users}";
        }
        public void RoomDeleteOnPanel(string id)
        {
            if (rooms_names.Contains(id))
            {
                room_panel_page.All_Rooms.Children.RemoveAt(rooms_names.BinarySearch(id));
                rooms_names.Remove(id);
            }
        }

        // Методы для работы с клиентами в комнате
        public void UsersAddToPanel(List<Room_User> users_on_room)
        {
            // Добавление пользователей (кто в комнате есть)
            foreach (Room_User user in users_on_room)
                UserAddToPanel(user);
        }
        public void UserAddToPanel(Room_User room_user)
        {
            var user_login = room_user.Login;

            // Добавление пользователей (кто в комнате есть)
            Grid new_grid = new() { Margin = new(0, -1, 0, 0), Tag = user_login };

            if (users_logins.Contains(user_login))
                return;

            users_logins.Add(user_login);
            room_page.All_PeopleRoom.Children.Insert(users_logins.BinarySearch(user_login), new_grid);

            new_grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            new_grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // Фон
            Rectangle rect = new()
            {
                Tag = "Background",
                Fill = new SolidColorBrush(Color.FromRgb(255, 219, 158)),
                StrokeThickness = 1,
                Stroke = new SolidColorBrush(Color.FromRgb(162, 141, 106))
            };
            
            // Настройка текстового блока с именем пользователя
            TextBlock textblock_name = new()
            {
                Tag = "Name",
                Foreground = new SolidColorBrush(Colors.Black),
                TextWrapping = TextWrapping.Wrap,
                FontSize = 20,
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(10, 10, 10, 15),
                Text = room_user.Name,
                FontFamily = new FontFamily("Comic Sans MS")
            };
            TextBlock textblock_texting = new()
            {
                Tag = "Texting_Texblock",
                Foreground = new SolidColorBrush(Colors.Gray),
                TextWrapping = TextWrapping.NoWrap,
                FontSize = 15,
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, -7, 0, 10),
                Text = "(Печатает...)",
                FontFamily = new FontFamily("Comic Sans MS"),
                Visibility = Visibility.Collapsed
            };

            Grid.SetRowSpan(rect, 2);
            Grid.SetRow(rect, 0);
            Grid.SetRow(textblock_name, 0);
            Grid.SetRow(textblock_texting, 1);

            new_grid.Children.Add(rect);
            new_grid.Children.Add(textblock_name);
            new_grid.Children.Add(textblock_texting);

            // Добавление подсказки
            TextBlockUtils.SetAutoToolTipOnTrimmedText(textblock_name, true);
        }
        public void UserDeleteOnPanel(string id)
        {
            if (users_logins.Contains(id))
            {
                room_page.All_PeopleRoom.Children.RemoveAt(users_logins.BinarySearch(id));
                users_logins.Remove(id);
            }
        }
        public void UsersDeleteOnPanel()
        {
            users_logins.Clear();
            users_logins_voice_chat.Clear();
            room_page.All_PeopleRoom.Children.Clear();
            room_page.All_PeopleVoiceChat.Children.Clear();
            room_page.Panel_Users_Tags.Children.Clear();
        }

        public void UsersVoiceChatAddToPanel(List<Room_User> users_on_room)
        {
            // Добавление пользователей в лист в голосовом чате
            var users_with_voice = from user in users_on_room where user.InVoiceChat select user;

            // Добавление пользователей (Кто в ВойсЧате)
            foreach (Room_User user in users_with_voice)
                UserVoiceChatAddToPanel(user);
        }
        public void UserVoiceChatAddToPanel(Room_User room_user)
        {
            var user_login = room_user.Login;

            Grid new_grid = new() { Height = 57, Tag = room_user.Login };

            if (users_logins_voice_chat.Contains(user_login))
                return;

            users_logins_voice_chat.Add(user_login);
            room_page.All_PeopleVoiceChat.Children.Insert(users_logins_voice_chat.BinarySearch(user_login), new_grid);

            // Фон
            Rectangle rect = new()
            {
                Tag = "Background",
                Fill = new SolidColorBrush(Color.FromRgb(255, 219, 158)),
                Margin = new Thickness(0, -1, 0, 0),
                StrokeThickness = 1,
                Stroke = new SolidColorBrush(Color.FromRgb(162, 141, 106))
            };

            // Настройка текстового блока с именем пользователя
            TextBlock textblock = new()
            {
                FontFamily = new FontFamily("Comic Sans MS"),
                Foreground = new SolidColorBrush(Color.FromRgb(0, 0, 0)),
                TextWrapping = TextWrapping.Wrap,
                Language = System.Windows.Markup.XmlLanguage.GetLanguage("ru-ru"),
                FontSize = 20,
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 10, 0, 10),
                Text = room_user.Name
            };

            new_grid.Children.Add(rect);
            new_grid.Children.Add(textblock);

            TextBlockUtils.SetAutoToolTipOnTrimmedText(textblock, true);
        }
        public void UserVoiceChatDeleteOnPanel(string id)
        {
            if (users_logins_voice_chat.Contains(id))
            {
                room_page.All_PeopleVoiceChat.Children.RemoveAt(users_logins_voice_chat.BinarySearch(id));
                users_logins_voice_chat.Remove(id);
            }
        }
        public void UserTexting(Room_User room_user)
        {
            if (room_page.All_PeopleRoom.FindElementByTag<Grid>(room_user.Login) is not Grid user_grid)
                return;

            if (user_grid.FindElementByTag<TextBlock>("Texting_Texblock") is not TextBlock texting_grid)
                return;

            texting_grid.Visibility = Visibility.Visible;

            if(!room_user.HasActionOnTexting())
                room_user.SetActionOnTexting(() => mainWindow.MakeAction_Form(() => texting_grid.Visibility = Visibility.Collapsed));

            room_user.StartTexting();
        }
        
        // Методы для работы с тегами пользователей
        public void ShowPeopleTagsOnPanel(List<Room_User> users_on_room, string text)
        {
            if (users_on_room is null) 
                return;
            
            DeletePeopleTagsOnPanel();

            if (room_page.Textbox_PrivateMsg.IsFocused || room_page.Grid_Users_Tags.IsFocused) 
                room_page.ChangeVisibility_Grid_PeopleTags(Visibility.Visible);

            int visible_tags = 0;

            foreach (Room_User user in users_on_room)
            {
                // Фильтрация пользователей по введенному тексту
                if (text is not null && !user.Login.TrimEnd().TrimStart().Contains(text, StringComparison.CurrentCultureIgnoreCase)) 
                    continue;

                visible_tags++;

                Grid new_grid = new() 
                { 
                    Name = "Grid_Tags_People_Button",
                    Cursor = Cursors.Hand,
                    Margin = new Thickness(0, 5, 0, 0),
                    Height = 40,
                    Focusable = true,
                    Tag = "Tags_People" + user.Login
                };

                new_grid.MouseLeftButtonUp += mainWindow.AnyButton_UpMouse;
                new_grid.MouseLeftButtonDown += mainWindow.AnyButton_DownMouse;
                new_grid.MouseEnter += mainWindow.AnyButton_EnterLeaveMouse;
                new_grid.MouseLeave += mainWindow.AnyButton_EnterLeaveMouse;

                // Фон
                new_grid.Children.Add(new Rectangle 
                { 
                    Tag = "Background",
                    Fill = new SolidColorBrush(Color.FromRgb(240, 240, 240)),
                    Stroke = new SolidColorBrush(Color.FromRgb(156, 156, 156)),
                    RadiusX = 11.5,
                    RadiusY = 11.5
                });
                
                // Панелька, на которой все элементы пользователя будут
                StackPanel stackpanel = new() 
                { 
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    Margin = new Thickness(0, 2, 0, 2)
                };
                
                // Аватарка пользователя (еще только наработка)
                stackpanel.Children.Add(new Ellipse 
                { 
                    Height = 32,
                    Width = 32,
                    StrokeThickness = 0.7,
                    Fill = new SolidColorBrush(Colors.White),
                    Stroke = new SolidColorBrush(Colors.Black),
                    Margin = new Thickness(10, 0, 0, 0)
                });

                // Имя пользователя
                stackpanel.Children.Add(new TextBlock 
                {
                    Text = user.Name,
                    IsEnabled = false,
                    Height = 24,
                    FontSize = 15,
                    TextAlignment = TextAlignment.Left,
                    Margin = new Thickness(10, 0, 0, 0),
                    FontFamily = new FontFamily("Comic Sans MS"),
                    Background = new SolidColorBrush(Colors.Transparent),
                    Foreground = new SolidColorBrush(Colors.Black)
                });
                
                // Логин пользователя
                stackpanel.Children.Add(new TextBlock 
                {
                    Text = "@ " + user.Login,
                    IsEnabled = false,
                    FontSize = 15,
                    Height = 24,
                    FontFamily = new FontFamily("Comic Sans MS"),
                    Foreground = new SolidColorBrush(Color.FromRgb(93, 93, 93)),
                    TextAlignment = TextAlignment.Left,
                    Margin = new Thickness(10, 0, 20, 0),
                    Background = new SolidColorBrush(Colors.Transparent),
                    Tag = "Text"
                });
                
                new_grid.Children.Add(stackpanel);

                // Добавление на форму
                room_page.Panel_Users_Tags.Children.Add(new_grid);
            }

            // Если логинов нет никаких
            Grid grid = new()
            {
                Tag = "NoUserTags",
                Margin = new Thickness(0, 5, 0, 0),
                Height = 40,
                Focusable = false,
                Visibility = visible_tags == 0 ? Visibility.Visible : Visibility.Collapsed
            };

            grid.Children.Add(new Rectangle
            {
                Tag = "Background",
                Fill = new SolidColorBrush(Color.FromRgb(240, 240, 240)),
                Stroke = new SolidColorBrush(Color.FromRgb(156, 156, 156)),
                RadiusX = 11.5,
                RadiusY = 11.5
            });

            StackPanel stack = new()
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(0, 2, 0, 2)
            };

            stack.Children.Add(new TextBlock
            {
                Text = "Нет логинов, удовлетворяющих запросу",
                IsEnabled = false,
                Height = 24,
                FontSize = 15,
                TextAlignment = TextAlignment.Left,
                Margin = new Thickness(10, 0, 10, 0),
                FontFamily = new FontFamily("Comic Sans MS"),
                Foreground = new SolidColorBrush(Colors.Black),
                Background = new SolidColorBrush(Colors.Transparent)
            });

            grid.Children.Add(stack);

            room_page.Panel_Users_Tags.Children.Add(grid);
        }
        public void UpdatePeopleTagsOnPanel(string text)
        {
            if (room_page.Textbox_PrivateMsg.IsFocused || room_page.Grid_Users_Tags.IsFocused)
                room_page.ChangeVisibility_Grid_PeopleTags(Visibility.Visible);

            int visible_tags = 0;

            foreach (Grid userTag_grid in room_page.Panel_Users_Tags.Children)
            {
                if (userTag_grid.Tag is not string _tag || !_tag.StartsWith("Tags_People"))
                    continue;

                if (userTag_grid.FindElementByTag<TextBlock>("Text") is not TextBlock userTag_textblock)
                    continue;

                if (userTag_textblock.Tag is not string tag)
                    continue;

                if (tag.Contains(text!, StringComparison.CurrentCultureIgnoreCase))
                {
                    userTag_grid.Visibility = Visibility.Visible;
                    visible_tags++;
                }
                else userTag_grid.Visibility = Visibility.Collapsed;
            }

            if (room_page.Panel_Users_Tags.FindElementByTag<Grid>("NoUserTags") is not Grid nouserTag_grid)
                return;

            nouserTag_grid.Visibility = visible_tags == 0 ? Visibility.Visible : Visibility.Collapsed;
        }
        public void DeletePeopleTagsOnPanel() => room_page.Panel_Users_Tags.Children.Clear();

        // Проверяет, нужно ли вставить дату перед показом сообщения
        public void Check_DATE(DateTime dateTime, MessageOnScreen message)
        {
            if (message is MessageOnScreen.SERVER or MessageOnScreen.CLIENT)
            {
                if (lastmsg_date.Day != dateTime.Day)
                {
                    Show_DATE_MessageOnScreen(Guid.NewGuid(), dateTime);
                    lastmsg_date = dateTime;
                }
            }
            else if (message is MessageOnScreen.MY)
            {
                if (dateTime != DateTime.MinValue)
                {
                    if (lastmsg_date.Day != dateTime.Day) Show_DATE_MessageOnScreen(Guid.NewGuid(), dateTime);

                    lastmsg_date = dateTime;
                }
                else
                {
                    if (lastmsg_date.Day != DateTime.Now.Day)
                    {
                        Show_DATE_MessageOnScreen(Guid.NewGuid(), DateTime.Now);
                        lastmsg_date = DateTime.Now;
                    }
                }
            }
        }

        // Методы для отображения сообщений
        public void Show_CLIENT_MessageOnScreen(DateTime dateTime, Guid guid, Color login_color, string text, string sender, My_FileInfo[] filesInfos, MessageFromUser IsPublic = MessageFromUser.Public)
        {
            Color background_color = Color.FromRgb(255, 255, 255);

            Check_DATE(dateTime, MessageOnScreen.CLIENT);
            lastmsg_date = dateTime;

            if (IsPublic is MessageFromUser.Private)
            {
                background_color = Color.FromRgb(230, 230, 230);
                sender = "(Личное) " + sender;
            }

            Grid msgGrid = new()
            {
                MinWidth = 90,
                MaxWidth = 450,
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(0, 5, 0, 0),
                Uid = guid.ToString()
            };

            msgGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto, MaxHeight = 500 }); // Для картинок
            msgGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Для отправителя
            msgGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Для My_TextBox (сообщения)
            msgGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Для времени

            StackPanel? filesContainer = null;

            if (filesInfos is not null && filesInfos.Length > 0)
            {
                filesContainer = new StackPanel { Orientation = Orientation.Vertical };
                filesContainer.Children.Add(mainWindow.CreateFilesContainer([.. filesInfos]));
            }

            Rectangle background = new() 
            { 
                Tag = "Background",
                RadiusX = 15,
                RadiusY = 15,
                Fill = new SolidColorBrush(background_color),
                StrokeThickness = 1,
                Stroke = new SolidColorBrush(Color.FromRgb(162, 141, 106))
            };
            TextBlock sender_name = new() 
            {
                Tag = "Sender",
                Text = sender,
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Top,
                Padding = new(14, 7, 10, 0),
                Foreground = new SolidColorBrush(login_color) 
            };
            My_TextBox textBox = new(My_TextBox_Type.Messages_Form)
            {
                Tag = "Text",
                Text = text,
                FontFamily = new FontFamily("Comic Sans MS"),
                FontSize = 15,
                IsReadOnly = true,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                TextAlignment = TextAlignment.Left,
                Padding = new(10, 5, 12, 0),
            };
            TextBlock time = new() 
            { 
                Tag = "Time",
                Text = dateTime.ToString(format_time),
                FontSize = 10,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Bottom,
                Padding = new Thickness(0, 4, 14, 5),
                Foreground = new SolidColorBrush(Color.FromRgb(234, 75, 133))
            };

            textBox.txtTextbox.IsTabStop = false;

            Grid.SetRow(background, 0);
            Grid.SetRowSpan(background, 4);
            if (filesContainer is not null) Grid.SetRow(filesContainer, 0);
            Grid.SetRow(sender_name, 1);
            Grid.SetRow(textBox, 2);
            Grid.SetRow(time, 3);

            msgGrid.Children.Add(background);
            if (filesContainer is not null) msgGrid.Children.Add(filesContainer);
            msgGrid.Children.Add(sender_name);
            msgGrid.Children.Add(textBox);
            msgGrid.Children.Add(time);

            textBox.UpdateRichTextBoxWidth();

            room_page.MessagesPanel.Children.Add(msgGrid);

            room_page.Scroll_Bar_All_Messages.ScrollToEnd();
        } // СООБЩЕНИЕ ОТ ДРУГИХ
        public void Show_MY_MessageOnScreen(DateTime date, Guid guid, string text, string reciever, My_FileInfo[] files_pathes)
        {
            string time_text = Encoding.UTF8.GetString("🗴"u8.ToArray());
            Color background_color = Color.FromRgb(245, 242, 255);

            if (date != DateTime.MinValue) 
                time_text = date.ToString("HH:mm");

            Check_DATE(date, MessageOnScreen.MY);

            if (reciever != "")
            {
                reciever = "(Личное) " + reciever;
                background_color = Color.FromRgb(228, 228, 228);
            }

            Grid msgGrid = new()
            {
                MinWidth = 125,
                MaxWidth = 450,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 5, 0, 0),
                Uid = guid.ToString()
            };

            msgGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto, MaxHeight = 500 }); // Для картинок
            msgGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Для никнейма
            msgGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Для текста
            msgGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Для времени

            Rectangle backgroundRect = new()
            {
                Tag = "Background",
                RadiusX = 15,
                RadiusY = 15,
                Fill = new SolidColorBrush(background_color),
                StrokeThickness = 1,
                Stroke = new SolidColorBrush(Color.FromRgb(162, 141, 106))
            };

            StackPanel? imageContainer = null;

            if (files_pathes is not null && files_pathes.Length > 0)
            {
                imageContainer = new StackPanel { Orientation = Orientation.Vertical };
                imageContainer.Children.Add(mainWindow.CreateFilesContainer([.. files_pathes]));
            }

            TextBlock? recieverText = null;

            if (reciever != "")
            {
                recieverText = new TextBlock
                {
                    Tag = "Reciever",
                    Text = reciever,
                    FontSize = 12,
                    VerticalAlignment = VerticalAlignment.Top,
                    Padding = new Thickness(14, 7, 10, 0),
                    Foreground = new SolidColorBrush(Color.FromRgb(68, 87, 251))
                };
            }

            My_TextBox textBox = new()
            {
                Tag = "Text",
                Text = text,
                FontFamily = new FontFamily("Comic Sans MS"),
                FontSize = 15,
                IsReadOnly = true,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                TextAlignment = TextAlignment.Left,
                Padding = new Thickness(10, 5, 12, 0)
            };

            textBox.txtTextbox.IsTabStop = false;

            TextBlock timeText = new()
            {
                Tag = "Time",
                Text = time_text,
                FontSize = 10,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Bottom,
                Padding = new Thickness(0, 4, 14, 5),
                Foreground = new SolidColorBrush(Color.FromRgb(234, 75, 133))
            };

            if (date != DateTime.MinValue)
            {
                timeText.FontSize = 10;
                timeText.Foreground = new SolidColorBrush(Color.FromRgb(234, 75, 133));
            }
            else
            {
                timeText.FontSize = 16;
                timeText.Foreground = new SolidColorBrush(Color.FromRgb(255, 69, 69));
            }

            Grid.SetRow(backgroundRect, 0);
            Grid.SetRowSpan(backgroundRect, 4);
            if(imageContainer is not null) Grid.SetRow(imageContainer, 0);
            if (recieverText is not null) Grid.SetRow(recieverText, 1);
            Grid.SetRow(textBox, 2);
            Grid.SetRow(timeText, 3);

            msgGrid.Children.Add(backgroundRect);
            if (imageContainer is not null) msgGrid.Children.Add(imageContainer);
            if (recieverText is not null) msgGrid.Children.Add(recieverText);
            msgGrid.Children.Add(textBox);
            msgGrid.Children.Add(timeText);

            textBox.UpdateRichTextBoxWidth();

            room_page.MessagesPanel.Children.Add(msgGrid);
            room_page.Scroll_Bar_All_Messages.ScrollToEnd();
        } // СООБЩЕНИЕ ОТ МЕНЯ
        public void Show_DATE_MessageOnScreen(Guid guid, DateTime date)
        {
            int minHeight = 35;
            Color background_color = Color.FromRgb(255, 255, 255);
            Thickness padding = new(11, 3, 12, 3);

            Grid msgGrid = new() 
            { 
                MinHeight = minHeight,
                VerticalAlignment = VerticalAlignment.Bottom,
                HorizontalAlignment = HorizontalAlignment.Center,
                MaxWidth = 366,
                MinWidth = 125,
                Margin = new Thickness(0, 5, 0, 0),
                Uid = guid.ToString()
            };

            msgGrid.RowDefinitions.Add(new RowDefinition() { Height = GridLength.Auto });

            Rectangle background = new()
            {
                Tag = "Background",
                RadiusX = 15,
                RadiusY = 15,
                Fill = new SolidColorBrush(background_color),
                StrokeThickness = 1,
                Stroke = new SolidColorBrush(Color.FromRgb(162, 141, 106))
            };
            My_TextBox textbox = new(My_TextBox_Type.Messages_Form)
            {
                Tag = "Text",
                Text = date.ToString("d MMMM yyyy"),
                TabIndex = -1,
                FontSize = 15,
                IsReadOnly = true,
                AcceptsTab = false,
                IsTabStop = false,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                TextAlignment = TextAlignment.Center,
                Padding = padding,
            };

            textbox.txtTextbox.AcceptsTab = false;
            textbox.txtTextbox.IsTabStop = false;
            textbox.txtTextbox.TabIndex = -1;

            Grid.SetRow(background, 0);
            Grid.SetRow(textbox, 0);

            msgGrid.Children.Add(background);
            msgGrid.Children.Add(textbox);

            textbox.UpdateRichTextBoxWidth();

            room_page.MessagesPanel.Children.Add(msgGrid);

            room_page.Scroll_Bar_All_Messages.ScrollToEnd();
        } // СООБЩЕНИЕ ОТ СЕРВЕРА
        public void Show_SERVER_MessageOnScreen(Guid guid, DateTime date, string text)
        {
            int minHeight = 35;
            Color background_color = Color.FromRgb(255, 255, 255);
            Thickness padding = new(11, 3, 12, 3);

            Check_DATE(date, MessageOnScreen.SERVER);

            Grid msgGrid = new() 
            { 
                MinHeight = minHeight,
                VerticalAlignment = VerticalAlignment.Bottom,
                HorizontalAlignment = HorizontalAlignment.Center,
                MaxWidth = 366,
                MinWidth = 125,
                Margin = new Thickness(0, 5, 0, 0),
                Uid = guid.ToString()
            };

            msgGrid.RowDefinitions.Add(new RowDefinition() { Height = GridLength.Auto });

            Rectangle background = new()
            {
                Tag = "Background",
                RadiusX = 15,
                RadiusY = 15,
                Fill = new SolidColorBrush(background_color),
                StrokeThickness = 1,
                Stroke = new SolidColorBrush(Color.FromRgb(162, 141, 106))
            };
            My_TextBox textbox = new(My_TextBox_Type.Messages_Form)
            {
                Tag = "Text",
                Text = text,
                FontFamily = new FontFamily("Comic Sans MS"),
                FontSize = 15,
                IsReadOnly = true,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                TextAlignment = TextAlignment.Center,
                Padding = padding,
            };

            textbox.txtTextbox.IsTabStop = false;

            Grid.SetRow(background, 0);
            Grid.SetRow(textbox, 0);

            msgGrid.Children.Add(background);
            msgGrid.Children.Add(textbox);

            textbox.UpdateRichTextBoxWidth();

            room_page.MessagesPanel.Children.Add(msgGrid);

            room_page.Scroll_Bar_All_Messages.ScrollToEnd();
        } // СООБЩЕНИЕ ОТ СЕРВЕРА
        public void DeleteMessagesOnScreen() => room_page.MessagesPanel.Children.Clear();

        // Открывает форму с регистрацией, с входом или не показывает вовсе форму
        public void Navigate_MainFrame_To(Page_Type page)
        {
            if (page is Page_Type.None)
            {
                mainWindow.mainFrame_ref.Navigate(null);
            }
            else if (page is Page_Type.RegisterPage)
            {
                mainWindow.mainFrame_ref.Navigate(mainWindow.register_page);
            }
            else if (page is Page_Type.LogInPage)
            {
                mainWindow.mainFrame_ref.Navigate(mainWindow.login_page);
            }
            else throw new ArgumentException("not supported page (Navigate_MainFrame_To)");

            mainWindow.mainFrame_ref.Visibility = page is Page_Type.None ? Visibility.Collapsed : Visibility.Visible;
        }
        public void Navigate_Panels_To(Panel_Type page_to_set)
        {
            var (leftPage, rightPage) = page_to_set switch
            {
                Panel_Type.RoomPanels => (Page_Type.RoomPanelPage, Page_Type.RoomPage),
                Panel_Type.SettingsPanels => (Page_Type.TitleSettingsPage, Page_Type.SettingsPage),
                _ => throw new ArgumentException("not supported page (Navigate_LeftPanel_To)")
            };

            Navigate_LeftPanel_To(leftPage);
            Navigate_RightPanel_To(rightPage);
        }
        public void Navigate_LeftPanel_To(Page_Type page_to_set)
        {
            Page? page = page_to_set switch
            {
                Page_Type.RoomPanelPage => mainWindow.room_panel_page,
                Page_Type.TitleSettingsPage => mainWindow.titleSettings_page,
                _ => throw new Exception("not supported page (Navigate_LeftPanel_To)")
            };

            mainWindow.leftPanel_ref.Navigate(page);
            mainWindow.leftPanel_ref.Visibility = Visibility.Visible;
        }
        public void Navigate_RightPanel_To(Page_Type page_to_set)
        {
            Page? page = page_to_set switch
            {
                Page_Type.RoomPage => mainWindow.room_page,
                Page_Type.SettingsPage => mainWindow.settings_page,
                _ => throw new Exception("not supported page (Navigate_RightPanel_To)")
            };

            mainWindow.rightPanel_ref.Navigate(page);
            mainWindow.rightPanel_ref.Visibility = Visibility.Visible;
        }

        public void OnExitRoom()
        {
            UsersDeleteOnPanel();
            DeleteMessagesOnScreen();
            DeletePeopleTagsOnPanel();

            lastmsg_date = DateTime.MinValue;
        }

        // Полный сброс интерфейса
        public void DeleteAll()
        {
            DeletePeopleTagsOnPanel();
            RoomsDeleteOnPanel();
            UsersDeleteOnPanel();
            DeleteMessagesOnScreen();
            mainWindow.ResetButtonsAppearence();
        }
    }

    // Перечисления для классификации сообщений и страниц
    public enum MessageOnScreen
    {
        CLIENT,   // Сообщение от другого клиента
        SERVER,   // Серверное сообщение
        MY        // Собственное сообщение
    }
    public enum Page_Type : byte
    {
        None,           // Нет страницы
        StartPage,       // Стартовая страница
        RegisterPage,    // Страница регистрации
        LogInPage,       // Страница входа
        RoomPage,        // Страница комнаты
        RoomPanelPage,    // Панель списка комнат
        TitleSettingsPage,    // Панель с заголовками настроек
        SettingsPage,    // Панель с настройками
    }
    public enum Panel_Type : byte { RoomPanels, SettingsPanels }
}
