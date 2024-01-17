
using Lunacy.Tcp.Connectivity.Clients;

namespace Lunacy.Tcp.Connectivity.Servers {
    public interface IAsyncServer<T> : IAsyncServer where T : IAsyncClient {
        new Task<T> GetClientAsync(CancellationToken token);
    }

	public interface IAsyncServer : IServer {
		new event EventHandler<PacketHandle>? PacketPublished;
        Task<IAsyncClient> GetClientAsync(CancellationToken token);
    }
}