namespace Lunacy.Tcp.Serializers {
	public interface ISerializer {
		public Memory<byte> Serialize<T>(T instance);
		public T Deserialize<T>(Memory<byte> payload);
	}
}