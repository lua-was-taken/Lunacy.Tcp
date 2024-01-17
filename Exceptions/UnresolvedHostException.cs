namespace Lunacy.Tcp.Exceptions {
	public class UnresolvedHostException(string hostName) : Exception {
		public string HostName { get; protected init; } = hostName;
	}
}