using System.IO;
using SysPath = System.IO.Path;

namespace SoZvon.SubClasses
{
    public class FileLocker(string filePath) : IDisposable
    {
        readonly object fileStream_lock = new();
        FileStream? fileStream;
        readonly string filePath = filePath;

        public static FileLocker LockFile(string file_name)
        {
            var fileLocker = new FileLocker(file_name);

            fileLocker.FastLockFile();

            return fileLocker;
        }
        public void FastLockFile()
        {
            lock (fileStream_lock)
            {
                LockFile();
            }
        }
        void LockFile()
        {
            try
            {
                fileStream = new FileStream(filePath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read); 
                // FileShare.None - запрещает любое совместное использование
            }
            catch (IOException ex)
            {
                throw new IOException($"Cannot lock file {filePath}. It may be in use by another process.", ex);
            }
        }
        
        public FileStream? GetStream()
        {
            lock (fileStream_lock)
            {
                return fileStream;
            }
        }
        public void Dispose()
        {
            lock (fileStream_lock)
            {
                fileStream?.Dispose();
                fileStream = null;
            }
        }
    }

    // Класс для хранения информации о файле и работы с файлами
    public class My_FileInfo(string name, string extension, string path, FileType type, bool isFromHistoryMsg = false)
    {
        // Статическое поле с путем к папке для загрузок по умолчанию
        public readonly static string sozvon_papka = SysPath.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads\\Sozvon Files");
        
        // Максимальное количество файлов для одновременной обработки
        public const int MaxFiles = 4;

        // Статические массивы расширений для классификации файлов
        public static string[] ImageExtensions { get; } = [".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp"];
        public static string[] DocumentExtensions { get; } = [".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx", ".txt", ".csv"];
        public static string[] VideoExtensions { get; } = [".mp4", ".avi", ".mov", ".wmv", ".mkv", ".webm", ".mpeg", ".mpg"];
        public static string[] MusicExtensions { get; } = [".mp3", ".wav", ".ogg", ".flac", ".aac", ".wma", ".m4a", ".amr", ".opus", ".mid", ".midi", ".aiff"];

        // Основные свойства файла
        public string Name { get; set; } = name;
        public string Extension { get; set; } = extension;
        public string Path { get; set; } = path;
        public FileType Type { get; set; } = type;
        public long Size { get; set; } = 0;
        public bool IsFromHistoryMsg { get; set; } = isFromHistoryMsg;

        readonly FileLocker Locker = new(path);

        public My_FileInfo(string name, string extension, string path, FileType type, long size, bool isFromHistoryMsg = false) : this(name, extension, path, type, isFromHistoryMsg)
        {
            Size = size;
        }

        // Метод для получения информации о файле по пути
        public static (string name, string extension, FileType fileType, long size) GetFileInfoData(string filePath)
        {
            if (Directory.Exists(filePath))
                throw new Exception("its a papka");

            string name = SysPath.GetFileNameWithoutExtension(filePath);
            string extension = SysPath.GetExtension(filePath).ToLowerInvariant();
            long size = new FileInfo(filePath).Length;

            if (string.IsNullOrEmpty(name) && string.IsNullOrEmpty(extension))
                throw new Exception("name is empty and extension is empty");

            // Определение типа файла по расширению
            FileType fileType = extension switch
            {
                var ext when ImageExtensions.Contains(ext) => FileType.Image,
                var ext when DocumentExtensions.Contains(ext) => FileType.Document,
                var ext when VideoExtensions.Contains(ext) => FileType.Video,
                var ext when MusicExtensions.Contains(ext) => FileType.Music,
                _ => FileType.UnknownFile
            };

            return (name, extension, fileType, size);
        }

        // Методы для копирования файла с новым именем (две перегрузки)
        public static My_FileInfo? CopyFileWithNewName(string sourcePath, string targetDirectory, string newFileName)
        {
            var fileLocker = FileLocker.LockFile(sourcePath);

            try 
            {
                (string _, string extension, FileType fileType, long size) = GetFileInfoData(sourcePath);

                if (fileType is FileType.UnknownFile)
                    throw new("FileType.UnknownFile");

                Directory.CreateDirectory(targetDirectory);

                string destinationPath = SysPath.Combine(targetDirectory, newFileName + extension);

                File.Copy(sourcePath, destinationPath, overwrite: true);

                return new(newFileName, extension, destinationPath, fileType, size);
            }
            catch { throw; }
            finally
            {
                fileLocker.Dispose();
            }
        }
        public static My_FileInfo? CopyFileWithNewName(string sourcePath, string newFileName)
        {
            var fileLocker = FileLocker.LockFile(sourcePath);

            try
            {
                (string _, string extension, FileType fileType, long size) = GetFileInfoData(sourcePath);

                if (fileType is FileType.UnknownFile)
                    throw new("FileType.UnknownFile");

                Directory.CreateDirectory(sozvon_papka);

                string destinationPath = SysPath.Combine(sozvon_papka, newFileName + extension);

                File.Copy(sourcePath, destinationPath, overwrite: true);

                return new(newFileName, extension, destinationPath, fileType, size);
            }
            catch { throw; }
            finally
            {
                fileLocker.Dispose();
            }
        }
        public static My_FileInfo? DeleteFileByName(string sourcePath, string name)
        {
            // Проверяем существование файла
            if (!File.Exists(sourcePath))
                return null; // или throw new FileNotFoundException($"File not found: {sourcePath}");

            // Получаем информацию о файле перед удалением
            (string fileName, string extension, FileType fileType, long size) = GetFileInfoData(sourcePath);

            // Проверяем, совпадает ли имя файла (без расширения) с указанным именем
            if (!fileName.Equals(name, StringComparison.OrdinalIgnoreCase))
                return null; // Имена не совпадают

            // Удаляем файл
            File.Delete(sourcePath);

            // Возвращаем информацию об удаленном файле
            return new My_FileInfo(fileName, extension, sourcePath, fileType, size);
        }

        // Создание FileInfo
        public static My_FileInfo GetFileInfo(string filePath, bool isFromHistoryMsg = false)
        {
            if (Directory.Exists(filePath))
                throw new Exception("its a papka");

            string name = SysPath.GetFileNameWithoutExtension(filePath);
            string extension = SysPath.GetExtension(filePath).ToLowerInvariant();
            
            long size = !isFromHistoryMsg ? new FileInfo(filePath).Length : -1;

            if (string.IsNullOrEmpty(name) && string.IsNullOrEmpty(extension))
                throw new Exception("name is empty and extension is empty");

            FileType fileInfo = extension switch
            {
                var ext when ImageExtensions.Contains(ext) => FileType.Image,
                var ext when DocumentExtensions.Contains(ext) => FileType.Document,
                var ext when VideoExtensions.Contains(ext) => FileType.Video,
                var ext when MusicExtensions.Contains(ext) => FileType.Music,
                _ => FileType.UnknownFile,
            };

            return new(name, extension, filePath, fileInfo, size, isFromHistoryMsg);
        }

        // Блокировка файла, если он встал в очередь на отправку и соответственно его разблокировка
        public void LockFile() => Locker.FastLockFile();
        public void ReleaseFileLock() => Locker.Dispose();
        public FileStream? GetStream() => Locker.GetStream();

        // Генерация рандомных назвваний файлов 
        public static string MakeRandomFileName(string extension) => $"gg_{DateTime.Now.Millisecond}{DateTime.Now.Microsecond}{extension}";
        public static string MakeRandomFileName() => $"gg_{DateTime.Now.Millisecond}{DateTime.Now.Microsecond}";

        // Функция дл получения иконки файла FileInfo в зависимости от расширения файла (Extension)
        public string GetFileIcon()
        {
            return Type switch 
            {
                FileType.Image => "🖼️",
                FileType.Document => "📄",
                FileType.Video => "🎬",
                FileType.Music => "🎵",
                FileType.Folder or FileType.UnknownFile or _ => "📁"
            };
        }
    }

    // Перечисление типов файлов
    public enum FileType
    {
        Image,        // Изображения
        Document,     // Документы
        Video,        // Видео файлы
        Music,        // Аудио файлы
        UnknownFile,  // Неизвестный тип
        Folder        // Папка (директория)
    }
}
