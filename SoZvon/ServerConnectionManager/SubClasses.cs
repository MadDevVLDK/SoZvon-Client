namespace SoZvon.ServerConnectionManager
{
    record ConnectionAttempt(string IP, string Port, int Timeout_Millisecond, Action? Action);

}
