namespace SoZvon.SubClasses
{
    public record Room_User
    {
        public string Login { get; }
        public string Name { get; set; }
        public string Room_Name { get; set; }
        public bool InVoiceChat { get; set; }

        readonly My_Timer texting_timer = new(4);

        public Room_User(string Login, string Name, string Room_Name, bool InVoiceChat)
        {
            this.Login = Login;
            this.Name = Name;
            this.Room_Name = Room_Name;
            this.InVoiceChat = InVoiceChat;
        }

        public void SetActionOnTexting(Action action) => texting_timer.SetAcionOnTick(action);
        public bool HasActionOnTexting() => texting_timer.HasAction;
        public void StartTexting() => texting_timer.Reset();
    }

    public sealed class RoomManager
    {
        readonly Dictionary<string, Room> rooms = [];
        readonly ReaderWriterLockSlim roomsLock = new();

        public Room GetOrCreateRoom(string roomName, int numUsers, string creatorLogin, out bool isNewRoom)
        {
            ArgumentException.ThrowIfNullOrEmpty(roomName);
            ArgumentException.ThrowIfNullOrEmpty(creatorLogin);

            roomsLock.EnterWriteLock();
            try
            {
                // Проверяем еще раз на случай, если комната была добавлена другой потоком
                if (rooms.TryGetValue(roomName, out var existingRoom))
                {
                    existingRoom.UpdateRoomInfo(numUsers, creatorLogin);
                    isNewRoom = false;
                    return existingRoom;
                }
                else
                {
                    Room newRoom = new(roomName, numUsers, creatorLogin);
                    rooms.Add(roomName, newRoom);
                    isNewRoom = true;
                    return newRoom;
                }
            }
            finally
            {
                roomsLock.ExitWriteLock();
            }
        }
        public bool TryGetRoom(string roomName, out Room? room)
        {
            ArgumentException.ThrowIfNullOrEmpty(roomName);

            roomsLock.EnterReadLock();
            try
            {
                return rooms.TryGetValue(roomName, out room);
            }
            finally
            {
                roomsLock.ExitReadLock();
            }
        }
        public void ClearRooms()
        {
            roomsLock.EnterWriteLock();
            try
            {
                rooms.Clear();
            }
            finally
            {
                roomsLock.ExitWriteLock();
            }
        }

        // Дополнительные полезные методы
        public bool GetUserFromRoom(string roomName, string login, out Room_User? user)
        {
            ArgumentNullException.ThrowIfNull(roomName);

            roomsLock.EnterReadLock();
            try
            {
                if (rooms.TryGetValue(roomName, out var room))
                {
                    return room.TryGetUser(login, out user);
                }

                user = null;
                return false;
            }
            finally
            {
                roomsLock.ExitReadLock();
            }
        }
        public bool ExecuteWithRoom(string roomName, Action<Room> action)
        {
            ArgumentException.ThrowIfNullOrEmpty(roomName);

            roomsLock.EnterReadLock();
            try
            {
                if (rooms.TryGetValue(roomName, out var room))
                {
                    action(room);
                    return true;
                }
                return false;
            }
            finally
            {
                roomsLock.ExitReadLock();
            }
        }
        public bool ClearRoomsAddRange(List<Room> _rooms)
        {
            ArgumentNullException.ThrowIfNull(_rooms);

            roomsLock.EnterWriteLock();
            try
            {
                foreach (var room in _rooms)
                {
                    if (rooms.ContainsKey(room.Name_Room))
                        return false;
                }

                rooms.Clear();

                foreach (var room in _rooms)
                {
                    rooms.Add(room.Name_Room, room);
                }

                return true;
            }
            finally
            {
                roomsLock.ExitWriteLock();
            }
        }
        public bool FindRoomClearUsersAddRange(string roomName, List<Room_User> _users)
        {
            ArgumentNullException.ThrowIfNull(roomName);

            roomsLock.EnterReadLock();
            try
            {
                if (rooms.TryGetValue(roomName, out var room))
                {
                    return room.FindRoomClearUsersAddRange(_users);
                }
                return false;
            }
            finally
            {
                roomsLock.ExitReadLock();
            }
        }
        public bool GetUsersInRoom(string roomName, out List<Room_User>? _users)
        {
            ArgumentNullException.ThrowIfNull(roomName);

            roomsLock.EnterReadLock();
            try
            {
                if (rooms.TryGetValue(roomName, out var room))
                {
                    _users = room.GetUsers();
                    return true;
                }

                _users = null;
                return false;
            }
            finally
            {
                roomsLock.ExitReadLock();
            }
        }

        // Реализация IDisposable для корректного освобождения ресурсов
        ~RoomManager()
        {
            roomsLock?.Dispose();
        }
    }


    public class Room
    {
        readonly Dictionary<string, Room_User> users = [];
        readonly ReaderWriterLockSlim roomLock = new();

        public int Num_Users { get; private set; }
        public string Name_Room { get; private set; }
        public string Login_Creator { get; private set; }

        public Room(string roomName, int numUsers, string creatorLogin)
        {
            ArgumentException.ThrowIfNullOrEmpty(roomName);
            ArgumentNullException.ThrowIfNull(creatorLogin);

            Name_Room = roomName;
            Num_Users = numUsers;
            Login_Creator = creatorLogin;
        }

        public void UpdateRoomInfo(int numUsers, string creatorLogin)
        {
            ArgumentNullException.ThrowIfNull(creatorLogin);

            Num_Users = numUsers;
            Login_Creator = creatorLogin;
        }

        public List<Room_User> GetUsers()
        {
            roomLock.EnterReadLock();
            try
            {
                return [.. users.Values];
            }
            finally
            {
                roomLock.ExitReadLock();
            }
        }
        public bool TryGetUser(string login, out Room_User? room_User)
        {
            ArgumentNullException.ThrowIfNull(login);

            roomLock.EnterReadLock();
            try
            {
                return users.TryGetValue(login, out room_User);
            }
            finally
            {
                roomLock.ExitReadLock();
            }
        }
        public bool AddUser(Room_User user)
        {
            roomLock.EnterWriteLock();
            try
            {
                return users.TryAdd(user.Login, user);
            }
            finally
            {
                roomLock.ExitWriteLock();
            }
        }
        public bool RemoveUser(string login)
        {
            ArgumentNullException.ThrowIfNull(login);

            roomLock.EnterWriteLock();
            try
            {
                return users.Remove(login);
            }
            finally
            {
                roomLock.ExitWriteLock();
            }
        }
        public bool HasUser(string login)
        {
            ArgumentNullException.ThrowIfNull(login);

            roomLock.EnterReadLock();
            try
            {
                return users.ContainsKey(login);
            }
            finally
            {
                roomLock.ExitReadLock();
            }
        }
        public bool FindRoomClearUsersAddRange(List<Room_User> _users)
        {
            ArgumentNullException.ThrowIfNull(_users);

            roomLock.EnterWriteLock();
            try
            {
                foreach (var user in _users)
                {
                    if (users.ContainsKey(user.Login))
                        return false;
                }

                users.Clear();

                foreach (var user in _users)
                {
                    users.Add(user.Login, user);
                }

                return true;
            }
            finally
            {
                roomLock.ExitWriteLock();
            }
        }
        public bool ChangeUserInVoiceChat(string login, bool inVoiceChat, out Room_User user)
        {
            ArgumentNullException.ThrowIfNull(login);
            ArgumentNullException.ThrowIfNull(inVoiceChat);

            roomLock.EnterWriteLock();
            try
            {
                if (!users.TryGetValue(login, out var temp_user))
                {
                    user = null!;
                    return false;
                }

                user = temp_user;

                if (temp_user is not null)
                    temp_user.InVoiceChat = inVoiceChat;                
                
                return user is not null;
            }
            finally
            {
                roomLock.ExitWriteLock();
            }
        }

        ~Room()
        {
            roomLock?.Dispose();
        }
    }
}
