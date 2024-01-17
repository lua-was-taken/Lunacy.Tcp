using Lunacy.Tcp.Serializers;

namespace Lunacy.Tcp.Connectivity {
	public sealed class PacketHandle {
		internal event EventHandler<Packet>? PacketAccessed;

		public int Size { get; private init; }
		public Guid Id { get; private init; }
		public DateTime Timestamp { get; private init; }

		private readonly ClientConfig _Config;
		private readonly Packet _Packet;
		private volatile bool _Accessed = false;
		private readonly object _Lock = new();

		internal PacketHandle(ClientConfig config, Packet packet) {
			_Config = config;
			_Packet = packet;

			Id = packet.Id;
			Size = packet.Payload.Length;
			Timestamp = DateTime.Now;
		}

		public Packet GetPacket() {
			lock(_Lock) {
				if(!_Accessed) {
					PacketAccessed?.Invoke(this, _Packet);
					_Accessed = true;
				}

				return _Packet;
			}
		}

		public T GetData<T>(ISerializer? serializer = null) {
			Packet packet = GetPacket();
			return (serializer ?? _Config.Serializer).Deserialize<T>(packet.Payload);
		}

		public Memory<byte> GetPayload() {
			return GetPacket().Payload;
		}
	}
}