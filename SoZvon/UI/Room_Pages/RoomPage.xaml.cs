using SoZvon.SubClasses;
using SoZvon.UI.My_Controls;
using SoZvon.UI.SubClasses;
using System;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace SoZvon.UI.Room_Pages
{
    public partial class RoomPage : Page
    {
        readonly My_Timer my_TextingTimer = new(3);

        const int Min_PeoplePanel_Size = 250;
        double VoicePanel_percentage = 0.2;

        bool TabPressed = false;
        bool IsEntered_TagsPeople_TextBox_Grid = false;

        private readonly SemaphoreSlim _fileLock = new(1, 1);
        bool FilesLoading = false;

        IMainWindow mainWindow;

        // Стартовое состояние страницы
        public void StartProperties(IMainWindow mainWindow_)
        {
            mainWindow = mainWindow_;

            InitializeComponent();

            Join_VoiceChat_Button.MouseUp += mainWindow.AnyButton_UpMouse;
            Join_VoiceChat_Button.MouseDown += mainWindow.AnyButton_DownMouse;
            Join_VoiceChat_Button.MouseEnter += mainWindow.AnyButton_EnterLeaveMouse;
            Join_VoiceChat_Button.MouseLeave += mainWindow.AnyButton_EnterLeaveMouse;

            Speak_Button.MouseUp += mainWindow.AnyButton_UpMouse;
            Speak_Button.MouseDown += mainWindow.AnyButton_DownMouse;
            Speak_Button.MouseEnter += mainWindow.AnyButton_EnterLeaveMouse;
            Speak_Button.MouseLeave += mainWindow.AnyButton_EnterLeaveMouse;

            Grid_PrivateMsg.MouseEnter += (_, _) => IsEntered_TagsPeople_TextBox_Grid = true;
            Grid_PrivateMsg.MouseLeave += (_, _) => IsEntered_TagsPeople_TextBox_Grid = false;

            Textbox_PrivateMsg.LostFocus += IsFocusable_TagTextblock;
            Textbox_PrivateMsg.GotFocus += IsFocusable_TagTextblock;

            Grid_Users_Tags.MouseEnter += (_, _) => IsEntered_TagsPeople_TextBox_Grid = true;
            Grid_Users_Tags.MouseLeave += (_, _) => IsEntered_TagsPeople_TextBox_Grid = false;

            Grid_Users_Tags.LostFocus += IsFocusable_TagTextblock;
            Grid_Users_Tags.GotFocus += IsFocusable_TagTextblock;

            HideRoomInfoPanel.MouseUp += (_, _) => My_Animations.VoicePanel_Animation(this, HideRoomInfoPanel);
        }
        
        void TagGrid_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key is Key.Tab && Grid_Users_Tags.IsFocused)
            {
                Textbox_PrivateMsg.Focus();
                Keyboard.Focus(Textbox_PrivateMsg);
                e.Handled = true;
            }
        }
        void TextBox_Messages_ChangedText(object sender, TextChangedEventArgs e)
        {
            ProcessTextChanged();

            if (!my_TextingTimer.IsWorking && sender is My_TextBox textBox && !string.IsNullOrEmpty(textBox.Text))
            {
                mainWindow.MakeNotificationServer(TypeNotification.Texting, []);
                my_TextingTimer.Start();
            }
        }
        void Page_PreviewKeyDown(object sender, KeyEventArgs e) => TabPressed = (e.Key is Key.Tab);

        void SlideThumb_DragDelta(object sender, DragDeltaEventArgs e)
        {
            double newHeight_VoicePanel = VoicePanel.ActualHeight - e.VerticalChange;

            if ((PeoplePanel.ActualHeight + e.VerticalChange < Min_PeoplePanel_Size && e.VerticalChange < 0) || newHeight_VoicePanel < Min_PeoplePanel_Size)
                newHeight_VoicePanel = VoicePanel.ActualHeight;

            VoicePanel_percentage = Math.Round(VoicePanel.ActualWidth / MainGrid_RoomInfo.ActualWidth, 4);

            PeoplePanel.Margin = new Thickness(PeoplePanel.Margin.Left, PeoplePanel.Margin.Top, PeoplePanel.Margin.Right, newHeight_VoicePanel);
            VoicePanel.Height = newHeight_VoicePanel;
        }
        void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            double new_height = Math.Max(VoicePanel_percentage * e.NewSize.Height, Min_PeoplePanel_Size);

            if (e.NewSize.Height - new_height < Min_PeoplePanel_Size) 
                new_height = Min_PeoplePanel_Size;

            PeoplePanel.Margin = new Thickness(PeoplePanel.Margin.Left, PeoplePanel.Margin.Top, PeoplePanel.Margin.Right, new_height);
            VoicePanel.Height = new_height;
        }

        public void ChangedText_Hints(object sender, TextChangedEventArgs e)
        {
            if (sender is not TextBox textBox) 
                return;

            if (FindName(textBox.Name + "_Hint") is not TextBlock hint_textBlock)
                return;

            hint_textBlock.Visibility = textBox.Text != "" ? Visibility.Collapsed : Visibility.Visible;

            mainWindow.TextBoxTextChange(textBox.Text);

            e.Handled = false;
        }
        public void IsFocusable_TagTextblock(object sender, RoutedEventArgs e)
        {
            mainWindow.IsFocusable_TagTextblock(sender, e.RoutedEvent.Name == "GotFocus", IsEntered_TagsPeople_TextBox_Grid, TabPressed);
            TabPressed = false;
        }
        public void Textbox_PrivateMsg_IsEnabled(bool state)
        {
            Textbox_PrivateMsg.IsEnabled = state;

            if(!state) Textbox_PrivateMsg_Hint.Visibility = Visibility.Visible;
        }
        public void Textbox_IsEnabled(bool state) => TextBox.IsEnabled = state;
        // Функции срабатывающие при определенных действиях на форме
        public void OnEnterRoom(string room_name)
        {
            Textbox_PrivateMsg_IsEnabled(true);
            Textbox_IsEnabled(true);

            Textblock_EnterRoomFirst.Visibility = Visibility.Hidden;
            RoomName_OnTop.Text = "Комната: " + room_name;

            Grid_Users_Tags.Visibility = Visibility.Hidden;
        }
        public void OnExitRoom()
        {
            Textbox_PrivateMsg_IsEnabled(false);
            Textbox_IsEnabled(false);

            Textblock_EnterRoomFirst.Visibility = Visibility.Visible;
            RoomName_OnTop.Text = "Комната: ...";

            Textbox_PrivateMsg.Text = "";
            Textbox_PrivateMsg.IsEnabled = false;

            Grid_Users_Tags.Visibility = Visibility.Hidden;
        }
        public void OnUserMessages(Message message)
        {
            if (MessagesPanel.Children.OfType<Grid>().ToList().Find(b => b.Uid == message.Id.ToString()) is not Grid msg_grid) 
                return;

            if(msg_grid.FindElementByTag<TextBlock>("Time") is not TextBlock textblock_time) 
                return;

            textblock_time.Text = message.dateTime.ToString("HH:mm");
            textblock_time.FontSize = 10;
            textblock_time.Foreground = new SolidColorBrush(Color.FromRgb(234, 75, 133));
        }
        public void On_Grid_Tags_People_Button(string grid_tags_people_name_pressed)
        {
            foreach (Grid button in Panel_Users_Tags.Children.OfType<Grid>().ToList())
            {
                if (button.Tag is not string tag)
                    return;

                if (tag == grid_tags_people_name_pressed)
                {
                    Grid_Users_Tags.Visibility = Visibility.Hidden;
                    TextBox.Focus();
                    TextBlock textblock = ((StackPanel)button.Children[1]).Children.OfType<TextBlock>().Last();
                    Textbox_PrivateMsg.Text = textblock.Text[2..];
                }
            }
        }
        public void On_Sending_Text(My_FileInfo[] fileInfos)
        {
            TextBox.Clear();

            Files_Grid.Visibility = Visibility.Hidden;
            Chatting_Textbox_Hint.Visibility = Visibility.Visible;

            foreach(var fileInfo in fileInfos)
                fileInfo.ReleaseFileLock();

            Files_StackPanel.Children.Clear();
        }

        // Функции текстового поля, с помощью которого можно отправлять сообщения
        void TextBox_Messages_KeyDown(object sender, KeyEventArgs e)
        {
            if ((Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift && e.Key == Key.Enter)
            {
                TextBox.txtTextbox.CaretPosition.InsertTextInRun(Environment.NewLine);
                TextBox.txtTextbox.CaretPosition = TextBox.txtTextbox.CaretPosition.DocumentEnd;
                e.Handled = true; 
            }
            else if (e.Key == Key.Enter)
            {
                e.Handled = true;

                if (!CanDropFiles())
                {
                    mainWindow.Make_ErrorMessage("Send Error", "Подождите, некоторые файлы обрабатываются");
                    return;
                }

                string reciever = Textbox_PrivateMsg.Text;

                string text = TextBox.GetPlainText().TrimStart().TrimEnd();

                My_FileInfo[] file_infos = GetFilePathesFromStackPanel(Files_StackPanel);

                mainWindow.OnTextBoxMessages(reciever, text, file_infos);
            }
        }

        public void Chatting_RichTextBox_SetText(string text) => TextBox.Text = text;
        public void ProcessTextChanged()
        {
            Chatting_Textbox_Hint.Visibility = string.IsNullOrEmpty(TextBox.GetPlainText()) ? Visibility.Visible : Visibility.Collapsed;
        }
        
        // Обработчики событий при перемещении файлов в приложение
        void FilesToSend_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                Dragging_Files_Grid.Visibility = Visibility.Visible;
                e.Effects = DragDropEffects.Copy;
            }
            e.Handled = true;
        }
        void FilesToSend_DragLeave(object sender, DragEventArgs e)
        {
            Dragging_Files_Grid.Visibility = Visibility.Collapsed;
            e.Handled = true;
        }
        void FilesToSend_Drop(object sender, DragEventArgs e)
        {
            e.Handled = true;
            Dragging_Files_Grid.Visibility = Visibility.Hidden;

            if (!e.Data.GetDataPresent(DataFormats.FileDrop)) 
                return;
            if (!CanDropFiles()) 
                return;

            var files = (string[])e.Data.GetData(DataFormats.FileDrop);

            mainWindow.MakeAction_Form(async() => await ProcessFilesAsync(files));
        }

        async Task ProcessFilesAsync(string[] filePaths)
        {
            try
            {
                // Ограничиваем количество одновременно обрабатываемых файлов
                var batchSize = 4;
                for (int i = 0; i < filePaths.Length; i += batchSize)
                {
                    var batch = filePaths.Skip(i).Take(batchSize).ToArray();
                    await ProcessBatchAsync(batch);
                }
            }
            finally
            {
                SetFilesLoading(false);
            }
        }
        async Task ProcessBatchAsync(string[] batch)
        {
            // Копирование файлов в загрузки и добавление в лист
            var fileInfos = await Task.Run(() =>
            {
                return batch.Select(filePath =>
                {
                    try
                    {
                        var fileInfo = My_FileInfo.CopyFileWithNewName(filePath, My_FileInfo.MakeRandomFileName());
                        fileInfo?.LockFile();
                        return fileInfo;
                    }
                    catch { return null; }

                }).Where(info => info != null).ToList();
            });

            if (fileInfos is null || fileInfos.Count == 0) 
                return;

            await mainWindow.MakeAction_Form_Dispatcher(async () =>
            {
                // 2. Создание UI-элементов в UI-потоке
                var containers = new List<Grid>();
                int maxToAdd = My_FileInfo.MaxFiles - Files_StackPanel.Children.Count;

                foreach (var fileInfo in fileInfos.Take(maxToAdd))
                {
                    if (fileInfo != null)
                    {
                        var container = await CreateFileContainerAsync(fileInfo);

                        if (container != null) containers.Add(container);
                    }
                }

                // 3. Добавление на форму
                if (containers is null || containers.Count == 0) 
                    return;

                foreach (var container in containers)
                {
                    Files_StackPanel.Children.Add(container);
                }

                Files_Grid.Visibility = (Files_StackPanel.Children.Count > 0) ? Visibility.Visible : Visibility.Hidden;
            });
        }

        // Отображение перетянутых файлов на форме
        async Task<Grid> CreateFileContainerAsync(My_FileInfo file_info)
        {
            var grid = new Grid
            {
                Margin = new Thickness(2.5, 0, 2.5, 0),
                Tag = file_info,
                VerticalAlignment = VerticalAlignment.Bottom,
            };

            // Добавляем строки для контента и названия файла
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Для названия файла

            grid.ColumnDefinitions.Add(new ColumnDefinition { MaxWidth = 100 });
            grid.ColumnDefinitions.Add(new ColumnDefinition { MaxWidth = 25 });

            // Фон (растягиваем на все строки)
            var rect = new Rectangle
            {
                Fill = Brushes.White,
                RadiusX = 11,
                RadiusY = 11,
                Stroke = new BrushConverter().ConvertFrom("#a28d6a") as SolidColorBrush,
                StrokeThickness = 1
            };
            Grid.SetColumnSpan(rect, 2);
            Grid.SetRowSpan(rect, 2); // Для документов растягиваем ниже
            grid.Children.Add(rect);

            // Создаем контейнер для превью
            var previewContainer = new Grid();
            Grid.SetRow(previewContainer, 0);
            Grid.SetColumn(previewContainer, 0);

            UIElement preview = new();

            if (file_info.Type is FileType.Image)
            {
                var stream = file_info.GetStream() ?? throw new("WTF file_info.GetStream() is null");

                preview = new Image
                {
                    Margin = new Thickness(5),
                    Source = await GetOptimizedImageAsync(stream, file_info.Path),
                    Stretch = Stretch.Uniform,
                    StretchDirection = StretchDirection.DownOnly,
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    SnapsToDevicePixels = false,
                    MaxHeight = 50
                };

                RenderOptions.SetBitmapScalingMode(preview, BitmapScalingMode.HighQuality);
                RenderOptions.SetEdgeMode(preview, EdgeMode.Aliased);
            }
            else
            {
                preview = new TextBlock
                {
                    Text = file_info.GetFileIcon(),
                    FontSize = 24,
                    Margin = new Thickness(5),
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center
                };
            }

            previewContainer.Children.Add(preview);
            grid.Children.Add(previewContainer);

            // Добавляем название файла для документов
            var fileNameText = new TextBlock
            {
                Text = $"{file_info.Name}{file_info.Extension}",
                Padding = new Thickness(15, 0, 5, 10),
                FontSize = 12,
                FontWeight = FontWeights.Light,
                TextTrimming = TextTrimming.CharacterEllipsis, 
                TextWrapping = TextWrapping.NoWrap,           
                MaxWidth = 100,                              
                HorizontalAlignment = HorizontalAlignment.Left, 
                VerticalAlignment = VerticalAlignment.Center
            };

            Grid.SetRow(fileNameText, 1);
            Grid.SetColumn(fileNameText, 0);
            grid.Children.Add(fileNameText);

            // Кнопка удаления
            var deleteButton = new Grid
            {
                Cursor = Cursors.Hand,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top
            };
            var deleteText = new TextBlock
            {
                Padding = new Thickness(2.5, 4, 7, 0),
                Tag = "Delete",
                Foreground = Brushes.Red,
                FontSize = 12,
                FontFamily = new FontFamily("Comic Sans MS"),
                Text = "X"
            };

            deleteButton.Children.Add(deleteText);
            Grid.SetRow(deleteButton, 0);
            Grid.SetColumn(deleteButton, 1);
            grid.Children.Add(deleteButton);

            // Обработчик удаления
            deleteButton.MouseLeftButtonDown += (s, e) =>
            {
                e.Handled = true;

                file_info.ReleaseFileLock();

                Files_StackPanel.Children.Remove(grid);
                Files_Grid.Visibility = (Files_StackPanel.Children.Count == 0) ? Visibility.Hidden : Visibility.Visible;
            };

            return grid;
        }

        //Получение файлов с формы которые пользователь добавил в очередь на отправку с сообщением
        public static My_FileInfo[] GetFilePathesFromStackPanel(StackPanel stackPanel)
        {
            List<My_FileInfo> filePaths = [];

            foreach (var child in stackPanel.Children)
            {
                if (child is Grid grid && grid.Tag is not null)
                {
                    if (grid.Tag is My_FileInfo fileInfo)
                    {
                        filePaths.Add(fileInfo);
                    }
                }
            }

            return [.. filePaths];
        }


        public async Task<BitmapImage> GetOptimizedImageAsync(FileStream fileStream, string filePath)
        {
            if (!File.Exists(filePath))
                throw new Exception("пустота в fileInfo.Path");

            return await Task.Run(() =>
            {
                var bitmap = new BitmapImage();

                bitmap.BeginInit();
                bitmap.StreamSource = fileStream;
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                if (IsLargeImage(filePath))
                    bitmap.DecodePixelWidth = 800;
                bitmap.EndInit();

                bitmap.Freeze();
                return bitmap;
            });
        }
        bool IsLargeImage(string filePath) => new FileInfo(filePath).Length > 1_000_000; // >1MB

        bool CanDropFiles()
        {
            if (!_fileLock.Wait(0)) return false;

            try
            {
                return !FilesLoading;
            }
            finally
            {
                _fileLock.Release();
            }
        }
        bool SetFilesLoading(bool value)
        {
            if (!_fileLock.Wait(0)) return false;

            try
            {
                FilesLoading = value;
                return true;
            }
            finally
            {
                _fileLock.Release();
            }
        }
    }
}
