namespace SoZvon.ServerAPIManager
{
    public interface IManagerAPI
    {
        void OnIUserAction(Main_Thread.ActionFromIUser action_IUser, Dictionary<string, object> dict);
    }
}
