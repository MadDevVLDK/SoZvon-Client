using SoZvon.Main_Thread;
using SoZvon.SubClasses;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading.Channels;

namespace SoZvon.ServerAPIManager
{
    public partial class API_Manager
    {
        readonly Channel<Action_IUser> UserUI_Channel = Channel.CreateBounded<Action_IUser>(new BoundedChannelOptions(2000) { FullMode = BoundedChannelFullMode.Wait });

        async Task UserUI_Channel_Thread(CancellationToken cancellationToken)
        {
            try
            {
                await foreach (Action_IUser action_IUser in UserUI_Channel.Reader.ReadAllAsync(cancellationToken))
                {
                    Action action = InterpretateActionIUser(action_IUser);
                    action.Invoke();
                }
            }
            catch (OperationCanceledException) { }
            catch (ArgumentException ex)
            {
                ErrorMessageOccured("Wrong params (API_Manager)", $"Fatal Error: {ex.Message}");
            }
            catch (Exception ex)
            {
                ErrorMessageOccured("API_Manager", $"Fatal Error: {ex.Message}");
            }
        }
        Action InterpretateActionIUser(Action_IUser action_IUser)
        {
            Action action;

            var dict = action_IUser.Params;

            switch (action_IUser.Action)
            {
                case ActionFromIUser.OnLogin:
                    {
                        if (dict.Count != 1 || !dict.TryGetValue<Guid>("guid", out var guid))
                            throw new ArgumentException(action_IUser.Action.ToString());

                        action = () => UpdateLoginGuid(guid);
                        break;
                    }
                case ActionFromIUser.OnChangeIp:
                    {
                        if (dict.Count != 1 || !dict.TryGetValue<string>("ip", out var ip))
                            throw new ArgumentException(action_IUser.Action.ToString());

                        action = () => SetIP(ip);
                        break;
                    }
                case ActionFromIUser.OnCloseApplication:
                    {
                        if (dict.Count != 0)
                            throw new ArgumentException(action_IUser.Action.ToString());

                        action = Dispose;
                        break;
                    }
                case ActionFromIUser.CancelOperation:
                    {
                        if (dict.Count != 1 || !dict.TryGetValue<string>("operationID", out var operationID))
                            throw new ArgumentException(action_IUser.Action.ToString());

                        action = () => CancelOperation(operationID);
                        break;
                    }
                case ActionFromIUser.DownloadFile:
                    {
                        if (dict.Count != 2 || !dict.TryGetValue<string>("filename", out var filename) || !dict.TryGetValue<string>("saveFolder", out var saveFolder))
                            throw new ArgumentException(action_IUser.Action.ToString());

                        action = async () =>
                        {
                            User.OnInterfacesAction(ActionToIUser.SetOperationId, new() {
                                ["fileName"] = filename,
                                ["id"] = await EasyFileDownloadAsync(filename, saveFolder)
                            });
                        };
                        break;
                    }
                case ActionFromIUser.UploadFile:
                    {
                        if (dict.Count != 1 || !dict.TryGetValue<string>("filename", out var filename))
                            throw new ArgumentException(action_IUser.Action.ToString());

                        action = async () => 
                        {
                            User.OnInterfacesAction(ActionToIUser.SetOperationId, new() {
                                ["fileName"] = filename,
                                ["id"] = await EasyFileUploadAsync(filename)
                            });
                        };
                        break;
                    }
                case ActionFromIUser.GetInfoFile:
                    {
                        if (dict.Count != 1 || !dict.TryGetValue<string>("filename", out var filename))
                            throw new ArgumentException(action_IUser.Action.ToString());

                        action = async () =>
                        {
                            User.OnInterfacesAction(ActionToIUser.SetOperationId, new() {
                                ["fileName"] = filename,
                                ["id"] = await EasyFileGetInfoAsync(filename)
                            });
                        };
                        break;
                    }
                default:
                    throw new ArgumentException("no valid ActionFromIUser");
            }

            return action;
        }
        public async void OnIUserAction(ActionFromIUser action_IUser, Dictionary<string, object> dict) => await UserUI_Channel.Writer.WriteAsync(new(action_IUser, dict));
    }
    public partial class API_Manager : IManagerAPI
    {
        readonly IUser User;

        string IP = "95.154.89.8";
        const string PORT_CONST = "12001";
        
        string LoginGuidMessage = string.Empty;

        readonly ConcurrentDictionary<string, CancellationTokenSource> activeOperations = new();

        readonly HttpClient httpClient = new();
        readonly SemaphoreSlim semaphore = new(1, 1);
        Channel<Operation> operationChannel = Channel.CreateBounded<Operation>(new BoundedChannelOptions(2000) { FullMode = BoundedChannelFullMode.Wait });
        CancellationTokenSource globalCts = new();

        public API_Manager(IUser user)
        {
            User = user;

            _ = UserUI_Channel_Thread(globalCts.Token);
            _ = ProcessOperationsAsync(globalCts.Token);
        }

        async Task ProcessOperationsAsync(CancellationToken ct)
        {
            try
            {
                await semaphore.WaitAsync(ct);

                await foreach (Operation operation in operationChannel.Reader.ReadAllAsync(ct))
                {

                    (bool success, string error) = operation switch
                    {
                        DownloadOperation download => await DownloadFileAsync(download),
                        UploadOperation upload => await UploadFileAsync(upload),
                        GetFileInfoOperation getInfoFile => await GetFileInfoAsync(getInfoFile),
                        _ => throw new ArgumentException("Unknown operation")
                    };

                    if (!success)
                        HandleOperationError(operation, error);

                    activeOperations.TryRemove(operation.OperationId, out _);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                ErrorMessageOccured("ProcessOperation (API_Manager)", $"Fatal Error: {ex.Message}");
            }
            finally
            {
                semaphore.Release();
            }
        }
        void HandleOperationError(Operation operation, string error)
        {
            switch (operation)
            {
                case UploadOperation:
                    User.OnInterfacesAction(ActionToIUser.OnUploadErrorHandler, new() {
                        ["fileName"] = operation.Filename,
                        ["text"] = error
                    });
                    break;
                default:
                    User.OnInterfacesAction(ActionToIUser.OnErrorHandler, new() {
                        ["fileName"] = operation.Filename,
                        ["text"] = error
                    });
                    break;
            }
        }

        public async Task<string> EasyFileDownloadAsync(string filenameDownload, string saveFolder)
        {
            string url = $"http://{IP}:{PORT_CONST}/chat-download?file={filenameDownload}";

            string savePath = $"{saveFolder}\\{filenameDownload}";

            return await AddDownloadAsync(filenameDownload, url, savePath);
        }
        public async Task<string> EasyFileUploadAsync(string filenameUpload)
        {
            string url = $"http://{IP}:{PORT_CONST}/chat-upload";

            return await AddUploadAsync(filenameUpload, url);
        }
        public async Task<string> EasyFileGetInfoAsync(string filenameUpload)
        {
            string url = $"http://{IP}:{PORT_CONST}/chat-fileinfo?file={filenameUpload}";

            return await AddGetInfoAsync(filenameUpload, url);
        }

        public async Task<string> AddDownloadAsync(string filename, string url, string destinationPath, int progressStep = 1, CancellationToken ct = default)
        {
            string operationId = Guid.NewGuid().ToString();
            var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(globalCts.Token, ct);

            activeOperations.TryAdd(operationId, linkedCts);

            DownloadOperation operation = new(operationId, filename, url, destinationPath, progressStep, linkedCts.Token);

            try
            {
                await operationChannel.Writer.WriteAsync(operation, linkedCts.Token);
            }
            catch { return string.Empty; }

            return operationId;
        }
        public async Task<string> AddUploadAsync(string filename, string url, int progressStep = 1, CancellationToken ct = default)
        {
            string operationId = Guid.NewGuid().ToString();
            var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(globalCts.Token, ct);

            activeOperations.TryAdd(operationId, linkedCts);

            var operation = new UploadOperation(operationId, filename, url, progressStep, linkedCts.Token);

            try
            {
                await operationChannel.Writer.WriteAsync(operation, linkedCts.Token);
            }
            catch { return string.Empty; }

            return operationId;
        }
        public async Task<string> AddGetInfoAsync(string filename, string url, CancellationToken ct = default)
        {
            string operationId = Guid.NewGuid().ToString();
            var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(globalCts.Token, ct);

            activeOperations.TryAdd(operationId, linkedCts);

            var operation = new GetFileInfoOperation(operationId, filename, url, linkedCts.Token);

            try
            {
                await operationChannel.Writer.WriteAsync(operation, linkedCts.Token);
            }
            catch { return string.Empty; }

            return operationId;
        }

        async Task<(bool success, string error)> DownloadFileAsync(DownloadOperation operation)
        {
            try
            {
                operation.Cts.ThrowIfCancellationRequested();

                if (string.IsNullOrEmpty(LoginGuidMessage)) 
                    return (false, "Authorization token not set");

                var request = new HttpRequestMessage(HttpMethod.Get, operation.Url);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", LoginGuidMessage);

                var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, operation.Cts);

                if (!response.IsSuccessStatusCode)
                {
                    return response.StatusCode switch
                    {
                        HttpStatusCode.Unauthorized => (false, "Invalid token: " + LoginGuidMessage),
                        HttpStatusCode.NotFound => (false, "Файл не найден"),
                        _ => (false, await response.Content.ReadAsStringAsync(operation.Cts))
                    };
                }

                var totalBytes = response.Content.Headers.ContentLength ?? -1L;
                var memoryStream = new MemoryStream();
                await using var stream = await response.Content.ReadAsStreamAsync(operation.Cts);

                var buffer = new byte[8192];
                long receivedBytes = 0;
                int lastReportedProgress = -1;
                var lastUpdateTime = DateTime.MinValue;

                while (true)
                {
                    int bytesRead = await stream.ReadAsync(buffer, operation.Cts);

                    if (bytesRead == 0) 
                        break;

                    await memoryStream.WriteAsync(buffer.AsMemory(0, bytesRead), operation.Cts);
                    receivedBytes += bytesRead;

                    if (totalBytes > 0)
                    {
                        var percentage = (int)(receivedBytes * 100 / totalBytes);
                        int roundedProgress = percentage / operation.ProgressStep * operation.ProgressStep;

                        // Обновляем прогресс только если:
                        // 1. Процент изменился
                        // 2. И прошло >100мс с последнего обновления (для производительности)
                        if (roundedProgress > lastReportedProgress && (DateTime.UtcNow - lastUpdateTime).TotalMilliseconds > 100)
                        {
                            lastReportedProgress = roundedProgress;
                            lastUpdateTime = DateTime.UtcNow;

                            OnProgressFileHandler(operation.Filename, (short)percentage, totalBytes);
                        }
                    }
                }

                // Гарантируем 100% по завершении
                OnProgressFileHandler(operation.Filename, 100, totalBytes);

                memoryStream.Position = 0;

                Directory.CreateDirectory(My_FileInfo.sozvon_papka);

                using var fileStream = File.Create(operation.DestinationPath);

                await memoryStream.CopyToAsync(fileStream, operation.Cts);

                return (true, string.Empty);
            }
            catch (OperationCanceledException)
            {
                return (false, "Загрузка отменена");
            }
            catch (Exception ex) 
            {
                return (false, ex.Message);
            }
        }
        async Task<(bool success, string error)> UploadFileAsync(UploadOperation operation)
        {
            try
            {
                operation.Cts.ThrowIfCancellationRequested();

                if (string.IsNullOrEmpty(LoginGuidMessage))
                    return (false, "Authorization token not set");

                var filePath = Path.Combine(My_FileInfo.sozvon_papka, operation.Filename);
                var fileInfo = new FileInfo(filePath);
                var totalBytes = fileInfo.Length;
                int lastReportedProgress = -1;
                DateTime lastUpdateTime = DateTime.MinValue;
                My_Timer timer = new(3);

                httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", LoginGuidMessage);

                // Используем FileStream вместо чтения всего файла в память
                await using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
                var progressContent = new ProgressableStreamContent(fileStream, 8192, bytesSent =>
                {
                    if (totalBytes <= 0) 
                        return;

                    var currentTime = DateTime.UtcNow;

                    if ((currentTime - lastUpdateTime).TotalMilliseconds < 100) 
                        return;

                    var percentage = (int)(bytesSent * 100 / totalBytes);
                    var roundedProgress = percentage / operation.ProgressStep * operation.ProgressStep;

                    if (!timer.IsWorking)
                    {
                        NotificationOccured(TypeNotification.UploadingFile, new Dictionary<string, object>() {
                            ["name_file"] = fileInfo.Name,
                            ["percentage"] = (short)percentage,
                        });
                        timer.Reset();
                    }

                    if (roundedProgress <= lastReportedProgress) 
                        return;

                    lastReportedProgress = roundedProgress;
                    lastUpdateTime = currentTime;

                    OnProgressFileHandler(operation.Filename, (short)percentage, totalBytes);
                });
                var formData = new MultipartFormDataContent
                {
                    { progressContent, "file", operation.Filename }
                };

                timer.Start();

                var response = await httpClient.PostAsync(operation.Url, formData, operation.Cts);

                // Гарантируем 100% по завершении
                OnProgressFileHandler(operation.Filename, 100, totalBytes);

                if (!response.IsSuccessStatusCode)
                {
                    return response.StatusCode switch
                    {
                        HttpStatusCode.Unauthorized => (false, "Invalid token"),
                        _ => (false, $"Server error: {await response.Content.ReadAsStringAsync(operation.Cts)}")
                    };
                }

                var result = await response.Content.ReadFromJsonAsync<UploadResult>(operation.Cts);

                if (result is null)
                    return (false, "UploadResult is null");

                NotificationOccured(TypeNotification.EndUploadingFile, new() {
                    ["name_file"] = fileInfo.Name
                });

                return result.status == "ok" ? (true, string.Empty) : (false, "Upload failed: invalid server response");
            }
            catch (OperationCanceledException)
            {
                return (false, "Загрузка отменена");
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }
        async Task<(bool success, string error)> GetFileInfoAsync(GetFileInfoOperation operation)
        {
            try
            {
                operation.Cts.ThrowIfCancellationRequested();

                if (string.IsNullOrEmpty(LoginGuidMessage))
                    return (false, "Authorization token not set");

                var request = new HttpRequestMessage(HttpMethod.Get, operation.Url);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", LoginGuidMessage);

                var response = await httpClient.SendAsync(request, operation.Cts);

                if (!response.IsSuccessStatusCode)
                {
                    return response.StatusCode switch
                    {
                        HttpStatusCode.Unauthorized => (false, "Invalid token: " + LoginGuidMessage),
                        HttpStatusCode.NotFound => (false, "Файл не найден"),
                        _ => (false, await response.Content.ReadAsStringAsync(operation.Cts))
                    };
                }

                var result = await response.Content.ReadFromJsonAsync<GetFileInfoResult>(operation.Cts);

                if (result is null)
                    return (false, "GetFileInfoResult is null");

                User.OnInterfacesAction(ActionToIUser.OnFileInfoHandler, new() {
                    ["fileName"] = operation.Filename,
                    ["fileSize"] = result.size
                });

                return (true, string.Empty);
            }
            catch (OperationCanceledException)
            {
                return (false, "Получение инфы о файле отменено");
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        public void SetIP(string ip) => IP = ip;
        public void UpdateLoginGuid(Guid guid) => LoginGuidMessage = guid.ToString();

        public void NotificationOccured(TypeNotification type, Dictionary<string, object> dict) => User.OnInterfacesAction(ActionToIUser.ServerNotifyOccured, new () {
            ["notification"] = new NotificationServer(type, dict)
        });
        public void OnProgressFileHandler(string filename, int percent, long fileSize) => User.OnInterfacesAction(ActionToIUser.OnProgressHandler, new() {
            ["fileName"] = filename,
            ["percent"] = percent,
            ["fileSize"] = fileSize
        });
        public void ErrorMessageOccured(string title, string text) => User.OnInterfacesAction(ActionToIUser.MessageErrorOccurred, new() {
            ["title"] = title,
            ["text"] = text
        });

        public bool CancelOperation(string operationId)
        {
            if (activeOperations.TryRemove(operationId, out var cts))
            {
                cts.Cancel();
                cts.Dispose();
                return true;
            }
            return false;
        }
        public void CancelAllOperations()
        {
            operationChannel.Writer.Complete();
            globalCts.Cancel();
            globalCts.Dispose();
            globalCts = new();

            operationChannel = Channel.CreateUnbounded<Operation>();
        }
        public void Dispose()
        {
            globalCts.Cancel();
            globalCts.Dispose();
            operationChannel.Writer.Complete();
            httpClient.Dispose();
            semaphore.Dispose();
        }
    }
}