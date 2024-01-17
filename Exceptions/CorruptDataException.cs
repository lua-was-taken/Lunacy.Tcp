namespace Lunacy.Tcp.Exceptions {
	public class CorruptDataException(Memory<byte> data, string message = "") : Exception(message) {
		public new Memory<byte> Data { get; protected init; } = data;
		public string OptMessage { get; protected init; } = message;

		public CorruptDataException(string message = "") : this(Memory<byte>.Empty, message) { }
	}
}