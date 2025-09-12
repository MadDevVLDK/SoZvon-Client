namespace SoZvon.VoiceChatManager
{
    public interface IVoiceManager
    {
        void OnIUserAction(Main_Thread.ActionFromIUser action_IUser, Dictionary<string, object> dict);

        bool JoinVoiceChat();
        void ExitVoiceChat();
        bool StartSpeaking();
        bool StopSpeaking();

        Dictionary<string, string> GetMicrophoneDevices();
    }

}
