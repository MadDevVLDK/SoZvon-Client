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
                    try
                    {
                        Action action = InterpretateActionIUser(action_IUser);
                        action.Invoke();
                    }
                    catch (OperationCanceledException) { }
                    catch (My_Exception ex)
                    {
                        ErrorMessageOccured(ex.Title ?? action_IUser.Action.ToString(), ex.Message);
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
                case ActionFromIUser.OnLogin:
                    {
                        if (dict.Count != 1 || !dict.TryGetValue<Guid>("guid", out var guid))
                            throw new My_Exception("no valid params");

                        action = () => UpdateLoginGuid(guid);
                        break;
                    }
                case ActionFromIUser.OnChangeIp:
                    {
                        if (dict.Count != 1 || !dict.TryGetValue<string>("ip", out var ip))
                            throw new My_Exception("no valid params");

                        action = () => SetIP(ip);
                        break;
                    }
                case ActionFromIUser.OnCloseApplication:
                    {
                        if (dict.Count != 0)
                            throw new My_Exception("no valid params");

                        action = Dispose;
                        break;
                    }
                case ActionFromIUser.CancelOperation:
                    {
                        if (dict.Count != 1 || !dict.TryGetValue<string>("operationID", out var operationID))
                            throw new My_Exception("no valid params");

                        action = () => CancelOperation(operationID);
                        break;
                    }
                case ActionFromIUser.DownloadFile:
                    {
                        if (dict.Count != 2 || !dict.TryGetValue<string>("filename", out var filename) || !dict.TryGetValue<string>("saveFolder", out var saveFolder))
                            throw new My_Exception("no valid params");

                        action = async () =>
                        {
                            string id = await EasyFileDownloadAsync(filename, saveFolder);

                            User.OnInterfacesAction(ActionToIUser.SetOperationId, new() {
                                ["fileName"] = filename,
                                ["id"] = id
                            });
                        };
                        break;
                    }
                case ActionFromIUser.UploadFile:
                    {
                        if (dict.Count != 1 || !dict.TryGetValue<string>("filename", out var filename))
                            throw new My_Exception("no valid params");

                        action = async () => 
                        {
                            string id = await EasyFileUploadAsync(filename);

                            User.OnInterfacesAction(ActionToIUser.SetOperationId, new() {
                                ["fileName"] = filename,
                                ["id"] = id
                            });
                        };
                        break;
                    }
                case ActionFromIUser.GetInfoFile:
                    {
                        if (dict.Count != 1 || !dict.TryGetValue<string>("filename", out var filename))
                            throw new My_Exception("no valid params");

                        action = async () =>
                        {
                            string id = await EasyFileGetInfoAsync(filename);

                            User.OnInterfacesAction(ActionToIUser.SetOperationId, new()
                            {
                                ["fileName"] = filename,
                                ["id"] = id
                            });
                        };
                        break;
                    }
                default:
                    throw new My_Exception("no valid ActionFromIUser");
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
        
        string LoginGuidMessage;

        readonly ConcurrentDictionary<string, CancellationTokenSource> activeOperations = new();

        readonly HttpClient httpClient = new();
        Channel<Operation> operationChannel = Channel.CreateUnbounded<Operation>();
        readonly SemaphoreSlim semaphore = new(1, 1);
        CancellationTokenSource globalCts = new();

        public API_Manager(IUser user)
        {
            User = user;

            _ = UserUI_Channel_Thread(globalCts.Token);
            _ = ProcessOperationsAsync(globalCts.Token);
        }
        
        public async Task<string> EasyFileDownloadAsync(string filenameDownload, string saveFolder)
        {
            try
            {
                string url = $"http://{IP}:{PORT_CONST}/chat-download?file={filenameDownload}";

                string savePath = $"{saveFolder}\\{filenameDownload}";

                return await AddDownloadAsync(filenameDownload, url, savePath);
            }
            catch (OperationCanceledException)
            {
                ErrorMessageOccured("File_Error", "Download was canceled!");
            }
            catch (Exception ex)
            {
                ErrorMessageOccured("File_Error", $"Download failed: {ex.Message}");
            }

            return "";
        }
        public async Task<string> EasyFileUploadAsync(string filenameUpload)
        {
            try
            {
                string url = $"http://{IP}:{PORT_CONST}/chat-upload";

                return await AddUploadAsync(filenameUpload, url);
            }
            catch (OperationCanceledException)
            {
                ErrorMessageOccured("File_Error", "Upload was canceled!");
            }
            catch (Exception ex)
            {
                ErrorMessageOccured("File_Error", $"Upload failed: {ex.Message}");
            }

            return "";
        }
        public async Task<string> EasyFileGetInfoAsync(string filenameUpload)
        {
            try
            {
                string url = $"http://{IP}:{PORT_CONST}/chat-fileinfo?file={filenameUpload}";

                return await AddGetInfoAsync(filenameUpload, url);
            }
            catch (OperationCanceledException)
            {
                ErrorMessageOccured("File_Error", "Upload was canceled!");
            }
            catch (Exception ex)
            {
                ErrorMessageOccured("File_Error", $"Upload failed: {ex.Message}");
            }

            return "";
        }

        async Task ProcessOperationsAsync(CancellationToken ct)
        {
            try
            {
                await semaphore.WaitAsync(ct);

                await foreach (Operation operation in operationChannel.Reader.ReadAllAsync(ct))
                {
                    try
                    {
                        if (operation is DownloadOperation download)
                            await ProcessDownloadAsync(download);

                        else if (operation is UploadOperation upload)
                            await ProcessUploadAsync(upload);

                        else if (operation is GetFileInfoOperation getInfoFile)
                            await ProcessGetInfoAsync(getInfoFile);
                    }
                    catch (OperationCanceledException)
                    {
                        User.OnInterfacesAction(ActionToIUser.OnErrorHandler, new() {
                            ["fileName"] = operation.Filename,
                            ["text"] = "Скачивание отменено"
                        });
                    }
                    catch (My_Exception ex)
                    {
                        User.OnInterfacesAction(ActionToIUser.OnErrorHandler, new() {
                            ["fileName"] = operation.Filename,
                            ["text"] = ex.Message
                        });
                    }
                    catch (Exception ex)
                    {
                        User.OnInterfacesAction(ActionToIUser.OnErrorHandler, new() {
                            ["fileName"] = operation.Filename,
                            ["text"] = ex.Message
                        });
                    }
                    finally
                    {
                        activeOperations.TryRemove(operation.OperationId, out _);
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                ErrorMessageOccured("Operation_Error", $"Error processing operation: {ex.Message}");
            }
            finally
            {
                semaphore.Release();
            }
        }

        public async Task<string> AddDownloadAsync(string filename, string url, string destinationPath, int progressStep = 1, CancellationToken ct = default)
        {
            string operationId = Guid.NewGuid().ToString();
            var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(globalCts.Token, ct);

            activeOperations.TryAdd(operationId, linkedCts);

            DownloadOperation operation = new(operationId, filename, url, destinationPath, progressStep, linkedCts.Token);

            await operationChannel.Writer.WriteAsync(operation, linkedCts.Token);

            return operationId;
        }
        public async Task<string> AddUploadAsync(string filename, string url, int progressStep = 1, CancellationToken ct = default)
        {
            string operationId = Guid.NewGuid().ToString();
            var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(globalCts.Token, ct);

            activeOperations.TryAdd(operationId, linkedCts);

            var operation = new UploadOperation(operationId, filename, url, progressStep, linkedCts.Token);

            await operationChannel.Writer.WriteAsync(operation, linkedCts.Token);
            return operationId;
        }
        public async Task<string> AddGetInfoAsync(string filename, string url, CancellationToken ct = default)
        {
            string operationId = Guid.NewGuid().ToString();
            var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(globalCts.Token, ct);

            activeOperations.TryAdd(operationId, linkedCts);

            var operation = new GetFileInfoOperation(operationId, filename, url, linkedCts.Token);

            await operationChannel.Writer.WriteAsync(operation, linkedCts.Token);
            return operationId;
        }

        async Task ProcessDownloadAsync(DownloadOperation operation)
        {
            var (success, error, stream) = await DownloadFileAsync(operation);

            if (!success)
                throw new Exception(error);

            Directory.CreateDirectory(My_FileInfo.sozvon_papka);

            using var fileStream = File.Create(operation.DestinationPath);

            if (stream is null) 
                throw new Exception("stream is null");

            await stream.CopyToAsync(fileStream, operation.Cts);
        }
        async Task ProcessUploadAsync(UploadOperation operation)
        {
            var (success, error) = await UploadFileAsync(operation);

            if (!success)
            {
                User.OnInterfacesAction(ActionToIUser.OnUploadErrorHandler, new() {
                    ["fileName"] = operation.Filename,
                    ["text"] = error ?? "Error Upload"
                });
            }
        }
        async Task ProcessGetInfoAsync(GetFileInfoOperation operation)
        {
            var (success, error) = await GetFileInfoAsync(operation);

            if (!success)
                throw new Exception(error);
        }

        async Task<(bool success, string? error, Stream? fileStream)> DownloadFileAsync(DownloadOperation operation)
        {
            try
            {
                operation.Cts.ThrowIfCancellationRequested();

                if (string.IsNullOrEmpty(LoginGuidMessage)) 
                    return (false, "Authorization token not set", null);

                var request = new HttpRequestMessage(HttpMethod.Get, operation.Url);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", LoginGuidMessage);

                var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, operation.Cts);

                if (!response.IsSuccessStatusCode)
                {
                    return response.StatusCode switch
                    {
                        HttpStatusCode.Unauthorized => (false, "Invalid token: " + LoginGuidMessage, null),
                        HttpStatusCode.NotFound => (false, "Файл не найден", null),
                        _ => (false, await response.Content.ReadAsStringAsync(operation.Cts), null)
                    };
                }

                var totalBytes = response.Content.Headers.ContentLength ?? -1L;
                var memoryStream = new MemoryStream();
                await using var stream = await response.Content.ReadAsStreamAsync(operation.Cts);

                var buffer = new byte[8192];
                long receivedBytes = 0;
                int lastReportedProgress = -1;
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();
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
                        if (roundedProgress > lastReportedProgress && (DateTime.Now - lastUpdateTime).TotalMilliseconds > 100)
                        {
                            lastReportedProgress = roundedProgress;
                            lastUpdateTime = DateTime.Now;

                            User.OnInterfacesAction(ActionToIUser.OnProgressHandler, new() {
                                ["fileName"] = operation.Filename,
                                ["percent"] = percentage,
                                ["fileSize"] = totalBytes
                            });
                        }
                    }
                }

                User.OnInterfacesAction(ActionToIUser.OnProgressHandler, new() {
                    ["fileName"] = operation.Filename,
                    ["percent"] = 100,
                    ["fileSize"] = totalBytes
                });

                memoryStream.Position = 0;
                return (true, null, memoryStream);
            }
            catch (OperationCanceledException ex) 
            {
                return (false, ex.Message, null);
            }
            catch (Exception ex) 
            {
                return (false, ex.Message, null);
            }
        }
        async Task<(bool success, string? error)> UploadFileAsync(UploadOperation operation)
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

                using var httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", LoginGuidMessage);

                // Используем FileStream вместо чтения всего файла в память
                await using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
                var progressContent = new ProgressableStreamContent(fileStream, 8192, bytesSent =>
                {
                    if (totalBytes <= 0) 
                        return;

                    var currentTime = DateTime.Now;

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

                    User.OnInterfacesAction(ActionToIUser.OnProgressHandler, new() {
                        ["fileName"] = operation.Filename,
                        ["percent"] = percentage,
                        ["fileSize"] = totalBytes
                    });
                });
                var formData = new MultipartFormDataContent
                {
                    { progressContent, "file", operation.Filename }
                };

                timer.Start();

                NotificationOccured(TypeNotification.UploadingFile, new Dictionary<string, object>() { 
                    [ "name_file"] = fileInfo.Name,
                    [ "percentage"] = (short)0,
                });

                var response = await httpClient.PostAsync(operation.Url, formData, operation.Cts);

                // Гарантируем 100% по завершении
                User.OnInterfacesAction(ActionToIUser.OnProgressHandler, new() {
                    ["fileName"] = operation.Filename,
                    ["percent"] = 100,
                    ["fileSize"] = totalBytes
                });

                if (!response.IsSuccessStatusCode)
                {
                    return response.StatusCode switch
                    {
                        HttpStatusCode.Unauthorized => (false, "Invalid token"),
                        _ => (false, $"Server error: {await response.Content.ReadAsStringAsync()}")
                    };
                }

                var result = await response.Content.ReadFromJsonAsync<UploadResult>();

                NotificationOccured(TypeNotification.EndUploadingFile, new Dictionary<string, object>() {
                    ["name_file"] = fileInfo.Name
                });

                return result?.status == "ok" ? (true, null) : (false, "Upload failed: invalid server response");
            }
            catch (Exception ex)
            {
                return (false, $"Upload failed: {ex.Message}");
            }
        }
        async Task<(bool success, string? error)> GetFileInfoAsync(GetFileInfoOperation operation)
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

                GetFileInfoResult result = await response.Content.ReadFromJsonAsync<GetFileInfoResult>() ?? throw new My_Exception("GetFileInfoResult is null");

                User.OnInterfacesAction(ActionToIUser.OnFileInfoHandler, new() {
                    ["fileName"] = operation.Filename,
                    ["fileSize"] = result.size
                });

                return (true, null);
            }
            catch (OperationCanceledException ex)
            {
                return (false, ex.Message);
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }


        public void SetIP(string ip) => IP = ip;
        
        public void UpdateLoginGuid(Guid guid) => LoginGuidMessage = guid.ToString();

        public void NotificationOccured(TypeNotification type, Dictionary<string, object> dict) => User.OnInterfacesAction(ActionToIUser.ServerNotifyOccured, new () { ["notification"] = new NotificationServer(type, dict) });
        public void ErrorMessageOccured(string title, string text) => User.OnInterfacesAction(ActionToIUser.MessageErrorOccurred, new() { ["title"] = title, ["text"] = text });

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
            operationChannel.Writer.Complete();
            httpClient.Dispose();
            semaphore.Dispose();
            globalCts.Dispose();
        }
    }
}