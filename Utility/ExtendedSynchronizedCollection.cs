namespace Lunacy.Tcp.Utility {
	public sealed class ExtendedSynchronizedCollection<T> : SynchronizedCollection<T>, IDisposable {

		private readonly AsyncTrigger _CollectionItemRemovedTrigger = new();
		private readonly AsyncTrigger _CollectionEmptiedTrigger = new();
		private readonly SemaphoreSlim _AsyncLock = new(1, 1);

		public override void Clear() {
			if(_IsDisposing || _IsDisposed) {
				base.Clear();
				return;
			}

			_AsyncLock.Wait();
			try {
				base.Clear();
				_CollectionEmptiedTrigger.Trigger();
			} finally {
				if(!_IsDisposing && !_IsDisposed) {
					_AsyncLock.Release();
				}
			}
		}

		public override bool Remove(T item) {
			ObjectDisposedException.ThrowIf(_IsDisposing || _IsDisposed, this);

			_AsyncLock.Wait();
			try {
				bool success = base.Remove(item);
				if(success) {
					if(Count == 0) {
						_CollectionEmptiedTrigger.Trigger();
					}

					_CollectionItemRemovedTrigger.Trigger();
				}

				return success;
			} finally {
				if(!_IsDisposing && !_IsDisposed) {
					_AsyncLock.Release();
				}
			}
		}

		public async Task WaitForItemRemovedAsync(T item, CancellationToken token) {
		METHOD_START:
			if(_IsDisposing || _IsDisposed) return;
			token.ThrowIfCancellationRequested();

			bool releasedLock = false;
			await _AsyncLock.WaitAsync(token);
			try {
				if(!Contains(item)) {
					return;
				}

				Task triggerTask = _CollectionItemRemovedTrigger.WaitAsync(token);
				releasedLock = true;
				_AsyncLock.Release();

				bool repeat = false;
				await triggerTask;
				await _AsyncLock.WaitAsync(token);
				try {
					repeat = Contains(item);
				} finally {
					if(!_IsDisposing && !_IsDisposed) {
						_AsyncLock.Release();
					}
				}

				if(repeat) {
					goto METHOD_START;
				}

			} finally {
				if(!_IsDisposing && !_IsDisposed && !releasedLock) {
					_AsyncLock.Release();
				}
			}
		}

		public async Task WaitForEmptiedCollectionAsync() => await WaitForEmptiedCollectionAsync(CancellationToken.None);
		public async Task WaitForEmptiedCollectionAsync(CancellationToken token) {
			ObjectDisposedException.ThrowIf(_IsDisposed || _IsDisposing, this);
			token.ThrowIfCancellationRequested();

			bool releasedLock = false;
			await _AsyncLock.WaitAsync(token);
			try {
				if(Count == 0) {
					return;
				}

				Task triggerTask = _CollectionEmptiedTrigger.WaitAsync(token);
				releasedLock = true;
				_AsyncLock.Release();

				await triggerTask;

			} finally {
				if(!_IsDisposing && !_IsDisposed && !releasedLock) {
					_AsyncLock.Release();
				}
			}
		}

		public void Dispose() {
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		private readonly object _DisposeLock = new();
		private volatile bool _IsDisposing, _IsDisposed;
		private void Dispose(bool disposing) {
			lock(_DisposeLock) {
				if(!( _IsDisposing || _IsDisposed )) {
					_IsDisposing = true;
					Clear();
					_CollectionEmptiedTrigger.Dispose();
					_CollectionItemRemovedTrigger.Dispose();
					_AsyncLock.Dispose();
					_IsDisposed = true;

					GC.KeepAlive(disposing);
				}
			}
		}

		~ExtendedSynchronizedCollection() {
			Dispose(false);
		}
	}
}