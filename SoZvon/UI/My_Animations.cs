using SoZvon.UI.Pages;
using SoZvon.UI.Room_Pages;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace SoZvon.UI
{
    public class My_Animations()
    {
        static bool VoicePanel_IsAnimating = false;
        static bool ServerInfo_IsAnimating = false;

        public static void Server_Info_Animation(LogInPage login_page)
        {
            if (ServerInfo_IsAnimating) return;

            ServerInfo_IsAnimating = true;

            Storyboard sb_angle = (Storyboard)login_page.Resources["RotateAnimation180"];
            int from_angle = 180;
            int to_angle = 0;

            Storyboard sb_height = (Storyboard)login_page.Resources["MoveServerInfo"];
            int from_height = 70;
            int to_height = 128;

            if (((RotateTransform)login_page.Server_Info.RenderTransform).Angle == 0)
            {
                from_angle = 0;
                to_angle = 180;

                from_height = 128;
                to_height = 70;
            }

            sb_angle.Completed += (s, args) => ServerInfo_IsAnimating = false;

            ((DoubleAnimation)sb_angle.Children[0]).From = from_angle;
            ((DoubleAnimation)sb_angle.Children[0]).To = to_angle;
            sb_angle.Begin(login_page);

            ((DoubleAnimation)sb_height.Children[0]).From = from_height;
            ((DoubleAnimation)sb_height.Children[0]).To = to_height;
            sb_height.Begin(login_page);
        }
        public static void VoicePanel_Animation(RoomPage room_page, Rectangle rect)
        {
            if (VoicePanel_IsAnimating) return;
            
            VoicePanel_IsAnimating = true;

            room_page.HideRoomInfoPanel.Cursor = Cursors.ScrollW;

            Storyboard sb_translate = (Storyboard)room_page.Resources["RoomInfoPanelAnimation"];
            double Cords_From = 0;
            double Cords_To = room_page.MainGrid_RoomInfo.ActualWidth - (rect.Width - 4);

            Storyboard sb_margin = (Storyboard)room_page.Resources["CentreGridMarginAnimation"];
            double margin_offset = room_page.MainGrid_RoomInfo.ActualWidth - rect.Width + 4;

            if (room_page.Centre_Window_Grid.Margin.Right != 237)
            {
                Cords_To = 0;
                Cords_From = room_page.MainGrid_RoomInfo.ActualWidth - (rect.Width - 4);
                margin_offset = -room_page.MainGrid_RoomInfo.ActualWidth + (rect.Width - 4);
                room_page.HideRoomInfoPanel.Cursor = Cursors.ScrollE;
            }

            sb_translate.Completed += (s, args) => VoicePanel_IsAnimating = false;

            ((DoubleAnimation)sb_translate.Children[0]).From = Cords_From;
            ((DoubleAnimation)sb_translate.Children[0]).To = Cords_To;

            ((ThicknessAnimation)sb_margin.Children[0]).From = room_page.Centre_Window_Grid.Margin;
            ((ThicknessAnimation)sb_margin.Children[0]).To = new Thickness(room_page.Centre_Window_Grid.Margin.Left, 0, room_page.Centre_Window_Grid.Margin.Right - margin_offset, 0);

            sb_translate.Begin(room_page);
            sb_margin.Begin(room_page);
        }
        public static void ErrorGrid_Animation(Grid grid_1, double timeSpan, out Storyboard storyboard)
        {
            storyboard = new();

            DoubleAnimation opacity_anim = new(1, 0, new Duration(TimeSpan.FromMilliseconds(timeSpan)));

            Storyboard.SetTarget(opacity_anim, grid_1);
            Storyboard.SetTargetProperty(opacity_anim, new PropertyPath(UIElement.OpacityProperty));
            storyboard.Children.Add(opacity_anim);

            storyboard.Begin(grid_1);
        }
    }
}
