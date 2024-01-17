using Lunacy.Tcp.Serializers;

namespace Lunacy.Tcp.Connectivity.Clients {
	public static class IAsyncClientExtensions {
		public static async Task<PacketHandle?> GetPacketAsync(this IAsyncClient client, bool waitForPacket = true) {
			return await client.GetPacketAsync(CancellationToken.None, waitForPacket);
		}

		public static async Task<T?> GetDataAsync<T>(this IAsyncClient client, bool waitForPacket = true) => await GetDataAsync<T>(client, CancellationToken.None, waitForPacket);
		public static async Task<T?> GetDataAsync<T>(this IAsyncClient client, CancellationToken token, bool waitForPacket = true) {
			PacketHandle? packetHandle = await client.GetPacketAsync(token, waitForPacket);
			if(packetHandle != null) {
				return packetHandle.GetData<T>();
			}

			return default;
		}

		public static async Task<Memory<byte>> GetPayloadAsync(this IAsyncClient client, bool waitForPacket = true) => await GetPayloadAsync(client, CancellationToken.None, waitForPacket);
		public static async Task<Memory<byte>> GetPayloadAsync(this IAsyncClient client, CancellationToken token, bool waitForPacket = true) {
			PacketHandle? packetHandle = await client.GetPacketAsync(token, waitForPacket);
			if(packetHandle != null) {
				return packetHandle.GetPayload();
			}

			return Memory<byte>.Empty;
		}

		public static T GetData<T, TSerializer>(this PacketHandle packetHandle) where TSerializer : ISerializer, new() {
			TSerializer serializer = new();
			Packet packet = packetHandle.GetPacket();
			
			Memory<byte> payload = packet.Payload;
			return serializer.Deserialize<T>(payload);
		}
	}
}