using Lunacy.Tcp.Connectivity.Clients;
using Lunacy.Tcp.Utility;
using System.Net.Sockets;

namespace Lunacy.Tcp.Connectivity.Servers {
	public interface IServer : IDisposable {
		SynchronizedCollection<IClient> ConnectedClients { get; }
		ClientConfig DefaultClientConfig { get; set; }
		bool IsOpen { get; }
		Socket Socket { get; }

		event EventHandler<IClient>? ClientConnected;
		event EventHandler<IClient>? ClientCreated;
		event EventHandler<IClient>? ClientDestroyed;
		event EventHandler<IClient>? ClientDisconnected;
		event EventHandler<Packet>? PacketPublished;

		bool Close();
		Task DisconnectAllAsync();
		void Open(int port);
	}
}