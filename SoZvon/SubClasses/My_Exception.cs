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

    public class UserException(string? title, string message) : Exception(message)
    {
        public string? Title { get; } = title;
        public string Text { get; } = message;

        public UserException(string message) : this(null, message) { }
    }
}
