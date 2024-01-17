using Lunacy.Tcp.Connectivity.Clients;

namespace Lunacy.Tcp.Connectivity.Servers {
	public static class IAsyncServerExtensions {
		public static async Task<IAsyncClient> GetClientAsync(this IAsyncServer server) {
			return await server.GetClientAsync(CancellationToken.None);
		}
	}
}