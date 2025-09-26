using SoZvon.SubClasses;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading.Channels;

using Action_IUser = SoZvon.Main_Thread.Action_IUser;
using ActionFromIUser = SoZvon.Main_Thread.ActionFromIUser;
using ActionToIUser = SoZvon.Main_Thread.ActionToIUser;
using IUser = SoZvon.Main_Thread.IUser;

namespace SoZvon.ServerConnectionManager
{
    public partial class ConnectionManager
    {
        readonly Channel<Action_IUser> Actions_Channel = Channel.CreateBounded<Action_IUser>(new BoundedChannelOptions(2000) { FullMode = BoundedChannelFullMode.Wait });

        async Task IUserAction_Channel_Thread(CancellationToken cancellationToken)
        {
            try
            {
                await foreach (Action_IUser action_IUser in Actions_Channel.Reader.ReadAllAsync(cancellationToken))
                {
                    try
                    {
                        InterpretateActionIUser(action_IUser).Invoke();
                    }
                    catch (OperationCanceledException) { }
                    catch (My_Exception ex)
                    {
                        Message_Error(ex.Title ?? action_IUser.Action.ToString(), ex.Message);
                    }
                }
            }
            catch (OperationCanceledException) { }
        }
        Action InterpretateActionIUser(Action_IUser action_IUser)
        {
            Action action;

            var dict = action_IUser.Params;

            switch (action_IUser.Action)
            {
                case ActionFromIUser.OnStart:
                    {
                        if (dict.Count != 0)
                            throw new My_Exception("no valid params");

                        action = StartProperties;
                        break;
                    }
                case ActionFromIUser.OnLogin:
                    {
                        if (dict.Count != 0)
                            throw new My_Exception("no valid params");

                        action = OnLogin;
                        break;
                    }
                case ActionFromIUser.OnCloseApplication:
                    {
                        if (dict.Count != 0)
                            throw new My_Exception("no valid params");

                        action = Dispose;
                        break;
                    }
                case ActionFromIUser.ReloadConnectionServer:
                    {
                        if (dict.Count != 0)
                            throw new My_Exception("no valid params");

                        action = async () =>
                        {
                            ForceDisconnect();
                            await New_ConnectionAttempt();
                        };
                        break;
                    }
                case ActionFromIUser.OnSendingMessage:
                    {
                        if (dict.Count != 1 || !dict.TryGetValue<Message>("message", out var message))
                            throw new My_Exception("no valid params");

                        action = async () => await SendMessage(message);
                        break;
                    }
                case ActionFromIUser.OnChangeIp:
                    {
                        if (dict.Count != 1 || !dict.TryGetValue<string>("ip", out var ip))
                            throw new My_Exception("no valid params");

                        action = () => SetIP(ip);
                        break;
                    }
                default:
                    throw new My_Exception("no valid ActionFromIUser");
            }

            return action;
        }
        public async void OnIUserAction(ActionFromIUser action_IUser, Dictionary<string, object> dict) => await Actions_Channel.Writer.WriteAsync(new(action_IUser, dict));
    }
    public partial class ConnectionManager : IServerConnection
    {
        string IP = "95.154.89.8";
        const string PORT_CONST = "12000";
        
        public bool IsLogIn { get; private set; } = false;
        public bool IsConnected
        {
            get => _isConnected;
            private set
            {
                if (_isConnected != value)
                {
                    _isConnected = value;
                    User.OnInterfacesAction(ActionToIUser.IsConnectedChanged, new() { ["value"] = value });
                    OnConnectedChanged(value);
                }
            }
        }

        bool _isConnected = false;
        NetworkStream? Stream;
        TcpClient? TcpClient;

        readonly IUser User;

        readonly My_Timer heartBeat_check = new(6);
        readonly Channel<Message> send_current_messages_channel = Channel.CreateBounded<Message>(new BoundedChannelOptions(2000) { FullMode = BoundedChannelFullMode.Wait });
        readonly Channel<ConnectionAttempt> connection_attempts_channel = Channel.CreateBounded<ConnectionAttempt>(new BoundedChannelOptions(2000) { FullMode = BoundedChannelFullMode.Wait });

        readonly CancellationTokenSource cts_main = new();
        CancellationTokenSource linked_cts_main = new();
        CancellationTokenSource? cts_currentOperation = new();
        readonly object lock_currentOperation = new();

        public ConnectionManager(IUser user)
        {
            User = user;

            _ = IUserAction_Channel_Thread(cts_main.Token);
        }

        public void StartProperties()
        {
            heartBeat_check.SetAcionOnTick(() => ForceDisconnect("No Heartbeat From Server"));
            
            _ = ConnectingServerThread(cts_main.Token);
        }
        public void OnLogin()
        {
            IsLogIn = true;
            heartBeat_check.Start();
        }

        public async Task New_ConnectionAttempt(int timeout_millisecond = 2000, Action? action = null) => await New_ConnectionAttempt(IP, timeout_millisecond, action);
        async Task New_ConnectionAttempt(string ip_, int timeout_millisecond_, Action? action)
        {
            await connection_attempts_channel.Writer.WriteAsync(new(ip_, PORT_CONST, timeout_millisecond_, action));
        }

        public async Task SendMessage(Message message) => await send_current_messages_channel.Writer.WriteAsync(message);

        async Task ConnectingServerThread(CancellationToken cancellationToken)
        {
            NetworkChange.NetworkAvailabilityChanged += OnNetworkAvailabilityChanged;

            while (!cancellationToken.IsCancellationRequested)
            {
                linked_cts_main = new();
                var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cts_main.Token, linked_cts_main.Token);

                await AwaitConnection(linkedCts.Token);

                Task heartbeat = HeartbeatToServer(linkedCts.Token);
                Task receiveTask = ReceiveMessagesAsync(linkedCts.Token);
                Task sendTask = SendingThread(linkedCts.Token);

                await Task.WhenAll(receiveTask, sendTask, heartbeat);
            }

            NetworkChange.NetworkAvailabilityChanged -= OnNetworkAvailabilityChanged;
        }
        async Task ReceiveMessagesAsync(CancellationToken cancellationToken)
        {
            MessageInfo message_info;

            List<byte> all_bytes;
            byte[] temp_bytes;

            while (!cancellationToken.IsCancellationRequested)
            {
                var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                linkedCts.CancelAfter(5000);

                try
                {
                    all_bytes = [];

                    (temp_bytes, all_bytes) = await ReadSomeBytesAsync(num_bytes: 2, all_bytes, linkedCts);

                    if (temp_bytes[0] == 1 && temp_bytes[1] == 1)
                    {
                        heartBeat_check?.Reset(); // HEARTBEAT СЕРВЕРА
                        continue;
                    }
                    else if (temp_bytes[0] != 7 || temp_bytes[1] != 7) 
                        continue;

                    (temp_bytes, all_bytes) = await ReadSomeBytesAsync(num_bytes: 2, all_bytes, linkedCts); 
                    // КОЛИЧЕСТВО БАЙТОВ ДЛЯ ИНФОРМАЦИИ О ДЛИНЕ СООБЩЕНИЯ

                    message_info = new MessageInfo(BitConverter.ToInt16(temp_bytes, 0));

                    if (message_info.MessageLength == 0) 
                        continue;

                    (temp_bytes, all_bytes) = await ReadSomeBytesAsync(num_bytes: message_info.MessageLength, all_bytes, linkedCts); 
                    // КОЛИЧЕСТВО БАЙТОВ ДЛЯ ИНФОРМАЦИИ О ДЛИНЕ СООБЩЕНИЯ

                    var message = new Message(message_info, [.. all_bytes]);

                    User.OnInterfacesAction(ActionToIUser.MessageRecieved, new() {
                        ["message"] = message
                    });
                }
                catch (OperationCanceledException)
                {
                    if(cancellationToken.IsCancellationRequested) 
                        break;

                    continue;
                }
                catch (Exception ex)
                {
                    Message_Error("ReceiveMessagesAsync", $"Unexpected error: {ex.Message}");
                    break;
                }
            }

            return;
        }
        async Task SendingThread(CancellationToken cancellationToken)
        {
            try
            {
                await foreach (Message msg in send_current_messages_channel.Reader.ReadAllAsync(cancellationToken))
                {
                    try
                    {
                        if (Stream is null) throw new Exception("Stream is null");

                        await Stream.WriteAsync(msg.message_data.AsMemory(0, msg.message_data.Length), cancellationToken);

                        if(msg.message_info.CommandText is not CommandText.HeartBeat)
                            msg.AddMessageToHistory();
                    }
                    catch (OperationCanceledException) { }
                    catch (Exception ex)
                    {
                        if (IsConnected) ForceDisconnect(ex.Message.ToString());
                    }
                }
            }
            catch (OperationCanceledException) { return; }
        }
        async Task HeartbeatToServer(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(3000, cancellationToken);

                    if (IsLogIn && IsConnected)
                    {
                        await send_current_messages_channel.Writer.WriteAsync(new Message(CommandText.HeartBeat, [0x01, 0x01]), cancellationToken);
                    }
                }
                catch (OperationCanceledException) { break; }
                catch { break; }
            }
        }
        async Task AwaitConnection(CancellationToken cancellationToken)
        {
            try
            {
                await foreach (var attempt in connection_attempts_channel.Reader.ReadAllAsync(cancellationToken))
                {
                    // Безопасная отмена предыдущей операции
                    CancelCurrentConnectionAttempt();

                    using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

                    linkedCts.CancelAfter(attempt.Timeout_Millisecond);

                    lock (lock_currentOperation) 
                        cts_currentOperation = linkedCts;

                    try
                    {
                        if (await Make_Connection_Server(attempt, linkedCts.Token))
                        {
                            CancelCurrentConnectionAttempt();
                            return; // Успешное подключение - выходим
                        }
                    }
                    catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                    {
                        ForceDisconnect();
                    }

                    CancelCurrentConnectionAttempt();
                }
            }
            catch (OperationCanceledException) { }
            finally
            {
                CancelCurrentConnectionAttempt();
            }
        }
        async Task<bool> Make_Connection_Server(ConnectionAttempt connectionAttempt, CancellationToken ct)
        {
            string error_text = "";
            string ip = connectionAttempt.IP;
            int port = Convert.ToInt32(connectionAttempt.Port);

            if (connectionAttempt.IP != "")
            {
                try
                {
                    ip = IPAddress.Parse(connectionAttempt.IP).ToString();
                }
                catch
                {
                    Message_Error("Server_Error", "Incorrect IP");
                    Log_Error("No Connection To Server (not valid ip)");
                    return false;
                }
            }

            Log_Error($"Trying to connect to the Server... (ip: {ip})");

            TcpClient tcpclient = new();
            CancellationTokenSource token = new();
            token.CancelAfter(connectionAttempt.Timeout_Millisecond);

            try
            {
                ct.ThrowIfCancellationRequested();

                await tcpclient.ConnectAsync(ip, port, token.Token);

                if (tcpclient.Connected)
                {
                    TcpClient = tcpclient;
                    Stream = tcpclient.GetStream();
                    Connection_Succeed(ip);
                    connectionAttempt.Action?.Invoke();
                    return true;
                }
            }
            catch (OperationCanceledException)
            {
                if (ct.IsCancellationRequested)
                    error_text = "Error, Connection failed: timeout exceeded";
                else error_text = "OperationCanceledException";
            }
            catch (SocketException)
            {
                error_text = "Error: Connection to Server with Such IP and Port Failed";
            }
            catch (Exception)
            {
                error_text = "Unexpected Error: Connection Failed";
            }

            Message_Error("Server_Error", error_text);
            Log_Error($"No Connection To Server (ip: {ip})");
            return false;
        }
        void CancelCurrentConnectionAttempt()
        {
            lock (lock_currentOperation)
            {
                if (cts_currentOperation is null) 
                    return;

                if (!cts_currentOperation.IsCancellationRequested)
                    cts_currentOperation.Cancel();

                cts_currentOperation.Dispose();
                cts_currentOperation = null;
            }
        }

        async Task<(byte[] read_bytes, List<byte> all_bytes)> ReadSomeBytesAsync(int num_bytes, List<byte> list_to_add_bytes, CancellationTokenSource linkedCts)
        {
            if (Stream is null) 
                throw new InvalidOperationException("Stream is null. Cannot receive messages");

            byte[] temp_bytes = new byte[num_bytes]; // КОЛИЧЕСТВО БАЙТОВ НАЧАЛА СООБЩЕНИЯ
            await Stream.ReadExactlyAsync(temp_bytes, 0, temp_bytes.Length, linkedCts.Token);
            list_to_add_bytes.AddRange(temp_bytes);

            return (temp_bytes, list_to_add_bytes);
        }

        void OnNetworkAvailabilityChanged(object? _, NetworkAvailabilityEventArgs e)
        {
            if (!e.IsAvailable)
                ForceDisconnect("Turn on your internet");
        }
        void Connection_Succeed(string ip)
        {
            Log_Notify($"Connected To Server (ip: {ip})");
            IsConnected = true;
        }
        public void ForceDisconnect()
        {
            if (!IsConnected) 
                return;

            Log_Error("Connection Lost");
            IsConnected = false;
        }
        public void ForceDisconnect(string text_error)
        {
            if(!IsConnected) return;

            Message_Error("Server_Error", text_error);
            Log_Error("Connection Lost");
            IsConnected = false;
        }
        void OnConnectedChanged(bool newIsConnectedValue)
        {
            if (!newIsConnectedValue)
            {
                IsLogIn = false;
                linked_cts_main.Cancel();

                Stream?.Close();
                Stream?.Dispose();
                Stream = null;

                TcpClient?.Close();
                TcpClient?.Dispose();
                TcpClient = null;
            }
        }
        public void SetIP(string ip) => IP = ip;

        void Message_Error(string title, string text) => User.OnInterfacesAction(ActionToIUser.MessageErrorOccurred, new() {
            ["title"] = title,
            ["text"] = text
        });
        void Log_Error(string text) => User.OnInterfacesAction(ActionToIUser.LogErrorOccurred, new() { 
            ["text"] = text
        });
        void Log_Notify(string text) => User.OnInterfacesAction(ActionToIUser.LogNotifyOccurred, new() {
            ["text"] = text
        });
        public void Dispose()
        {
            IsConnected = false;
            cts_main.Cancel();
        }
    }
}
