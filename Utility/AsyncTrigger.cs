namespace Lunacy.Tcp.Utility {
	public sealed class AsyncTrigger : IDisposable {

		private readonly SemaphoreSlim _Lock = new(0, int.MaxValue);
		private readonly SemaphoreSlim _InteractionLock = new(1, 1);
		private volatile int _WaitCount = 0;

		public void Trigger() {
			_InteractionLock.Wait();
			try {
				if(_WaitCount > 0) {
					_Lock.Release(_WaitCount);
					_WaitCount = 0;
				}
			} finally { 
				if(!_IsDisposing && !_IsDisposed) {
					_InteractionLock.Release();
				}
			}
		}
		
		public Task WaitAsync() => WaitAsync(CancellationToken.None);
		public async Task WaitAsync(CancellationToken token) {
			await _InteractionLock.WaitAsync(token);
			try {
				_WaitCount++;
			} finally {
				if(!_IsDisposing && !_IsDisposed) {
					_InteractionLock.Release();
				}
			}

			await _Lock.WaitAsync(token);
		}

		public void Dispose() {
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		private bool _IsDisposing, _IsDisposed;
		private void Dispose(bool disposing) {
			if(!(_IsDisposing || _IsDisposed) || disposing) {
				_IsDisposing = true;
				_Lock.Dispose();
				_InteractionLock.Dispose();
				_IsDisposed = true;
			}
		}

		~AsyncTrigger() { 
			Dispose(false);	
		}
	}
}