namespace Lunacy.Tcp.Utility {
	internal class TimeoutTokenSource() : IDisposable {

		protected TimeSpan? _LowestTimeout = null;
		protected CancellationTokenSource? _TimeoutTokenSource = null, _LinkedTokenSource = null;

		protected List<CancellationToken> _Tokens = [];
		protected readonly object _Lock = new();

		protected bool _IsBuilt = false;

		public CancellationToken Token {
			get {
				if(!_IsBuilt) {
					throw new InvalidOperationException("Timeout token source is not yet built");
				}

				if(_LinkedTokenSource != null) {
					return _LinkedTokenSource.Token;
				} else {

					return CancelledToken.Token;
				}
			}
		}

		public void AddToken(CancellationToken token) {
			lock(_Lock) {
				ObjectDisposedException.ThrowIf(_IsDisposing || _IsDisposed, this);
				ThrowIfAlreadyBuilt();

				_Tokens.Add(token);
			}
		}

		public void AddTimeout(TimeSpan timeout) {
			lock(_Lock) {
				ObjectDisposedException.ThrowIf(_IsDisposing || _IsDisposed, this);
				ThrowIfAlreadyBuilt();

				if(timeout == Timeout.InfiniteTimeSpan) {
					return;
				}

				if(_LowestTimeout.HasValue) {
					if(timeout < _LowestTimeout.Value) {
						_LowestTimeout = timeout;
					}
				} else {
					_LowestTimeout = timeout;
				}
			}
		}

		public CancellationToken Build() {
			lock(_Lock) {
				ObjectDisposedException.ThrowIf(_IsDisposing || _IsDisposed, this);
				ThrowIfAlreadyBuilt();

				CancellationToken[] tokens;
				if(_LowestTimeout.HasValue) {
					_TimeoutTokenSource = new(_LowestTimeout.Value);

					tokens = [.. _Tokens, _TimeoutTokenSource.Token];
				} else {
					tokens = [.. _Tokens];
				}

				_LinkedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(tokens);
				_IsBuilt = true;

				return _LinkedTokenSource.Token;
			}
		}

		private void ThrowIfAlreadyBuilt() {
			if(_IsBuilt) {
				throw new InvalidOperationException("Cannot add tokens after the token source has already been built.");
			}
		}

		public void Dispose() {
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		protected volatile bool _IsDisposing, _IsDisposed;
		protected virtual void Dispose(bool disposing) {
			lock(_Lock) {
				if(!_IsDisposing && !_IsDisposed) {
					_IsDisposing = true;
					_LinkedTokenSource?.Dispose();
					_TimeoutTokenSource?.Dispose();
					_IsDisposed = true;
				}
			}
		}

		~TimeoutTokenSource() {
			Dispose(false);
		}
	}
}
