using Lunacy.Tcp.Enums;
using System.Net;

namespace Lunacy.Tcp.Connectivity.Clients {
	public interface IClient : IDisposable {
		bool GracefulDisconnect { get; }

		event EventHandler? Connected;
		event EventHandler? Disconnected;
		event EventHandler<Packet>? PacketPublished;

		Task<int> SendAsync(Packet packet, CancellationToken token);
		Task<int> SendAsync(Packet packet, PacketOrderType orderType, CancellationToken token);

		ClientConfig Config { get; }
		CancellationToken DisconnectToken { get; }
        ConnectionDirectionType Direction { get; }
        bool IsConnected { get; }

		IPEndPoint LocalEndPoint { get; }
		IPEndPoint? RemoteEndPoint { get; }

		string GetRemoteHost();
		string GetLocalHost();

		string SessionId { get; }
		Task<string> GetSessionIdAsync(CancellationToken token);

		Task<bool> ConnectAsync(IPEndPoint remoteEndPoint, CancellationToken token);
		Task<bool> DisconnectAsync();
		void DisconnectFast();

		void UpdateConnection(ConnectionDirectionType directionType);
	}
}