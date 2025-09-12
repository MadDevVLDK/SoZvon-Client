namespace SoZvon.UI
{
    public interface IApplicationUI
    {
        void OnIUserAction(Main_Thread.ActionFromIUser action_IUser, Dictionary<string, object> dict);
    }
}
