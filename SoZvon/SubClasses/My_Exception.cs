namespace SoZvon.SubClasses
{
    public class My_Exception : Exception
    {
        public string? Title { get; }
        public string Details { get; }

        public My_Exception(string message) : base(message)
        {
            Title = null;
            Details = message;
        }
        public My_Exception(string title, string message) : base(message)
        {
            Title = title;
            Details = message;
        }

        public My_Exception(string title, string message, Exception innerException) : base(message, innerException)
        {
            Title = title;
            Details = message;
        }
    }
}
