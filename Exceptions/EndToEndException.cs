namespace Lunacy.Tcp.Exceptions {
    public sealed class EndToEndException : Exception {
        public EndToEndException() :base() { }
        public EndToEndException(string message) : base(message) { }
        public EndToEndException(string message, Exception innerException) : base(message, innerException) { }
    }
}