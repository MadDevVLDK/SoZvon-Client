namespace SoZvon.Main_Thread
{
    public interface IUser
    {
        void OnInterfacesAction(ActionToIUser action_IUser, Dictionary<string, object> dict);
        void On_Button_Clicked(string button_name, Dictionary<string, object> dict);
    }
}
