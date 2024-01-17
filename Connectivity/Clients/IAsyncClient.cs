namespace Lunacy.Tcp.Connectivity.Clients {
	public interface IAsyncClient : IClient {
		new event EventHandler<PacketHandle>? PacketPublished;

        IClient BaseClient { get; }
        Task<PacketHandle?> GetPacketAsync(CancellationToken token, bool waitForPacket = true);
	}
}