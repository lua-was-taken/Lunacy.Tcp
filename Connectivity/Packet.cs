using Lunacy.Tcp.Enums;
using Lunacy.Tcp.Extensions;

namespace Lunacy.Tcp.Connectivity {
	public class BasePacket {
		public const int BasePacketSize = 16 + sizeof(int);

		public Guid Id { get; set; } = Guid.NewGuid();
		public PacketOptions Options { get; set; } = PacketOptions.None;
		public SourceOptions Source { get; set; } = SourceOptions.Local;

		public Memory<byte> GetBytes() {
			return Id.ToByteArray().Combine(BitConverter.GetBytes((int)Options));
		}

		public BasePacket FromBytes(Memory<byte> buffer) {
			Id = new Guid(buffer[..16].Span);
			Options = (PacketOptions)BitConverter.ToInt32(buffer[16..20].Span);

			Source = SourceOptions.Remote;
			return this;
		}

		public BasePacket CopyBasePacket() {
			return new BasePacket() {
				Id = Id,
				Options = Options,
				Source = Source
			};
		}
	}

	public class Packet() : BasePacket {
		public Memory<byte> Payload { get; set; } = Memory<byte>.Empty;
		public int Descriptor { get; set; } = -1;

		public Packet(Memory<byte> payload) : this() {
			Payload = payload;
		}

		public void SetDescriptor<T>(T descriptor) where T : Enum {
			Descriptor = (int)(object)descriptor;
		}

		public T GetDescriptor<T>() where T : Enum {
			return (T)(object)Descriptor;
		}

		public new Memory<byte> GetBytes() {
			return base.GetBytes().Combine(Payload, BitConverter.GetBytes(Descriptor));
		}

		public new Packet FromBytes(Memory<byte> buffer) {
			base.FromBytes(buffer);

			Payload = buffer[BasePacketSize..( buffer.Length - sizeof(int) )];
			Descriptor = BitConverter.ToInt32(buffer[(BasePacketSize + Payload.Length)..].Span);
			
			Source = SourceOptions.Remote;
			return this;
		}
	}
}