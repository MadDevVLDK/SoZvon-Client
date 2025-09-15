using SoZvon.UI.SubClasses;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Path = System.IO.Path;

namespace SoZvon.SubClasses
{
    // Базовый класс для работы с файлами в UI
    public abstract class My_FileParent(My_FileInfo fileInfo_, FileUI_Action action, UI.FilesManager filesManager) : Grid
    {
        internal readonly UI.FilesManager filesManager = filesManager;
        internal readonly My_Timer uploading_timer = new(4);

        // Статический словарь текстов для разных действий с файлами    
        internal readonly static Dictionary<FileUI_Action, Progress_Text> files_text = new()
        {
            [FileUI_Action.Download] = new("Требуется скачать", "Загружено"),
            [FileUI_Action.Upload] = new("Начало отправки...", "Отправлено"),
            [FileUI_Action.None] = new("Выполнено", "Выполнено"),
        };

        // ID текущей операции (загрузки/выгрузки)
        string? _operationID;
        public string? OperationID
        {
            get { return _operationID; }
            internal set
            {
                _operationID = value;
                UpdateOperation();
            }
        }

        // Выполнена ли операция, если ошибка --> false
        public bool OperationCompleted { get; internal set; } = false;

        // Присвоен ли был размер
        public bool HasSize { get; internal set; } = false;
        public bool FileExists
        {
            get
            {
                if (!File.Exists(fileInfo.Path))
                {
                    ErrorOccured("Файл не найден");
                    return false;
                }
                else return true;
            }
        }

        public Rectangle MainBackground { get; internal set; }
        public StackPanel MainContainer { get; internal set; }
        public TextBlock ProgressText { get; internal set; }
        public TextBlock FileNameText { get; internal set; }
        public TextBlock FileIcon { get; internal set; }
        public StackPanel FileInfoPanel { get; internal set; }
        public Grid InfoMsg { get; internal set; }

        // Переменная текущего действия, которое надо выполнить
        Action currentOperationAction;

        public FileUI_Action FileAction { get; internal set; } = action;
        public Progress_Text Progress_text { get; internal set; } = files_text[action];

        // Основные свойства файла
        public My_FileInfo fileInfo { get; internal set; } = fileInfo_;


        // Инициализация базовых свойств UI элемента
        internal void StartProperties()
        {
            Cursor = Cursors.Hand;
            Margin = new Thickness(15);
            MinHeight = 120;
            MinWidth = 120;

            MouseEnter += (s, e) => MainBackground.Fill = new SolidColorBrush(Color.FromRgb(235, 235, 235));
            MouseLeave += (s, e) => MainBackground.Fill = new SolidColorBrush(Color.FromRgb(245, 245, 245));

            MainBackground = new Rectangle()
            {
                RadiusX = 8,
                RadiusY = 8,
                StrokeThickness = 1,
                Stroke = new SolidColorBrush(Color.FromRgb(220, 220, 220)),
                Fill = new SolidColorBrush(Color.FromRgb(245, 245, 245))
            };
            MainContainer = new StackPanel
            {
                Orientation = Orientation.Vertical,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(10)
            };

            FileIcon = new TextBlock
            {
                Text = fileInfo.GetFileIcon(),
                FontSize = 30,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 3)
            };
            FileNameText = new TextBlock
            {
                Text = fileInfo.Name,
                TextWrapping = TextWrapping.NoWrap,
                TextTrimming = TextTrimming.CharacterEllipsis,
                TextAlignment = TextAlignment.Center,
                MaxWidth = 200,
                FontSize = 12,
            };
            FileInfoPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 2, 0, 0),
                Children =
                {
                    new TextBlock { Tag = "FileSizeText", Text = "0 B", FontSize = 10, Foreground = new SolidColorBrush(Color.FromRgb(120, 120, 120)) },
                    new TextBlock { Text = " • ", FontSize = 10, Foreground = new SolidColorBrush(Color.FromRgb(120, 120, 120)) },
                    new TextBlock { Text = fileInfo.Extension.ToUpper().TrimStart('.'), FontSize = 10, Foreground = new SolidColorBrush(Color.FromRgb(120, 120, 120)) }
                }
            };
            ProgressText = new TextBlock
            {
                Text = Progress_text.StartText,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(224, 147, 45)),
                Padding = new Thickness(5, 10, 5, 5)
            };

            MainContainer.Children.Add(FileIcon);
            MainContainer.Children.Add(FileNameText);
            MainContainer.Children.Add(FileInfoPanel);
            MainContainer.Children.Add(ProgressText);

            Children.Add(MainBackground);
            Children.Add(MainContainer);

            // Создание информационного сообщения
            Children.Add(CreateInfoMsg());

            uploading_timer.SetAcionOnTick(() => filesManager.MakeAction_Form(() => ErrorOccured("Файл не загружен")));
        }

        // Создание файла с действием определенным
        internal My_FileParent MakeFileWithAction()
        {
            CheckFileInfo();

            return FileAction switch
            {
                FileUI_Action.None => Make_ReadyFile(),
                FileUI_Action.Upload => Make_UploadFile(),
                FileUI_Action.Download => Make_DownloadFile(),
                _ => throw new ArgumentOutOfRangeException(),
            };
        }

        // Методы для выполнения действий с файлом
        internal virtual My_FileParent Make_ReadyFile() => this;
        internal virtual My_FileParent Make_UploadFile() => this;
        internal virtual My_FileParent Make_DownloadFile() => this;

        // Создание контекстного меню для файла
        internal virtual ContextMenu CreateFileContextMenu(FrameworkElement element)
        {
            element.MouseRightButtonDown -= OnContextMenuMouseRightButtonDown;

            // Создаем контекстное меню
            var contextMenu = new ContextMenu();

            // Пункт "Копировать в буфер обмена"
            var copyMenuItem = new MenuItem
            {
                Header = "Копировать в буфер обмена",
                Icon = new TextBlock { Text = "📋", FontSize = 14 }
            };
            copyMenuItem.Click += (s, e) => filesManager.MakeAction_Form(SaveBufferFile);

            // Пункт "Сохранить как..."
            var saveMenuItem = new MenuItem
            {
                Header = "Сохранить как...",
                Icon = new TextBlock { Text = "💾", FontSize = 14 }
            };
            saveMenuItem.Click += (s, e) => filesManager.MakeAction_Form(SaveFolderFile);

            // Пункт "Сохранить на рабочий стол"
            var quickSaveMenuItem = new MenuItem
            {
                Header = "Сохранить на рабочий стол",
                Icon = new TextBlock { Text = "🖥️", FontSize = 14 }
            };
            quickSaveMenuItem.Click += (s, e) => filesManager.MakeAction_Form(SaveDesktopFile);

            // Пункт "Открыть расположение"
            var openLocationMenuItem = new MenuItem
            {
                Header = "Открыть расположение файла",
                Icon = new TextBlock { Text = "📂", FontSize = 14 }
            };
            openLocationMenuItem.Click += (s, e) => filesManager.MakeAction_Form(OpenLocationFile);

            // Добавляем пункты в меню
            contextMenu.Items.Add(copyMenuItem);
            contextMenu.Items.Add(saveMenuItem);
            contextMenu.Items.Add(quickSaveMenuItem);
            contextMenu.Items.Add(new Separator());
            contextMenu.Items.Add(openLocationMenuItem);

            // Обработчик правой кнопки мыши
            element.MouseRightButtonDown += OnContextMenuMouseRightButtonDown;

            return contextMenu;
        }
        internal void OnContextMenuMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (ContextMenu != null)
            {
                ContextMenu.IsOpen = true;
                e.Handled = true;
            }
        }

        // Инициализация InfoMsg, который показывает пользователю информацию о том, что при нажатии на него будет
        internal Grid CreateInfoMsg()
        {
            InfoMsg = new Grid
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                MaxWidth = MinWidth,
                MaxHeight = MinHeight,
                Visibility = Visibility.Collapsed
            };
            InfoMsg.Children.Add(new Rectangle
            {
                Fill = new SolidColorBrush(Color.FromRgb(255, 255, 255)),
                RadiusX = 5,
                RadiusY = 5,
                StrokeThickness = 1,
                Stroke = new SolidColorBrush(Color.FromRgb(170, 170, 170)),
                Tag = "Background"
            });
            InfoMsg.Children.Add(new TextBlock
            {
                Text = "Открыть файл",
                Foreground = new SolidColorBrush(Color.FromRgb(150, 150, 150)),
                TextAlignment = TextAlignment.Center,
                TextWrapping = TextWrapping.WrapWithOverflow,
                Padding = new Thickness(5),
                Tag = "Text"
            });

            // Общие обработчики
            MouseLeftButtonDown += (s, e) => filesManager.MakeAction_Form(HandleLeftClick);
            MouseEnter += (s, e) => InfoMsg.Visibility = Visibility.Visible;
            MouseLeave += (s, e) => InfoMsg.Visibility = Visibility.Collapsed;
            InfoMsg.Unloaded += OnUnloaded;

            UpdateOperation();

            return InfoMsg;
        }

        // Обработка клика по файлу. В зависимости от состояния выполняет:
        // - Открытие файла
        // - Начало загрузки/выгрузки
        // - Отмену текущей операции
        internal void HandleLeftClick() => currentOperationAction.Invoke();
        internal void UpdateOperation()
        {
            // В зависимости от состояния присваивает текст и т.п. для InfoMsg,
            // а также присваивает действие при нажатии на него

            bool has_working_operation = HasWorkingOperation();

            if (FileAction is FileUI_Action.None || OperationCompleted)
            {
                currentOperationAction = OpenFile;
            }
            else
            {
                currentOperationAction = has_working_operation ? (CanselOperation) : (FileAction is FileUI_Action.Download ? DownloadFile : UploadFile);
            }

            (string message, Color foreground) = FileAction switch
            {
                FileUI_Action.None => ("Открыть", Color.FromRgb(0, 0, 0)),
                _ when OperationCompleted => ("Открыть", Color.FromRgb(0, 0, 0)),
                FileUI_Action.Download => has_working_operation
                    ? ("Отменить загрузку", Color.FromRgb(255, 0, 0))  // Красный для отмены
                    : ("Начать загрузку", Color.FromRgb(0, 128, 0)),   // Зеленый для начала
                FileUI_Action.Upload => has_working_operation
                    ? ("Отменить выгрузку", Color.FromRgb(255, 0, 0))  // Красный для отмены
                    : ("Начать выгрузку", Color.FromRgb(0, 128, 0)),   // Зеленый для начала
                _ => (string.Empty, Colors.Transparent)
            };

            if (InfoMsg.FindElementByTag<TextBlock>("Text") is not TextBlock textBlock) 
                return;

            textBlock.Text = message;
            textBlock.Foreground = new SolidColorBrush(foreground);
        }
        public void ExecuteProgressOperationHandler(int percent, long fileSize)
        {
            string percent_text = $"{percent}% ({FormatFileSize(fileSize * percent / 100)})";
            Color color = Color.FromRgb(224, 147, 45);

            if (percent == 100)
            {
                color = Color.FromRgb(116, 181, 51);
                OnReadyFile();
            }

            UpdateProgress(percent_text, color);
        }
        public void ExecuteFileInfoHandler(long fileSize)
        {
            UpdateFileInfo(fileSize);

            if (fileSize < 5242880)
                DownloadFile();
            else
                ClearOperationID();
        }
        public void ExecuteErrorHandler(string text) => ErrorOccured(text);
        public void ExecuteOnUpdateErrorHandler(string text) => OnUploadErrorOccured(text);

        // Вспомогательные методы для Операций
        internal string SetOperationID(string operationID) => OperationID = operationID;
        internal bool HasWorkingOperation() => OperationID is not null;
        internal void CanselOperation()
        {
            if (OperationID is not null)
                filesManager.CanselOperation(OperationID);
        }
        internal void ClearOperationID() => OperationID = null;

        // Основные операции с файлами
        public void DownloadFile() => filesManager.DownloadFile(fileInfo.Name + fileInfo.Extension, My_FileInfo.sozvon_papka);
        public void UploadFile() => filesManager.UploadFile(fileInfo.Name + fileInfo.Extension);
        public void GetInfoFile() => filesManager.GetInfoFile(fileInfo.Name + fileInfo.Extension);

        // Основные операции с файлами
        public void SaveDesktopFile()
        {
            try
            {
                if (!FileExists)
                    throw new Exception("Файл не найден");

                string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                string fileName = fileInfo.Name + fileInfo.Extension;
                string destPath = Path.Combine(desktopPath, fileName);

                int counter = 1;
                while (File.Exists(destPath))
                {
                    string newFileName = $"{fileInfo.Name} ({counter}){fileInfo.Extension}";
                    destPath = Path.Combine(desktopPath, newFileName);
                    counter++;
                }

                File.Copy(fileInfo.Path, destPath, true);

                filesManager.OnNotify("File", $"Сохранено на рабочий стол");
            }
            catch (Exception ex)
            {
                filesManager.OnError("File_Error", $"Ошибка сохранения: {ex.Message}");
            }
        }
        public virtual void SaveFolderFile()
        {
            var saveDialog = new Microsoft.Win32.SaveFileDialog
            {
                Title = "Сохранить файл",
                FileName = fileInfo.Name,
                Filter = "Все файлы|*.*",
                DefaultExt = fileInfo.Extension
            };

            if (saveDialog.ShowDialog() == true)
            {
                try
                {
                    if (!FileExists)
                        throw new Exception("Файл не найден");

                    File.Copy(fileInfo.Path, saveDialog.FileName, true);
                    filesManager.OnError("File", $"Файл сохранён: {saveDialog.FileName}");
                }
                catch (Exception ex)
                {
                    filesManager.OnError("File_Error", $"Ошибка сохранения: {ex.Message}");
                }
            }
        }
        public virtual void SaveBufferFile()
        {
            try
            {
                if (!FileExists)
                    throw new Exception("Файл не найден");

                var dataObject = new DataObject();
                dataObject.SetText(fileInfo.Path);
                dataObject.SetData(DataFormats.FileDrop, new string[] { fileInfo.Path });
                Clipboard.SetDataObject(dataObject, true);

                filesManager.OnNotify("File", "Путь к файлу скопирован в буфер");
            }
            catch (Exception ex)
            {
                filesManager.OnError("File_Error", $"Не удалось скопировать: {ex.Message}");
            }
        }
        public void OpenFile()
        {
            if (!FileExists)
            {
                filesManager.OnError("File_Error", "Файл не найден");
                return;
            }

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = fileInfo.Path,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                filesManager.OnError("File_Error", $"Не удалось открыть файл: {ex.Message}");
            }
        }
        public void OpenLocationFile()
        {
            try
            {
                if (FileExists)
                {
                    Process.Start("explorer.exe", $"/select,\"{fileInfo.Path}\"");
                }
                else throw new Exception("Файл не найден по указанному пути");
            }
            catch (Exception ex)
            {
                filesManager.OnError("File_Error", ex.Message);
            }
        }
        void CheckFileInfo()
        {
            if (fileInfo.Size == 0)
            {
                GetInfoFile();
            }
            else
            {
                UpdateFileInfo(fileInfo.Size);
            }
        }

        // Вспомогательные методы
        internal void UpdateFileInfo(long size)
        {
            if (FileInfoPanel.FindElementByTag<TextBlock>("FileSizeText") is not TextBlock FileSizeText) 
                return;

            //HasFileInfo = true;
            
            FileSizeText.Text = FormatFileSize(size);
        }
        internal void UpdateProgress(string status, Color color)
        {
            ProgressText.Text = $"{Progress_text.TextOnChange}: {status}";
            ProgressText.Foreground = new SolidColorBrush(color);
        }

        // Метод, который вызывается при готовности файла
        internal virtual void OnReadyFile()
        {
            OperationCompleted = true;

            ClearOperationID();

            ContextMenu = CreateFileContextMenu(this);
            ProgressText.Visibility = Visibility.Collapsed;
        }

        // Метод, который вызывается при ошибки файла
        internal virtual void ErrorOccured(string status)
        {
            if(ProgressText is not null)
            {
                ProgressText.Text = status;
                ProgressText.Foreground = new SolidColorBrush(Color.FromRgb(200, 0, 0));
                ProgressText.Visibility = Visibility.Visible;

                FileAction = FileUI_Action.Download;
                Progress_text = files_text[FileAction];
                    
                if (ContextMenu != null && ContextMenu.IsOpen)
                    ContextMenu.IsOpen = false;

                ContextMenu = null;
            }

            FileIcon.Visibility = Visibility.Visible;
            FileNameText.Visibility = Visibility.Visible;
            FileInfoPanel.Visibility = Visibility.Visible;

            OperationCompleted = false;
            ClearOperationID();
        }
        internal virtual void OnUploadErrorOccured(string status)
        {
            if (ProgressText is not null)
            {
                ProgressText.Text = status;
                ProgressText.Foreground = new SolidColorBrush(Color.FromRgb(200, 0, 0));
                ProgressText.Visibility = Visibility.Visible;

                FileAction = FileUI_Action.Upload;
                Progress_text = files_text[FileAction];

                if (ContextMenu != null && ContextMenu.IsOpen)
                    ContextMenu.IsOpen = false;

                ContextMenu = null;
            }

            FileIcon.Visibility = Visibility.Visible;
            FileNameText.Visibility = Visibility.Visible;
            FileInfoPanel.Visibility = Visibility.Visible;

            OperationCompleted = false;
            ClearOperationID();
        }

        // Форматирование размера файла
        public static string FormatFileSize(long bytes)
        {
            string[] sizes = ["B", "KB", "MB", "GB"];
            int order = 0;
            double len = bytes;

            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len /= 1024;
            }

            return $"{len:0.##} {sizes[order]}";
        }

        // При выгрузке с формы, отменяем задачу активную
        protected void OnUnloaded(object s, RoutedEventArgs e) => CanselOperation();
        
        // Добавьте метод для сброса состояния к загрузке
        public void OnReadyFileToDownload()
        {
            filesManager.MakeAction_Form(() =>
            {
                if (FileExists)
                {
                    //это поведение еще не пробовал, надо пробовать
                    OnReadyFile();
                    return;
                }

                uploading_timer.Stop();

                // Сбрасываем состояние
                HasSize = false;

                if (FileAction is FileUI_Action.None)
                {
                    FileAction = FileUI_Action.Download;
                    Progress_text = files_text[FileAction];
                }

                // Удаляем контекстное меню
                ContextMenu = null;

                // Начинаем загрузку
                DownloadFile();
            });
        }
        public void OnFileLoadingToServer()
        {
            filesManager.MakeAction_Form(() =>
            {
                if (FileExists)
                {
                    //это поведение еще не пробовал, надо пробовать
                    OnReadyFile();
                    return;
                }

                uploading_timer.Reset();

                if (ProgressText is not null)
                {
                    ProgressText.Text = $"Загружается на сервер";
                    ProgressText.Foreground = new SolidColorBrush(Color.FromRgb(0, 0, 150));
                    ProgressText.Visibility = Visibility.Visible;
                }
            });
        }
    }

    // Класс для отображения файлов (наследуется от FileUI)
    public class FileUI : My_FileParent
    {
        // Конструктор инициализирует UI элементы:
        // - Иконка файла
        // - Название файла
        // - Панель информации (размер, расширение)
        // - Прогресс-бар (если требуется)
        public FileUI(My_FileInfo fileInfo_, FileUI_Action action, UI.FilesManager filesManager) : base(fileInfo_, action, filesManager) => StartProperties();

        // Переопределенные методы для действий
        internal override FileUI Make_ReadyFile()
        {
            OnReadyFile();

            return this;
        }
        internal override FileUI Make_UploadFile()
        {
            UploadFile();

            return this;
        }
    }

    // Класс для отображения изображений (наследуется от My_FileParent)
    public class ImageUI : My_FileParent
    {
        // Конструктор инициализирует UI элементы:
        // - Иконка файла
        // - Название файла
        // - Панель информации (размер, расширение)
        // - Прогресс-бар (если требуется)
        // Дополнительное свойство - само изображение
        public Image DisplayImage { get; }

        public ImageUI(My_FileInfo fileInfo_, FileUI_Action action, UI.FilesManager filesManager) : base(fileInfo_, action, filesManager)
        {
            // Создание основного контейнера
            StartProperties();

            // Инициализация элементов
            DisplayImage = new()
            {
                MaxHeight = 500,
                Margin = new Thickness(5, 5, 5, 5),
                Stretch = Stretch.Uniform,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                SnapsToDevicePixels = false,
                Cursor = Cursors.Hand,
                Tag = "DisplayImage",
                Visibility = Visibility.Collapsed
            };

            RenderOptions.SetBitmapScalingMode(DisplayImage, BitmapScalingMode.HighQuality);
            RenderOptions.SetEdgeMode(DisplayImage, EdgeMode.Aliased);

            MainContainer.Children.Insert(0, DisplayImage);
        }

        // Переопределенные методы с доп логикой для изображений
        internal override ImageUI Make_ReadyFile()
        {
            OnReadyFile();

            return this;
        }
        internal override ImageUI Make_UploadFile()
        {
            UploadFile();

            return this;
        }

        // Загрузка и отображение изображения
        internal async void SetImage(int maxRetries = 3)
        {
            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();

                    if (fileInfo.GetStream() is FileStream stream)
                        bitmap.StreamSource = stream;
                    else
                        bitmap.UriSource = new Uri(fileInfo.Path);

                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.EndInit();
                    bitmap.Freeze();

                    DisplayImage.Source = bitmap;
                    DisplayImage.Visibility = Visibility.Visible;

                    return;
                }
                catch (IOException) when (attempt < maxRetries)
                {
                    // Ждем перед повторной попыткой
                    await Task.Delay(100 * attempt);
                    continue;
                }
                catch (Exception ex)
                {
                    filesManager.OnError("File_Error", $"Не удалось открыть изображение. {ex.Message}");
                    return;
                }
            }

            filesManager.OnError("File_Error", "Не удалось открыть изображение после нескольких попыток");
        }

        // Копирование изображения в буфер (а не только пути)
        public override void SaveBufferFile()
        {
            try
            {
                if (DisplayImage?.Source is null)
                {
                    filesManager.OnError("Image_Error", "Нет изображения для копирования");
                    return;
                }

                // Создаем объект DataObject для поддержки разных форматов
                var dataObject = new DataObject();

                // Добавляем само изображение
                dataObject.SetImage((BitmapSource)DisplayImage.Source);

                // Добавляем путь к файлу как текст (если нужно)
                dataObject.SetText(fileInfo.Path, TextDataFormat.Text);
                dataObject.SetData(DataFormats.FileDrop, new string[] { fileInfo.Path });

                // Устанавливаем в буфер обмена
                Clipboard.SetDataObject(dataObject, true);

                filesManager.OnNotify("File", "Изображение скопировано в буфер");
            }
            catch (Exception ex)
            {
                filesManager.OnError("File_Error", $"Не удалось скопировать изображение. {(ex.InnerException?.Message ?? ex.Message)}");
            }
        }
        public override void SaveFolderFile()
        {
            var saveDialog = new Microsoft.Win32.SaveFileDialog
            {
                Title = "Сохранить изображение",
                FileName = fileInfo.Name,
                Filter = "JPEG Image|*.jpg|PNG Image|*.png|Bitmap Image|*.bmp|All Files|*.*",
                DefaultExt = fileInfo.Extension
            };

            if (saveDialog.ShowDialog() is true)
            {
                try
                {
                    using (var fileStream = new FileStream(saveDialog.FileName, FileMode.Create))
                    {
                        BitmapEncoder encoder = saveDialog.FileName.EndsWith(".png") ? new PngBitmapEncoder() : new JpegBitmapEncoder();
                        encoder.Frames.Add(BitmapFrame.Create((BitmapSource)DisplayImage.Source));
                        encoder.Save(fileStream);
                    }

                    filesManager.OnNotify("Image", $"Изображение сохранено на рабочий стол: {saveDialog.FileName}");
                }
                catch (Exception ex)
                {
                    filesManager.OnError("Image_Error", $"Ошибка сохранения: {ex.Message}");
                }
            }
        }

        // При готовности файла к полному отображению и при появлении ошибки
        internal override void OnReadyFile()
        {
            FileIcon.Visibility = Visibility.Collapsed;
            FileNameText.Visibility = Visibility.Collapsed;
            FileInfoPanel.Visibility = Visibility.Collapsed;

            SetImage();

            base.OnReadyFile();
        }
        internal override void ErrorOccured(string status)
        {
            base.ErrorOccured(status);

            DisplayImage.Visibility = Visibility.Collapsed;
            FileIcon.Visibility = Visibility.Visible;
            FileNameText.Visibility = Visibility.Visible;
            FileInfoPanel.Visibility = Visibility.Visible;
        }
    }

    // Вспомогательные типы
    public record Progress_Text(string StartText, string TextOnChange);
    public enum FileUI_Action { None, Download, Upload }
}
