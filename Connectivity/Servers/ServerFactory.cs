using Lunacy.Tcp.Connectivity.Clients;
using System.Net.Sockets;

namespace Lunacy.Tcp.Connectivity.Servers {
	public static class ServerFactory {
		public static IAsyncServer CreateAsyncServer(bool encryptedConnection = true) {
			return CreateAsyncServer(ClientConfig.Default, encryptedConnection);
		}

		public static IAsyncServer CreateAsyncServer(ClientConfig config, bool encryptedConnection = true) {
			AsyncRemoteClientServer server = new(
				defaultClientConfig: config,
				clientFactoryMethod: (IServer server, Socket socket) => {
					return ClientFactory.CreateAsyncClient(server.DefaultClientConfig, socket, encryptedConnection);
				}
			); 

			return server;
		}
		
		public static IServer CreateServer(bool encryptedConnection = true) {
			return CreateServer(ClientConfig.Default, encryptedConnection);
		}
		
		public static IServer CreateServer(ClientConfig config, bool encryptedConnection = true) {
			RemoteClientServer<IClient> server = new(
				defaultClientConfig: config,
				clientFactoryMethod: (IServer server, Socket socket) => {
					return ClientFactory.CreateClient(server.DefaultClientConfig, socket, encryptedConnection);
				} 
			);

			return server;
		}
	}
}