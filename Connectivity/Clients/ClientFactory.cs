using System.Net.Sockets;

namespace Lunacy.Tcp.Connectivity.Clients {
	public static class ClientFactory {

		public static IClient CreateClient(bool encryptedConnection = true) {
			return CreateClient(ClientConfig.Default, SocketFactory.CreateTcpSocket(), encryptedConnection);
		}

		public static IClient CreateClient(Socket baseSocket, bool encryptedConnection = true) {
			return CreateClient(ClientConfig.Default, baseSocket, encryptedConnection);
		}

		public static IClient CreateClient(ClientConfig config, bool encryptedConnection = true) {
			return CreateClient(config, SocketFactory.CreateTcpSocket(), encryptedConnection);
		}

		public static IClient CreateClient(ClientConfig config, Socket baseSocket, bool encryptedConnection = true) {
			if(encryptedConnection) {
				return new SecureRemoteClient(config, baseSocket);
			} else {
				return new RemoteClient(config, baseSocket);
			}
		}

		public static IAsyncClient CreateAsyncClient(IClient baseClient) {
			return new AsyncRemoteClient(baseClient);
		}

		public static IAsyncClient CreateAsyncClient(bool encryptedConnection = true) {
			return CreateAsyncClient(ClientConfig.Default, SocketFactory.CreateTcpSocket(), encryptedConnection);
		}

		public static IAsyncClient CreateAsyncClient(Socket baseSocket, bool encryptedConnection = true) {
			return CreateAsyncClient(ClientConfig.Default, baseSocket, encryptedConnection);
		}

		public static IAsyncClient CreateAsyncClient(ClientConfig config, bool encryptedConnection = true) {
			return CreateAsyncClient(config, SocketFactory.CreateTcpSocket(), encryptedConnection);
		}

		public static IAsyncClient CreateAsyncClient(ClientConfig config, Socket baseSocket, bool encryptedConnection = true) {
			return new AsyncRemoteClient(CreateClient(config, baseSocket, encryptedConnection));
		}
	}
}