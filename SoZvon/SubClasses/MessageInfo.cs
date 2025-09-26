namespace SoZvon.SubClasses
{
    public class MessageInfo
    {
        public const byte lenght_message_head = 21;

        public short MessageLength { get; set; } = 0;
        public MessageType MessageType { get; private set; } = 0;
        public MessageSender Sender { get; private set; } = 0;
        public CommandText CommandText
        {
            get { return _commandText; }
            set
            {
                _commandText = value;
                switch ((byte)value)
                {
                    case < 50:
                        {
                            MessageType = MessageType.Command;
                            Sender = MessageSender.Server;
                            break;
                        }
                    case < 100:
                        {
                            MessageType = MessageType.Command;
                            Sender = MessageSender.Client;
                            break;
                        }
                    case < 150:
                        {
                            MessageType = MessageType.Text;
                            Sender = MessageSender.Server;
                            break;
                        }
                    case < 200:
                        {
                            MessageType = MessageType.Text;
                            Sender = MessageSender.Client;
                            break;
                        }
                }
            }
        }

        CommandText _commandText = CommandText.None;
        
        public MessageInfo(short length, CommandText commandText)
        {
            MessageLength = length;
            CommandText = commandText;
        }
        public MessageInfo(short length) => MessageLength = length;
        public MessageInfo(CommandText commandText) => CommandText = commandText;
        
        public static bool ReadMessageInfo(ref List<byte> response_array, ref Message message)
        {
            try
            {
                message.Id = Read_Guid_Bytes(ref response_array);
                message.message_info.CommandText = (CommandText)Read_Byte_Bytes(ref response_array);
            }
            catch
            {
                return false;
            }
            return true;
        }
        
        public static byte[] Read_Num_Bytes(ref List<byte> response_array, int num_to_read, int offset = 0)
        {
            if (offset < 0) 
                throw new ArgumentException("Offset must be 0 or greater");

            if (response_array.Count - offset < num_to_read) 
                throw new ArgumentException("Response array is too short for the specified message length");

            byte[] messageBytes = [.. response_array.GetRange(offset, num_to_read)];

            response_array.RemoveRange(offset, num_to_read);

            return messageBytes;
        }

        public static byte[] Read_Bytes(ref List<byte> response_array) => Read_Num_Bytes(ref response_array, Read_Int16_Bytes(ref response_array));
        public static byte Read_Byte_Bytes(ref List<byte> response_array) => Read_Num_Bytes(ref response_array, 1)[0];
        public static Guid Read_Guid_Bytes(ref List<byte> response_array) => new(Read_Num_Bytes(ref response_array, 16));
        public static DateTime Read_DateTime_Bytes(ref List<byte> response_array) => DecodeDateTime(Read_Num_Bytes(ref response_array, 8));
        public static string Read_String_Bytes(ref List<byte> response_array) => System.Text.Encoding.UTF8.GetString(Read_Num_Bytes(ref response_array, Read_Int16_Bytes(ref response_array)));
        public static short Read_Int16_Bytes(ref List<byte> response_array) => BitConverter.ToInt16(Read_Num_Bytes(ref response_array, 2));
        public static bool Read_Bool_Bytes(ref List<byte> response_array) => Read_Byte_Bytes(ref response_array) == 1;
        public static bool Read_MyBool_Bytes(ref List<byte> response_array) => Read_Byte_Bytes(ref response_array) == 2;

        public static DateTime DecodeDateTime(byte[] data)
        {
            return TimeZoneInfo.ConvertTimeFromUtc(DateTimeOffset.FromUnixTimeMilliseconds(BitConverter.ToInt64(data, 0) / 1_000_000).UtcDateTime, TimeZoneInfo.Local);
        } 
    }

    public enum MessageType : byte 
    {
        None = 0,
        Command = 1,
        Text = 2,
    }
    public enum MessageSender : byte 
    {
        None = 0,
        Server = 1,
        Client = 2,
    }
    public enum CommandText : byte 
    {
        None = 0,

        // Команды клиенту от сервера
        ShowRooms = 1,
        PeopleRoom = 2,
        ReplyOk = 3,
        ReplyError = 4,
        Notification_Serv = 5,

        // Команды серверу от клиента
        LogIn = 50,
        Register = 51,
        EnterRoom = 52,
        ExitRoom = 53,
        AddRoom = 54,
        DeleteRoom = 55,
        Notification_Cl = 56,
        HeartBeat = 99,

        // Сообщение от одного клиента другому клиенту (сообщение, которое отправляет сервер)
        Info = 100,
        Private_Serv = 101,
        All_Serv = 102,

        // Сообщение от одного клиента другому клиенту (сообщение, которое отправляет клиент)
        Private_Cl = 150,
        All_Cl = 151,
    }
    public enum MessageFromUser : byte
    {
        None = 0,
        Public = 102,
        Private = 151
    }
    public enum TypeNotification : byte
    {
        None = 0,

        Texting = 1,
        UploadingFile = 2,
        EndUploadingFile = 3,
        JoinRoom = 4,
        ExitRoom = 5,
        JoinVoiceChat = 6,
        ExitVoiceChat = 7,
        AddOrChangeRoom = 8,
        DeleteRoom = 9
    }
}