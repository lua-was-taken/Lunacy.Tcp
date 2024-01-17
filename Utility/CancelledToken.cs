namespace Lunacy.Tcp.Utility {
	public static class CancelledToken {
		private static readonly CancellationTokenSource TokenSource;
		public static readonly CancellationToken Token;
		
		static CancelledToken() {
			TokenSource = new();
			Token = TokenSource.Token;

			TokenSource.Cancel();
		}
	}
}