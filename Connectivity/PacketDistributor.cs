using Lunacy.Tcp.Utility;
using System.Diagnostics;

namespace Lunacy.Tcp.Connectivity {
	public sealed class PacketDistributor(ClientConfig config) : IDisposable {
		public event EventHandler<PacketHandle>? HandleAdded, HandleRemoved;

		public ClientConfig Config { get; init; } = config;
		public IReadOnlyList<PacketHandle> PacketHandles {
			get {
				return _Handles.ToArray();
			}
		}
		
		private readonly SynchronizedCollection<PacketHandle> _Handles = [];
		private readonly SemaphoreSlim _Lock = new(1, 1), _DequeueLock = new(1, 1);
		private readonly AsyncTrigger _HandleAddedTrigger = new();

		public PacketHandle AddNext(Packet packet) {
			ObjectDisposedException.ThrowIf(_IsDisposing || _IsDisposed, this);

			_Lock.Wait();
			try {
				PacketHandle handle = new(Config, packet);
				handle.PacketAccessed += OnPacketAccessed;

				int cleanUpCount = CleanUpHandles();
				_Handles.Add(handle);

				HandleAdded?.Invoke(this, handle);
				_HandleAddedTrigger.Trigger();

				return handle;
			} finally {
				if(!_IsDisposing && !_IsDisposed) {
					_Lock.Release();
				}
			}
		}

		private int CleanUpHandles() {
			int removeCount = 0;
			if(Config.AutoDeleteOldPackets && Config.DeletePacketsOlderThan != Timeout.InfiniteTimeSpan) {
				foreach(PacketHandle packetHandle in _Handles.ToList()) {

					// Is packet outdated?
					if(DateTime.Now - packetHandle.Timestamp > Config.DeletePacketsOlderThan) {
						_Handles.Remove(packetHandle);
						removeCount++;
					} else {
						break;
					}
				}
			}

			if(Config.MaxInternalBufferSize > 0) {
				int internalBufferSize;

				while(( internalBufferSize = _Handles.Sum(x => x.Size) ) > Config.MaxInternalBufferSize) {
					PacketHandle outdatedHandle = _Handles.First();
					_Handles.Remove(outdatedHandle);
					removeCount++;
				}
			}

#if DEBUG
			if(removeCount > 0) {
				if(Config.DebugLog) {
					Debug.WriteLine($"Cleaned up {removeCount} packets");
				}
			}
#endif

			return removeCount;
		}

		private void OnPacketAccessed(object? sender, Packet e) {
			PacketHandle handle = (PacketHandle)sender!;
			handle.PacketAccessed -= OnPacketAccessed;

			_Handles.Remove(handle);
		}

		public async Task<PacketHandle?> GetNextAsync(bool waitForPacket, CancellationToken token) {
			ObjectDisposedException.ThrowIf(_IsDisposing || _IsDisposed, this);

			bool lockReleased = false;
			await _Lock.WaitAsync(token);
			try {

				await _DequeueLock.WaitAsync(token);
				try {
					if(_Handles.Count > 0) {
						PacketHandle handle = _Handles.First();
						
						CleanUpHandles();
						return handle;
					} else {
						if(waitForPacket) {
							Task triggerTask = _HandleAddedTrigger.WaitAsync(token);

							lockReleased = true;
							_Lock.Release();

							await triggerTask;
							return _Handles.First();
						} else {
							return null;
						}
					}
				} finally {
					if(!_IsDisposing && !_IsDisposed) {
						_DequeueLock.Release();
					}
				}
			} catch(Exception ex) when(ex is OperationCanceledException or ObjectDisposedException) { 
				return null;
			} finally {
				if(!_IsDisposing && !_IsDisposed && !lockReleased) {
					_Lock.Release();
				}
			}
		}

		public void Dispose() {
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		private volatile bool _IsDisposing, _IsDisposed;
		private void Dispose(bool disposing) {
			if(!(_IsDisposing || _IsDisposed) || disposing) {
				_IsDisposing = true;
				_Lock.Dispose();
				_HandleAddedTrigger.Dispose();
				_IsDisposed = true;
			}
		}

		~PacketDistributor() {
			Dispose(false);
		}
	}
}