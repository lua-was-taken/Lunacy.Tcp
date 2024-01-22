using Lunacy.Tcp.Enums;
using Lunacy.Tcp.Serializers;

namespace Lunacy.Tcp.Connectivity {
	public sealed class ClientConfig {
		public static readonly ClientConfig Default = new();

		public ISerializer Serializer = JsonSerializer.Instance;

		/// <summary>
		/// Set to -1 for indefinite buffer size
		/// </summary>
		public int MaxInternalBufferSize = 5_242_880; // 5Mb

		public TimeSpan GracefulDisconnectTimeout = TimeSpan.FromSeconds(10);
		public TimeSpan DeletePacketsOlderThan = TimeSpan.FromSeconds(30);
		public TimeSpan ConfirmationTimeout = TimeSpan.FromSeconds(10);
		public TimeSpan EndToEndTimeout = TimeSpan.FromSeconds(10);

		public PacketOrderType PacketOrderType = PacketOrderType.Sequential;

		public bool AutoDeleteOldPackets = false;
		public bool DebugLog = false;
		public bool DebugPackets = false;
	}
}