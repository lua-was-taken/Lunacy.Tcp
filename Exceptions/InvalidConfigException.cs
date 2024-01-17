namespace Lunacy.Tcp.Exceptions {
    public class InvalidConfigException : Exception {
        public InvalidConfigException() { }
        public InvalidConfigException(string message) : base(message) { }
    }
}