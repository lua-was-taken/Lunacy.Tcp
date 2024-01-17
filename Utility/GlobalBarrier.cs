namespace Lunacy.Tcp.Utility {
	public sealed class GlobalBarrier : IDisposable {

		private readonly object _SyncLock = new();
		private readonly SemaphoreSlim _BlockLock = new(1, 1);
		private readonly CancellationTokenSource _DisposeTokenSource = new();
		private CancellationToken DisposeToken {
			get {
				return _DisposeTokenSource.Token;
			}
		}

		public bool Blocking {
			get;
			private set;
		}

		public GlobalBarrier(bool blocking) {
			if(blocking) {
				Block();
			}
		}
		
		public void Block() {
			lock(_SyncLock) {
				ObjectDisposedException.ThrowIf(_IsDisposing || _IsDisposed, this);

				if(Blocking) {
					return;
				}

				Task.Run(async () => {
					try {
						await _BlockLock.WaitAsync(DisposeToken);
					} catch(Exception ex) when (ex is OperationCanceledException or ObjectDisposedException) { }
				});

				Blocking = true;
			}
		}

		public void Unblock() {
			lock(_SyncLock) {
				ObjectDisposedException.ThrowIf(_IsDisposing || _IsDisposed, this);

				if(!Blocking) {
					return;
				}

				_BlockLock.Release();
				Blocking = false;
			}
		}

		public async Task WaitAsync() => await WaitAsync(CancellationToken.None);
		public async Task WaitAsync(CancellationToken token) {
			ObjectDisposedException.ThrowIf(_IsDisposing || _IsDisposed, this);
			
			using CancellationTokenSource linkedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(token, DisposeToken);
			token = linkedTokenSource.Token;

			token.ThrowIfCancellationRequested();

			await _BlockLock.WaitAsync(token);
			try {
				return;
			} catch(OperationCanceledException) {
				ObjectDisposedException.ThrowIf(_IsDisposing || _IsDisposed, this);
				throw;
			} finally {
				if(!_IsDisposing && !_IsDisposed) {
					_BlockLock.Release();
				}
			}
		}

		public void Dispose() {
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		private readonly object _lock = new();
		private volatile bool _IsDisposing, _IsDisposed;
		private void Dispose(bool disposing) {
			lock(_lock) {
				if(!( _IsDisposing || _IsDisposed ) || disposing) {
					_IsDisposing = true;
					if(!_IsDisposing && !_IsDisposed) {
						_DisposeTokenSource.Cancel(throwOnFirstException: true);
						_DisposeTokenSource.Dispose();
					}
					_BlockLock.Dispose();
					_IsDisposed = true;
				}
			}
		}

		~GlobalBarrier() {
			Dispose(false);
		}
	}
}