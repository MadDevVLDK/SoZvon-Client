using ActionFromIUser = SoZvon.Main_Thread.ActionFromIUser;

namespace SoZvon.ServerConnectionManager
{
    public interface IServerConnection
    {
        bool IsConnected { get; }

        void OnIUserAction(ActionFromIUser action_IUser, Dictionary<string, object> dict);
        Task New_ConnectionAttempt(int timeout_millisecond = 2000, Action? action = null);
    }
}
