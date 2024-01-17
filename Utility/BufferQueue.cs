using System.Collections.ObjectModel;

namespace Lunacy.Tcp.Utility {
	internal sealed class BufferQueue : IDisposable {

		public event EventHandler<Memory<byte>>? Enqueued, Dequeued;

		public int MaxQueueSize { get; set; }

		private readonly SemaphoreSlim _EnqueueLock = new(1, 1), _DequeueLock = new(1, 1);
		private readonly AsyncTrigger _EnqueuedTrigger = new(), _DequeuedTrigger = new();

		private readonly Collection<Memory<byte>> _Queue = [];

		public BufferQueue(int maxQueueSize) {
			Enqueued += OnItemEnqueued;
			Dequeued += OnItemDequeued;

			MaxQueueSize = maxQueueSize;
		}

		public BufferQueue() : this(5_242_880) /* 5Mb */ { }

		private void OnItemDequeued(object? sender, Memory<byte> e) => _DequeuedTrigger.Trigger();
		private void OnItemEnqueued(object? sender, Memory<byte> e) => _EnqueuedTrigger.Trigger();

		public async Task<bool> EnqueueAsync(Memory<byte> buffer) {
			ObjectDisposedException.ThrowIf(_IsDisposing || _IsDisposed, this);

			await _EnqueueLock.WaitAsync();
			try {

				if(MaxQueueSize != -1 && _Queue.Sum(x => x.Length) + buffer.Length >= MaxQueueSize) {
					return false;
				}

				_Queue.Add(buffer);
				Enqueued?.Invoke(this, buffer);

				return true;

			} finally {
				if(!_IsDisposed && !_IsDisposing) {
					_EnqueueLock.Release();
				}
			}
		}

		public async Task<Memory<byte>?> DequeueAsync(bool waitForItem, CancellationToken token) {
			ObjectDisposedException.ThrowIf(_IsDisposing || _IsDisposed, this);

			await _DequeueLock.WaitAsync(token);
			try {

				bool enqueueLockReleased = false;
				await _EnqueueLock.WaitAsync(token);
				try {
					if(_Queue.Count > 0) {
						Memory<byte> buffer = _Queue[0];
						_Queue.RemoveAt(0);

						Dequeued?.Invoke(this, buffer);
						return buffer;
					} else if(waitForItem) {
						Task waitForEnqueueTask = _EnqueuedTrigger.WaitAsync(token);
						
						_EnqueueLock.Release();
						enqueueLockReleased = true;

						await waitForEnqueueTask;

						Memory<byte> buffer = _Queue[0];
						_Queue.RemoveAt(0);

						Dequeued?.Invoke(this, buffer);
						return buffer;
					}

					return null;

				} finally {
					if(!_IsDisposing && !_IsDisposed && !enqueueLockReleased) {
						_EnqueueLock.Release();
					}
				}

			} finally {
				if(!_IsDisposed && !_IsDisposing) {
					_DequeueLock.Release();
				}
			}
		}

		public void Dispose() {
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		private bool _IsDisposing, _IsDisposed;
		private void Dispose(bool disposing) {
			if(!( _IsDisposing || _IsDisposed ) || disposing) {
				_IsDisposing = true;
				Enqueued -= OnItemEnqueued;
				Dequeued -= OnItemDequeued;
				_EnqueueLock.Dispose();
				_EnqueuedTrigger.Dispose();
				_DequeuedTrigger.Dispose();
				_IsDisposed = true;
			}
		}

		~BufferQueue() {
			Dispose(false);
		}
	}
}