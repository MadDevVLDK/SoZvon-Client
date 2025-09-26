using NAudio.Wave;
using SoZvon.SubClasses;
using System.Net.Sockets;
using System.Threading.Channels;
using Action_IUser = SoZvon.Main_Thread.Action_IUser;
using ActionFromIUser = SoZvon.Main_Thread.ActionFromIUser;
using ActionToIUser = SoZvon.Main_Thread.ActionToIUser;
using IUser = SoZvon.Main_Thread.IUser;

namespace SoZvon.VoiceChatManager
{
    public partial class VoiceManager
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
                        if (InterpretateActionIUser(action_IUser) is Action action)
                            action.Invoke();
                    }
                    catch (OperationCanceledException) { }
                    catch (My_Exception ex)
                    {
                        ReportError(ex.Title ?? action_IUser.Action.ToString(), ex.Message);
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
                case ActionFromIUser.OnChangeIp:
                    {
                        if (dict.Count != 1 || !dict.TryGetValue<string>("ip", out var ip))
                            throw new My_Exception("no valid params");

                        action = () => SetIP(ip);
                        break;
                    }
                case ActionFromIUser.OnLogin:
                    {
                        if (dict.Count != 1 || !dict.TryGetValue<Guid>("guid", out var guid))
                            throw new My_Exception("no valid params");

                        action = () => UpdateLoginGuid(guid);
                        break;
                    }
                case ActionFromIUser.OnCloseApplication:
                    {
                        if (dict.Count != 0)
                            throw new My_Exception("no valid params");

                        action = () => {
                            cts.Cancel();
                            DisposeResources();
                        };
                        break;
                    }
                case ActionFromIUser.OnSelectMicrophoneByName:
                    {
                        if (dict.Count != 1 || !dict.TryGetValue<string>("microphone", out var microphone))
                            throw new My_Exception("no valid params");

                        action = () => 
                        {
                            SelectMicrophoneByName(microphone);

                            if (!ReinitializeWithSelectedMicrophone())
                                throw new My_Exception("InitializeInputDevices is false");
                            if(IsSpeaking)
                                inputDevice?.StartRecording();
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

    public partial class VoiceManager : IVoiceManager
    {
        readonly IUser User;

        int selectedDeviceNumber = -1;
        Dictionary<string, string> previousDevices = [];
        readonly My_Timer deviceMonitorTimer = new(2);

        const int SampleRate = 16000;
        const int BitsPerSample = 16;
        const int Channels = 1;

        readonly WaveFormat waveFormat = new(SampleRate, BitsPerSample, Channels);

        const int BufferSize = 2048;

        string IP = "95.154.89.8";
        const int Port = 12002;

        Guid LoginGuid;

        WaveInEvent? inputDevice;
        WaveOut? outputDevice;
        BufferedWaveProvider? bufferStream;
        Thread? listeningThread;
        NetworkStream? networkStream;
        TcpClient? tcpClient;

        bool IsConnected = false;
        bool IsListening = false;
        bool IsSpeaking = false;
        readonly object syncLock = new();

        readonly CancellationTokenSource cts = new();

        public VoiceManager(IUser user)
        {
            User = user;

            _ = UserUI_Channel_Thread(cts.Token);

            deviceMonitorTimer.SetAcionOnTick(CheckForDeviceChanges);
            deviceMonitorTimer.Reset();
        }

        public bool JoinVoiceChat()
        {
            lock (syncLock)
            {
                if (IsConnected) 
                    return false;

                try
                {
                    if (!MakeConnection()) 
                        return false;
                    if (!InitializeAudioDevices()) 
                        return false;
                    if (!StartListeningThread()) 
                        return false;

                    IsConnected = true;
                    IsListening = true;
                    return true;
                }
                catch (Exception ex)
                {
                    ReportError("Voice_Chat", $"Failed to join voice chat: {ex.Message}");
                    DisposeResources();
                    return false;
                }
            }
        }
        public void ExitVoiceChat()
        {
            lock (syncLock)
            {
                if (IsConnected)
                    DisposeResources();
                
                IsListening = false;
            }
        }

        public bool StartSpeaking()
        {
            lock (syncLock)
            {
                if (!IsConnected || inputDevice is null || IsSpeaking)
                    return false;

                try
                {
                    inputDevice.DataAvailable += OnVoiceDataAvailable;
                    inputDevice.StartRecording();
                    IsSpeaking = true;
                    return true;
                }
                catch (Exception ex)
                {
                    ReportError("Voice_Chat", $"Failed to start speaking: {ex.Message}");
                    return false;
                }
            }
        }
        public bool StopSpeaking()
        {
            lock (syncLock)
            {
                if (!IsConnected || inputDevice is null || !IsSpeaking) 
                    return false;

                try
                {
                    inputDevice.DataAvailable -= OnVoiceDataAvailable;
                    inputDevice.StopRecording();
                    IsSpeaking = false;
                    return true;
                }
                catch (Exception ex)
                {
                    ReportError("Voice_Chat", $"Failed to stop speaking: {ex.Message}");
                    return false;
                }
            }
        }

        public void UpdateLoginGuid(Guid guid) => LoginGuid = guid;
        public void SetIP(string ip) => IP = ip;

        bool MakeConnection()
        {
            try
            {
                tcpClient = new TcpClient();
                tcpClient.Connect(IP, Port);
                networkStream = tcpClient.GetStream();

                networkStream.Write(LoginGuid.ToByteArray());
                return true;
            }
            catch (Exception ex)
            {
                ReportError("Connection", $"Failed to establish connection: {ex.Message}");
                return false;
            }
        }
        public bool ReinitializeWithSelectedMicrophone()
        {
            lock (syncLock)
            {
                if (IsConnected)
                {
                    // Останавливаем текущие устройства
                    if (inputDevice != null)
                    {
                        inputDevice.DataAvailable -= OnVoiceDataAvailable;
                        inputDevice.StopRecording();
                        inputDevice.Dispose();
                        inputDevice = null;
                    }

                    // Переинициализируем с новым устройством
                    return InitializeInputDevices(waveFormat);
                }
                return true; // Если не подключены, просто сохраняем выбор
            }
        }
        bool InitializeAudioDevices()
        {
            try
            {
                if (!InitializeInputDevices(waveFormat))
                    throw new My_Exception("InitializeInputDevices is false");

                if (!InitializeOutputDevices(waveFormat))
                    throw new My_Exception("InitializeOutputDevices is false");

                return true;
            }
            catch (Exception ex)
            {
                ReportError("Audio_Devices", $"Failed to initialize audio devices: {ex.Message}");
                return false;
            }
        }
        bool InitializeOutputDevices(WaveFormat waveFormat)
        {
            try
            {
                bufferStream = new BufferedWaveProvider(waveFormat);
                outputDevice = new();
                outputDevice.Init(bufferStream);

                return true;
            }
            catch (Exception ex)
            {
                ReportError("Audio_Devices", $"Failed to initialize audio devices: {ex.Message}");
                return false;
            }
        }
        bool InitializeInputDevices(WaveFormat waveFormat)
        {
            try
            {
                inputDevice = new WaveInEvent
                {
                    WaveFormat = waveFormat,
                    DeviceNumber = selectedDeviceNumber // Устанавливаем выбранное устройство
                };
                inputDevice.DataAvailable += OnVoiceDataAvailable;

                return true;
            }
            catch (Exception ex)
            {
                ReportError("Audio_Devices", $"Failed to initialize audio devices: {ex.Message}");
                return false;
            }
        }

        public int GetMicrophoneCount() => WaveInEvent.DeviceCount;
        void CheckForDeviceChanges()
        {
            var currentDevices = GetMicrophoneDevices();

            // Проверяем изменения
            if (!currentDevices.SequenceEqual(previousDevices))
            {
                // Устройства изменились
                previousDevices = currentDevices;
                User.OnInterfacesAction(ActionToIUser.OnMicrophonesInfo, new() { ["microphones"] = currentDevices });
            }

            deviceMonitorTimer.Reset();
        }
        public Dictionary<string, string> GetMicrophoneDevices()
        {
            var devices = new Dictionary<string, string> {
                { "auto", $"Устройство по умолчанию" }
            };

            for (int deviceId = 0; deviceId < WaveInEvent.DeviceCount; deviceId++)
            {
                var capabilities = WaveInEvent.GetCapabilities(deviceId);
                devices.Add(capabilities.ProductName, $"{capabilities.ProductName}, Channels: {capabilities.Channels}");
            }
            return devices;
        }
        public bool SelectMicrophoneByName(string deviceName)
        {
            if(deviceName == "auto")
            {
                selectedDeviceNumber = -1;
                return true;
            }

            for (int deviceId = 0; deviceId < WaveInEvent.DeviceCount; deviceId++)
            {
                var capabilities = WaveInEvent.GetCapabilities(deviceId);
                if (capabilities.ProductName.Contains(deviceName, StringComparison.OrdinalIgnoreCase))
                {
                    selectedDeviceNumber = deviceId;
                    return true;
                }
            }

            ReportError("Microphone", $"Device not found: {deviceName}");
            return false;
        }

        bool StartListeningThread()
        {
            try
            {
                listeningThread = new Thread(ListenForIncomingAudio) {
                    IsBackground = true,
                    Name = "VoiceChatListener"
                };
                listeningThread.Start();
                return true;
            }
            catch (Exception ex)
            {
                ReportError("Threading", $"Failed to start listening thread: {ex.Message}");
                return false;
            }
        }
        void OnVoiceDataAvailable(object? sender, WaveInEventArgs e)
        {
            lock (syncLock)
            {
                if (!IsSpeaking)
                    return;

                if (!IsConnected || networkStream is null || !networkStream.CanWrite)
                {
                    DisposeResources();
                    return;
                }

                try
                {
                    networkStream.Write(e.Buffer, 0, e.BytesRecorded);
                }
                catch { }
            }
        }
        void ListenForIncomingAudio()
        {
            try
            {
                outputDevice?.Play();
                var buffer = new byte[BufferSize];

                while (IsConnected && networkStream is not null)
                {
                    try
                    {
                        int bytesRead = networkStream.Read(buffer, 0, buffer.Length);

                        if (bytesRead == 0)
                            throw new My_Exception("Connection closed by remote host");

                        if (IsListening && bufferStream is not null)
                        {
                            bufferStream.AddSamples(buffer, 0, bytesRead);
                        }
                    }
                    catch (System.IO.IOException ex) when (ex.InnerException is SocketException socketEx && (socketEx.SocketErrorCode == SocketError.ConnectionAborted || socketEx.SocketErrorCode == SocketError.ConnectionReset))
                    {
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                ReportError("Listening", $"Audio listening error: {ex.Message}");
            }
            finally
            {
                DisposeResources();
            }
        }

        void ReportError(string title, string text) => User.OnInterfacesAction(ActionToIUser.MessageErrorOccurred, new() { ["title"] = title, ["text"] = text });
        void NotifyConnectionClosed() => User.OnInterfacesAction(ActionToIUser.ConnectionClosedVoiceChat, []);

        void DisposeResources()
        {
            lock (syncLock)
            {
                if (!IsConnected)
                    return;

                IsConnected = false;
                IsSpeaking = false;

                if (inputDevice is not null)
                {
                    inputDevice.DataAvailable -= OnVoiceDataAvailable;
                    inputDevice.StopRecording();
                    inputDevice.Dispose();
                    inputDevice = null;
                }

                if (outputDevice is not null)
                {
                    outputDevice?.Stop();
                    outputDevice = null;
                }                

                networkStream?.Close();
                networkStream = null;

                tcpClient?.Close();
                tcpClient = null;

                bufferStream = null;

                NotifyConnectionClosed();
            }
        }
    }
}