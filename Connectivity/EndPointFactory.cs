using System.Net;

namespace Lunacy.Tcp.Connectivity {
	internal static class EndPointFactory {
		public static IPEndPoint CreateLocalEndPoint(int port) {
			return new IPEndPoint(IPAddress.Any, port);
		}
	}
}