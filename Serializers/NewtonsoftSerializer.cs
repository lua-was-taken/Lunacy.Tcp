using Lunacy.Tcp.Extensions;
using Newtonsoft.Json;

namespace Lunacy.Tcp.Serializers {
	public sealed class NewtonsoftSerializer : ISerializer {
		public static readonly NewtonsoftSerializer Instance = new();

		public T Deserialize<T>(Memory<byte> payload) {
			return JsonConvert.DeserializeObject<T>(payload.AsString()) ?? throw new JsonSerializationException();
		}

		public Memory<byte> Serialize<T>(T instance) {
			return JsonConvert.SerializeObject(instance).AsBytes();
		}
	}
}