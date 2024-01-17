
using Lunacy.Tcp.Extensions;

namespace Lunacy.Tcp.Serializers {
	public sealed class JsonSerializer : ISerializer {
		public static readonly JsonSerializer Instance = new();

		public T Deserialize<T>(Memory<byte> payload) {
			return System.Text.Json.JsonSerializer.Deserialize<T>(payload.AsString()) ?? throw new InvalidOperationException("Unable to deserialize payload from json");
		}

		public Memory<byte> Serialize<T>(T instance) {
			return System.Text.Json.JsonSerializer.Serialize(instance).AsBytes();
		}
	}
}