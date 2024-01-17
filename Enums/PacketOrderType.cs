namespace Lunacy.Tcp.Enums {
	public enum PacketOrderType {
		/// <summary>
		/// Each packets need to be confirmed to have been received and processed before the next packet can be sent
		/// </summary>
		Sequential,
		/// <summary>
		/// The order in which the packets are received and processed does not matter
		/// </summary>
		NonSequential
	}
}