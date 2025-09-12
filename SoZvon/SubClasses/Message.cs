namespace SoZvon.SubClasses
{
    public record NotificationServer(TypeNotification Type, Dictionary<string, object> Dict);

    public class Message
    {
        static readonly List<Message> history_all_messages = [];
        static readonly object history_lock = new();

        public Guid Id;
        public byte[] message_data;
        public MessageInfo message_info;
        public DateTime dateTime;

        public Message(CommandText cmnd_text, byte[] buffer)
        {
            Id = Guid.Empty;
            message_info = new(cmnd_text);
            message_data = buffer;
            dateTime = DateTime.Now;
        }
        public Message(MessageInfo message_info_, byte[] buffer)
        {
            Id = Guid.Empty;
            message_info = message_info_;
            message_data = buffer;
            dateTime = DateTime.Now;
        }
        public Message(byte[] guid, MessageInfo message_info_, byte[] buffer)
        {
            Id = new Guid(guid);
            message_info = message_info_;
            message_data = buffer;
            dateTime = DateTime.Now;
        }
        public Message(byte[] guid, MessageInfo message_info_, byte[] buffer, DateTime dateTime_)
        {
            Id = new Guid(guid);
            message_info = message_info_;
            message_data = buffer;
            dateTime = dateTime_;
        }

        
        public static Message MakeMessage(byte[] guid, MessageInfo message_info, params object?[]? args)
        {
            byte cmnd_text = (byte)message_info.CommandText;
            byte[] body = GetBytesFromArgsMessage(args);

            int msg_lenght = body.Length + guid.Length + 1;

            if (msg_lenght > short.MaxValue) throw new Exception("all_lenght > short.MaxValue");
            
            byte[] lengthBytes = BitConverter.GetBytes((short)msg_lenght); //1 ДЛЯ COMMAND_TEXT

            System.IO.MemoryStream ms = new();

            ms.Write([0x07, 0x07], 0, 2); //СТАРТОВЫЕ СИМВОЛЫ ДЛЯ ВСЕХ СООБЩЕНИЙ
            ms.Write(lengthBytes, 0, lengthBytes.Length); //ЗАПИСЫВАЕМ ДЛИННУ СООБЩЕНИЯ
            ms.Write(guid, 0, guid.Length); //ЗАПИСЫВАЕМ GUID
            ms.WriteByte(cmnd_text); //ЗАПИСЫВАЕМ COMMAND_TEXT
            ms.Write(body, 0, body.Length); //ЗАПИСЫВАЕМ ТЕЛО СООБЩЕНИЯ

            message_info.MessageLength = (short)msg_lenght; //1 ДЛЯ COMMAND_TEXT

            return new Message(guid, message_info, ms.ToArray());
        }
        public void AddMessageToHistory()
        {
            lock (history_lock)
            {
                history_all_messages.Add(this);
            }
        }
        public static void ClearMessagesHistory()
        {
            lock (history_lock)
            {
                history_all_messages.Clear();
            }
        }
        public static Message? FindMessage(Predicate<Message> match)
        {
            lock (history_lock)
            {
                return history_all_messages.Find(match);
            }
        }
        public static byte[] GetBytesFromArgsMessage(params object?[]? args)
        {
            if (args is null || args.Length == 0) return [];

            List<byte> body = [];

            foreach (object? arg in args)
            {
                switch (arg)
                {
                    case null:
                        body.AddRange([0x00, 0x00]);
                        break;
                    case byte one_byte:
                        body.Add(one_byte);
                        break;
                    case bool boolean:
                        body.Add((byte)((boolean ? 1 : 0) + 1));
                        break;
                    case short num:
                        body.AddRange(BitConverter.GetBytes(num));
                        break;
                    case DateTime dateTime:
                        body.AddRange(BitConverter.GetBytes(dateTime.Ticks));
                        break;
                    case Guid guid:
                        body.AddRange(guid.ToByteArray());
                        break;
                    case byte[] bytes:
                        body.AddRange(bytes);
                        break;
                    case string str:
                        var strBytes = System.Text.Encoding.UTF8.GetBytes(str);

                        if (strBytes.Length > short.MaxValue) throw new ArgumentException($"String length exceeds maximum allowed size ({short.MaxValue} bytes)");
                        else if (strBytes.Length == 0)
                        {
                            body.AddRange([0x00, 0x00]);
                            break;
                        }

                        body.AddRange(BitConverter.GetBytes((short)strBytes.Length));
                        body.AddRange(strBytes);
                        break;
                    case object[] array:
                        if(array.Length != 0) 
                            body.AddRange(GetBytesFromArgsMessage(array));
                        break;
                    default:
                        throw new ArgumentException($"Unsupported type: {arg.GetType()}");
                }
            }

            return body.ToArray();
        }
    }
}
