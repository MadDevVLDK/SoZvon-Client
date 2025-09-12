namespace SoZvon.SubClasses
{
    public class My_Timer(double seconds)
    {
        readonly System.Timers.Timer timer = new() { Interval = seconds * 1000 };

        public bool IsWorking { get { return timer.Enabled; } }
        public Action action { get; private set; }
        public bool HasAction
        {
            get { return action is not null; }
        }

        public void Start()
        {
            timer.Elapsed += Tick;
            timer.Start();
        }
        public void Stop()
        {
            timer.Elapsed -= Tick;
            timer.Stop();
        }
        public void Reset()
        {
            Stop();
            Start();
        }
        public virtual void Tick(object? sender, EventArgs e)
        {
            Stop();
            action?.Invoke();
        }

        public void SetAcionOnTick(Action action_) => action = action_;
    }
}
