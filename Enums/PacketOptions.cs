namespace Lunacy.Tcp.Enums {
	[Flags]
	public enum PacketOptions {
		None = 0,
		HasData = 1 << 0,
		HasDescriptor = 1 << 1,
		Internal = 1 << 2,
		RequiresConfirmation = 1 << 3,
		Confirmation = 1 << 4,
		SessionIdPart = 1 << 5,
	}
}