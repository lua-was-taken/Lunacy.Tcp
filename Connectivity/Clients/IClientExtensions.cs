using Lunacy.Tcp.Enums;

namespace Lunacy.Tcp.Connectivity.Clients {
	public static class IClientExtensions {
		public static Task<bool> ConnectAsync(this IClient client, string hostname, int port) => client.ConnectAsync(hostname, port, CancellationToken.None);
		public static Task<bool> ConnectAsync(this IClient client, string hostname, int port, CancellationToken token) {
			return client.ConnectAsync(NameResolver.Resolve(hostname, port), token);
		}

		public static Task<bool> ConnectAsync(this IClient client, string host) => client.ConnectAsync(host, CancellationToken.None);
		public static Task<bool> ConnectAsync(this IClient client, string host, CancellationToken token) {
			return client.ConnectAsync(NameResolver.Resolve(host), token);
		}

		public static Task<int> SendObjectAsync<T>(this IClient client, T instance) => client.SendObjectAsync(instance, CancellationToken.None);
		public static Task<int> SendObjectAsync<T>(this IClient client, T instance, CancellationToken token) {
			Memory<byte> payload = client.Config.Serializer.Serialize(instance);
			Packet packet = new(payload) {
				Options = PacketOptions.HasData | PacketOptions.RequiresConfirmation
			};

			return client.SendAsync(packet, token);
		}

		public static Task<int> SendObjectAsync<T, E>(this IClient client, T instance, E descriptor) where E : Enum => client.SendObjectAsync(instance, descriptor, CancellationToken.None);
		public static Task<int> SendObjectAsync<T, E>(this IClient client, T instance, E descriptor, CancellationToken token) where E : Enum {
			Memory<byte> payload = client.Config.Serializer.Serialize(instance);
			Packet packet = new(payload) {
				Options = PacketOptions.HasData | PacketOptions.HasDescriptor | PacketOptions.RequiresConfirmation
			};

			packet.SetDescriptor(descriptor);
			return client.SendAsync(packet, token);
		}

		public static Task<int> SendBytesAysnc(this IClient client, Memory<byte> payload) => client.SendBytesAsync(payload, CancellationToken.None);
		public static Task<int> SendBytesAsync(this IClient client, Memory<byte> payload, CancellationToken token) {
			Packet packet = new(payload) {
				Options = PacketOptions.HasData | PacketOptions.RequiresConfirmation
			};

			return client.SendAsync(packet, token);
		}

		public static Task<int> SendBytesAsync<E>(this IClient client, Memory<byte> payload, E descriptor) where E : Enum => client.SendBytesAsync(payload, descriptor, CancellationToken.None);
		public static Task<int> SendBytesAsync<E>(this IClient client, Memory<byte> payload, E descriptor, CancellationToken token) where E : Enum {
			Packet packet = new(payload) {
				Options = PacketOptions.HasData | PacketOptions.HasDescriptor | PacketOptions.RequiresConfirmation
			};

			packet.SetDescriptor(descriptor);
			return client.SendAsync(packet, token);
		}

		public static string GetSessionId(this IClient client) => GetSessionIdAsync(client).GetAwaiter().GetResult();

		public static Task<string> GetSessionIdAsync(this IClient client) {
			return client.GetSessionIdAsync(CancellationToken.None);
		}

		public static void UpdateConnection(this IClient client) => client.UpdateConnection(client.Direction);

		public static IAsyncClient CreateAsyncClient(this IClient client) {
			return ClientFactory.CreateAsyncClient(client);
		}
	}
}