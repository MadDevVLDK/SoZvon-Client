using System;
using System.Threading.Channels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace SoZvon.UI.SubClasses
{
    public class NotifyMsgManager
    {
        readonly Dictionary<string, NotifyMessage> ErrorMessages = [];

        private readonly CancellationTokenSource _cts = new();
        private readonly Channel<NotifyMessage> _notificationChannel = Channel.CreateUnbounded<NotifyMessage>();
        private readonly SemaphoreSlim notificationsSemaphore;
        private const int Max_Notifications = 3;
        internal StackPanel NotificationsStackPanel { get; private set; }
        internal readonly IMainWindow mainWindow;

        public NotifyMsgManager(IMainWindow mainWindow_)
        {
            notificationsSemaphore = new SemaphoreSlim(Max_Notifications, Max_Notifications);

            mainWindow = mainWindow_;
            NotificationsStackPanel = mainWindow.errorStackPanel_ref;

            _ = MainThread_NotificationMessages(_cts.Token);
        }

        public async void New_NotifyMessage(string errorTitle, string errorText, Color color, int animTime = 3000)
        {
            if(_notificationChannel.Reader.Count > 3) 
                return;

            await _notificationChannel.Writer.WriteAsync(new NotifyMessage(this, errorTitle, errorText, animTime, color));
        }
        async Task MainThread_NotificationMessages(CancellationToken cancellationToken)
        {
            try
            { 
                await foreach (var notifyMessage in _notificationChannel.Reader.ReadAllAsync(cancellationToken))
                {
                    await notificationsSemaphore.WaitAsync(cancellationToken);

                    try
                    {
                        string tag = "Error_" + Guid.NewGuid().ToString();

                        notifyMessage.StartProperties(tag);

                        ErrorMessages.Add(tag, notifyMessage);
                    }
                    catch
                    {
                        notificationsSemaphore.Release();
                    }
                }
            }
            catch (OperationCanceledException) { return; }
            catch { }
        }
        
        internal void OnCloseNotifyMessage(NotifyMessage Notify_msg)
        {
            NotificationsStackPanel.Children.Remove(Notify_msg.Notify_grid);
            Notify_msg.EndTimer();

            try
            {
                notificationsSemaphore.Release();
            }
            catch { }
        }

        internal void OnEnterAnyNotifyMessage()
        {
            foreach (var keyValue in ErrorMessages)
            {
                keyValue.Value.Notify_grid.BeginAnimation(UIElement.OpacityProperty, null);
                keyValue.Value.EndTimer();
            }
        }
        internal void OnLeaveAnyNotifyMessage()
        {
            foreach (var keyValue in ErrorMessages)
            {
                keyValue.Value.storyboard.Begin(keyValue.Value.Notify_grid);
                keyValue.Value.StartTimer();
            }
        }

        public void CloseNotifyWithTag(string tag_error)
        {
            if (ErrorMessages.TryGetValue(tag_error, out NotifyMessage? notifyMessage))
            {
                notifyMessage.EndProperties(this, EventArgs.Empty);

                OnLeaveAnyNotifyMessage();

                ErrorMessages.Remove(tag_error);
            }
        }
    }
    public class NotifyMessage
    {
        public NotifyMsgManager notifyMsgManager { get; private set; }

        public string title { get; private set; }
        public string text { get; private set; }
        public System.Timers.Timer timer { get; private set; }
        public Color color { get; private set; }

        public Grid Notify_grid { get; private set; }
        public Storyboard storyboard { get; private set; }

        public NotifyMessage(NotifyMsgManager notifyMsgManager_, string title_, string text_, int anim_time, Color color_)
        {
            notifyMsgManager = notifyMsgManager_;
            title = title_;
            text = text_;
            timer = new() { Interval = anim_time };
            color = color_;
        }

        public void StartProperties(string tag)
        {
            string error_title = title;
            string error_text = text;
            string guid = Guid.NewGuid().ToString();

            Grid grid_1 = new() { Opacity = 1, Margin = new Thickness(0, 3, 0, 0), VerticalAlignment = VerticalAlignment.Bottom, HorizontalAlignment = HorizontalAlignment.Left, MinHeight = 75, MaxHeight = 200, MinWidth = 243, MaxWidth = 300 };

            grid_1.Children.Add(new Rectangle 
            { 
                Fill = new SolidColorBrush(Color.FromRgb(255, 243, 222)),
                RadiusX = 5,
                RadiusY = 5, 
                StrokeThickness = 1, 
                Stroke = new SolidColorBrush(Color.FromRgb(0, 0, 0))
            });
            grid_1.Children.Add(new TextBlock 
            { 
                Text = error_title,
                FontFamily = new FontFamily("Comic Sans MS"),
                FontSize = 16, 
                Margin = new Thickness(10, 13, 43, 0),
                Foreground = new SolidColorBrush(color),
                VerticalAlignment = VerticalAlignment.Top
            });
            grid_1.Children.Add(new TextBlock 
            { 
                Text = error_text,
                FontFamily = new FontFamily("Comic Sans MS"),
                TextWrapping = TextWrapping.Wrap,
                FontSize = 18,
                Margin = new Thickness(10, 39, 14, 7) 
            });

            grid_1.MouseEnter += (_, _) => notifyMsgManager.OnEnterAnyNotifyMessage();
            grid_1.MouseLeave += (_, _) => notifyMsgManager.OnLeaveAnyNotifyMessage();

            Grid grid_2 = new() {
                Name = "Close_Error",
                Tag = tag,
                Cursor = Cursors.Hand,
                Margin = new Thickness(0, 8, 7, 0),
                VerticalAlignment = VerticalAlignment.Top,
                HorizontalAlignment = HorizontalAlignment.Right,
                Height = 27,
                Width = 27 
            };

            grid_2.Children.Add(new Rectangle 
            { 
                Fill = new SolidColorBrush(Color.FromRgb(236, 226, 201)),
                RadiusX = 20,
                RadiusY = 20,
                StrokeThickness = 1,
                Stroke = new SolidColorBrush(Color.FromRgb(93, 93, 93)),
                Tag = "Background"
            });
            grid_2.Children.Add(new TextBlock 
            { 
                Text = "X",
                FontFamily = new FontFamily("Comic Sans MS"), 
                FontSize = 14, 
                Margin = new Thickness(8, 4, 0, 5), 
                VerticalAlignment = VerticalAlignment.Center 
            });

            grid_2.MouseUp += notifyMsgManager.mainWindow.AnyButton_UpMouse;
            grid_2.MouseDown += notifyMsgManager.mainWindow.AnyButton_DownMouse;
            grid_2.MouseEnter += notifyMsgManager.mainWindow.AnyButton_EnterLeaveMouse;
            grid_2.MouseLeave += notifyMsgManager.mainWindow.AnyButton_EnterLeaveMouse;

            grid_1.Children.Add(grid_2);

            notifyMsgManager.NotificationsStackPanel.Children.Insert(0, grid_1);
            My_Animations.ErrorGrid_Animation(grid_1, timer.Interval, out Storyboard storyboard_);

            Notify_grid = grid_1;
            storyboard = storyboard_;

            StartTimer();
        }
        public void EndProperties(object? sender, EventArgs e)
        {
            notifyMsgManager.mainWindow.MakeAction_Form(new Action(() =>
            {
                Notify_grid.MouseEnter -= (_, _) => notifyMsgManager.OnEnterAnyNotifyMessage();
                Notify_grid.MouseLeave -= (_, _) => notifyMsgManager.OnLeaveAnyNotifyMessage();
                Notify_grid.Opacity = 0;
                Notify_grid.BeginAnimation(UIElement.OpacityProperty, null);

                notifyMsgManager.OnCloseNotifyMessage(this);
            }));
        }

        public void StartTimer()
        {
            timer.Elapsed += EndProperties;
            timer.Start();
        }
        public void EndTimer()
        {
            timer.Elapsed -= EndProperties;
            timer.Stop();
        }
    }
}
