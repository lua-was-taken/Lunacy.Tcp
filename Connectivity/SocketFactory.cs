using System.Net.Sockets;

namespace Lunacy.Tcp.Connectivity {
	internal static class SocketFactory {
		public static Socket CreateTcpSocket(int port) {
			Socket socket = CreateTcpSocket();
			if(port > 0) {
				socket.Bind(localEP: EndPointFactory.CreateLocalEndPoint(port));
			}

			return socket;
		}

		public static Socket CreateTcpSocket() {
			return new Socket(SocketType.Stream, ProtocolType.Tcp );
		}
	}
}