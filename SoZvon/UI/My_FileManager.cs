using SoZvon.SubClasses;
using System.Windows;
using System.Windows.Controls;

namespace SoZvon.UI
{
    public class FilesManager(IMainWindow mainWindow)
    {
        readonly IMainWindow mainWindow = mainWindow;

        readonly Dictionary<string, My_FileParent> files = [];
        readonly object _lock = new();

        public bool TryGetFile(string fileName, out My_FileParent file)
        {
            lock (_lock)
            {
                file = default!;
                if (files.TryGetValue(fileName, out var obj) && obj is My_FileParent typedValue)
                {
                    file = typedValue;
                    return true;
                }
                return false;
            }
        }
        public bool TryAddFile(string fileName, My_FileParent file)
        {
            lock (_lock)
            {
                return files.TryAdd(fileName, file);
            }
        }
        public bool CallOnReadyFileToDownload(string fileName)
        {
            lock (_lock)
            {
                files.TryGetValue(fileName, out My_FileParent? file);

                file?.OnReadyFileToDownload();

                return file != null;
            }
        }
        public bool CallOnFileLoadingToServer(string fileName)
        {
            lock (_lock)
            {
                files.TryGetValue(fileName, out My_FileParent? file);

                file?.OnFileLoadingToServer();

                return file != null;
            }
        }
        public void ClearFilesList()
        {
            lock (_lock)
            {
                files.Clear();
            }
        }

        // Фабричный метод для создания контейнера с файлами
        public Grid CreateContainer(List<My_FileInfo> fileInfos)
        {
            // Динамическое создание grid с разным количеством колонок/строк в зависимости от количества файлов (1-4)

            var filesGrid = new Grid();

            if (fileInfos.Count == 1)
            {
                filesGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                filesGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            }
            else if (fileInfos.Count == 2)
            {
                filesGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                filesGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                filesGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            }
            else // 3 и 4 файла
            {
                filesGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                filesGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                filesGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                filesGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            }

            for (int i = 0; i < fileInfos.Count; i++)
            {
                var fileInfo = fileInfos[i];

                My_FileParent fileCard = MakeFile(fileInfo);

                // Определяем позицию в grid
                int row = 0;
                int col = 0;

                if (fileInfos.Count == 2)
                {
                    col = i;
                }
                else if (fileInfos.Count == 3)
                {
                    // Три файла - первый слева занимает 2 строки
                    if (i == 0)
                    {
                        Grid.SetRowSpan(fileCard, 2);
                    }
                    else
                    {
                        col = 1;
                        row = i - 1;
                    }
                }
                else // 4 файла
                {
                    // Четыре файла - равномерная сетка 2x2
                    col = i % 2;
                    row = i / 2;
                }

                Grid.SetRow(fileCard, row);
                Grid.SetColumn(fileCard, col);

                filesGrid.Children.Add(fileCard);

                TryAddFile(fileInfo.Name + fileInfo.Extension, fileCard);
            }

            return filesGrid;
        }

        // Основной метод создания UI для файла
        internal My_FileParent MakeFile(My_FileInfo fileInfo)
        {
            My_FileParent file;
            FileUI_Action action = FileUI_Action.None;

            if (fileInfo.Path is not null && System.IO.File.Exists(fileInfo.Path))
            {
                if (!fileInfo.IsFromHistoryMsg)
                {
                    action = FileUI_Action.Upload;
                }
            }
            else action = FileUI_Action.Download;

            if (fileInfo.Type is FileType.Image)
                file = new ImageUI(fileInfo, action, this);
            else
                file = new FileUI(fileInfo, action, this);

            return file.MakeFileWithAction();
        }

        public bool SetOperationID(string fileName, string id)
        {
            if (!files.TryGetValue(fileName, out My_FileParent? file))
                return false;

            file.SetOperationID(id);
            return true;
        }

        public void MakeAction_Form(Action action) => mainWindow.MakeAction_Form(action);

        public void OnProgressHandler(string fileName, int percent, long fileSize)
        {
            if (!TryGetFile(fileName, out var file))
                throw new My_Exception("TryGetFile is false");

            file.ExecuteProgressOperationHandler(percent, fileSize);
        }
        public void OnFileInfoHandler(string fileName, long fileSize)
        {
            if (!TryGetFile(fileName, out var file))
                throw new My_Exception("TryGetFile is false");

            file.ExecuteFileInfoHandler(fileSize);
        }
        public void OnErrorHandler(string fileName, string text)
        {
            if (!TryGetFile(fileName, out var file))
                throw new My_Exception("TryGetFile is false");

            file.ExecuteErrorHandler(text);
        }
        public void OnUploadErrorHandler(string fileName, string text)
        {
            if (!TryGetFile(fileName, out var file))
                throw new My_Exception("TryGetFile is false");

            file.ExecuteOnUpdateErrorHandler(text);
        }

        public void DownloadFile(string fileName, string path) => mainWindow.DownloadFile(fileName, path);
        public void UploadFile(string fileName) => mainWindow.UploadFile(fileName);
        public void GetInfoFile(string fileName) => mainWindow.GetInfoFile(fileName);

        public void CanselOperation(string id) => mainWindow.CanselOperation(id);
        public void OnError(string title, string message) => mainWindow.Make_ErrorMessage(title, message);
        public void OnNotify(string title, string message) => mainWindow.Make_NotifyMessage(title, message);
    }
}