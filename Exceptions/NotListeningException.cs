namespace Lunacy.Tcp.Exceptions {
    public class NotListeningException : Exception {
        public NotListeningException() : base() { }
        public NotListeningException(string message) : base(message) { }
    }
}