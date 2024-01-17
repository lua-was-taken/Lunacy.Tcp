using Lunacy.Tcp.Enums;
using Lunacy.Tcp.Exceptions;
using Lunacy.Tcp.Extensions;
using Lunacy.Tcp.Utility;
using System.Net;
using System.Net.Sockets;

namespace Lunacy.Tcp.Connectivity {
	internal sealed class TcpSocket : IDisposable {

		public event EventHandler? Disconnected, Connected;
		public event EventHandler<Memory<byte>>? DataReceived;

		public Socket Socket { get; private init; }
		public bool IsConnected { get; private set; } = false;
		public ConnectionDirectionType Direction { get; internal set; } = ConnectionDirectionType.Unknown;

		public Exception? LastException { get; private set; } = null;

		private CancellationTokenSource? _DisconnectTokenSource = null;
		public CancellationToken DisconnectToken {
			get {
				if(_DisconnectTokenSource == null || _DisconnectTokenSource.IsCancellationRequested) {
					return CancelledToken.Token;
				}

				return _DisconnectTokenSource.Token;
			}
		}

		private readonly CancellationTokenSource _DisposeTokenSource = new();
		private CancellationToken DisposeToken {
			get {
				return _DisposeTokenSource.Token;
			}
		}

		private readonly SemaphoreSlim _ConnectLock = new(1, 1);
		private readonly AsyncTrigger _AllowConnectionTrigger = new();

		private readonly object _InitializeLock = new();
		private volatile bool _InitializingConnection = false;

		public TcpSocket(Socket baseSocket) {
			Socket = baseSocket;
			if(Socket.Connected) {
				StartInitializeConnection();
			}
		}

		public bool UpdateConnection() {
			if(IsConnected && !Socket.Connected) {
				Disconnect();
			} else if(Socket.Connected) {
				if(!_InitializingConnection) {
					StartInitializeConnection();
				}

				_AllowConnectionTrigger.Trigger();
			}

			return IsConnected = Socket.Connected;
		}

		private void StartInitializeConnection() {
			lock(_InitializeLock) {
				if(_InitializingConnection) {
					return;
				}

				_InitializingConnection = true;

				using SemaphoreSlim taskLock = new(0, 1);
				Task.Run(async () => {
					Task allowTrigger = _AllowConnectionTrigger.WaitAsync(DisposeToken);
					taskLock.Release();

					await allowTrigger;

					IsConnected = true;
					_DisconnectTokenSource = new();

					Connected?.Invoke(this, EventArgs.Empty);

					await InternalReadSocketAsync();
				});

				taskLock.Wait();
			}
		}

		public async Task<bool> ConnectAsync(IPEndPoint remoteEndPoint, CancellationToken token) {
			ObjectDisposedException.ThrowIf(_IsDisposing || _IsDisposed, this);
			token.ThrowIfCancellationRequested();

			await _ConnectLock.WaitAsync(token);
			try {
			TRY_CONNECT:
				if(Socket.Connected) {
					IPEndPoint? currentEndPoint = Socket.RemoteEndPoint as IPEndPoint;

					if(currentEndPoint != remoteEndPoint) {
						await Socket.DisconnectAsync(reuseSocket: true, token);
						goto TRY_CONNECT;
					}

					StartInitializeConnection();
					_AllowConnectionTrigger.Trigger();
					return true;
				} else {
					try {

						await Socket.ConnectAsync(remoteEndPoint, token);
						if(Socket.Connected) {
							Direction = ConnectionDirectionType.Initiator;

							StartInitializeConnection();
							_AllowConnectionTrigger.Trigger();
							return true;
						}

					} catch(Exception ex) {
						LastException = ex;
						if(!IsDisconnectedException(ex)) {
							throw;
						}
					}
				}

				return false;
			} finally {
				if(!_IsDisposing && !_IsDisposed) {
					_ConnectLock.Release();
				}
			}
		}

		public async Task<int> SendAsync(Memory<byte> buffer, CancellationToken token) {
			ObjectDisposedException.ThrowIf(_IsDisposing || _IsDisposed, this);
			token.ThrowIfCancellationRequested();

			if(!IsConnected) {
				throw LastException = new NotConnectedException();
			}

			using CancellationTokenSource tokenSource = CancellationTokenSource.CreateLinkedTokenSource(token, DisconnectToken);
			token = tokenSource.Token;

			try {

				int sendCount = 0;
				sendCount += await Socket.SendAsync(BitConverter.GetBytes(buffer.Length), token);
				sendCount += await Socket.SendAsync(buffer, token);

				return sendCount;

			} catch(Exception ex) {
				LastException = ex;
				if(IsDisconnectedException(ex)) {
					throw LastException = new NotConnectedException(string.Empty, ex);
				} else {
					throw;
				}
			}
		}

		private readonly object _lock = new();
		public void Disconnect() {
			lock(_lock) {
				try {
					if(Socket.Connected) {
						Socket.Disconnect(true);
					}
				} catch(Exception ex) { LastException = ex; }

				try {
					_DisconnectTokenSource?.Cancel();
					_DisconnectTokenSource?.Dispose();
					_DisconnectTokenSource = null;
				} catch(Exception ex) { LastException = ex; }

				IsConnected = false;
				Direction = ConnectionDirectionType.Unknown;
				_InitializingConnection = false;

				Disconnected?.Invoke(this, EventArgs.Empty);
			}
		}

		private async Task InternalReadSocketAsync() {
			if(!IsConnected) {
				throw LastException = new NotConnectedException();
			}

			try {
				while(Socket.Connected) {
					Memory<byte> sizeBuffer = await Socket.ReceiveExactlyAsync(sizeof(int));

					int packetSize = BitConverter.ToInt32(sizeBuffer.Span);
					if(packetSize <= 0) {
						if(!Socket.Connected || !Socket.Poll(1000, SelectMode.SelectWrite)
							|| !Socket.Poll(1000, SelectMode.SelectRead)
							|| !Socket.Poll(1000, SelectMode.SelectError)) {
							throw new NotConnectedException();
						}

						throw LastException = new CorruptDataException("Received invalid packet size announcement of " + packetSize);
					}

					Memory<byte> packetBuffer = await Socket.ReceiveExactlyAsync(packetSize);
					_ = Task.Run(() => DataReceived?.Invoke(this, packetBuffer));
				}
			} catch(Exception ex) {
				LastException = ex;
				if(!IsDisconnectedException(ex)) {
					throw;
				}

				throw LastException = new NotConnectedException();
			} finally {
				Disconnect();
			}
		}

		public bool IsDisconnectedException(Exception ex) {
			if(ex is TaskCanceledException && !IsConnected) {
				return true;
			}

			return ex is NotConnectedException or SocketException or EndOfStreamException or IOException or OperationCanceledException or ObjectDisposedException;
		}

		public void Dispose() {
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		private bool _IsDisposing, _IsDisposed;
		private void Dispose(bool disposing) {
			if(!( _IsDisposing || _IsDisposed ) || disposing) {
				_IsDisposing = true;
				_DisposeTokenSource.Cancel();

				Disconnect();
				Socket.Dispose();

				_DisposeTokenSource.Dispose();
				_ConnectLock.Dispose();
				_AllowConnectionTrigger.Dispose();
				_IsDisposed = true;
			}
		}

		~TcpSocket() {
			Dispose(false);
		}
	}
}