using Lunacy.Tcp.Enums;
using Lunacy.Tcp.Exceptions;
using Lunacy.Tcp.Extensions;
using System.Net;
using System.Net.Sockets;

namespace Lunacy.Tcp.Connectivity {
    public abstract class BaseRemoteClient : IDisposable {

		internal event EventHandler? Connected, Disconnected;
		internal event EventHandler<BasePacket>? PacketPublished;

        public ConnectionDirectionType Direction { get; internal set; } = ConnectionDirectionType.Unknown;

        public bool IsConnected {
			get {
				return _BaseSocket.IsConnected;
			}
		}

		public string GetRemoteHost() => RemoteEndPoint?.ToEndPointString() ?? NameResolver.DefaultHost;
		public IPEndPoint? RemoteEndPoint { get; protected set; } = null;

		public string GetLocalHost() => LocalEndPoint.ToEndPointString();
		public IPEndPoint LocalEndPoint {
			get {
				return _BaseSocket.Socket.LocalEndPoint as IPEndPoint ?? EndPointFactory.CreateLocalEndPoint(0);
			}
		}

		public CancellationToken DisconnectToken {
			get {
				return _BaseSocket.DisconnectToken;
			}
		}

		public ClientConfig Config { get; protected init; }

		internal readonly TcpSocket _BaseSocket;
		protected readonly SemaphoreSlim _MethodLock;

		internal BaseRemoteClient(ClientConfig config, Socket socket, int port) {
			Config = config;
			if(!socket.IsBound && port > 0) {
				socket.Bind(EndPointFactory.CreateLocalEndPoint(port));
			}

			_BaseSocket = new(socket);
			_MethodLock = new(1, 1);

			_BaseSocket.Connected += OnClientConnected;
			_BaseSocket.Disconnected += OnClientDisconnected;
			_BaseSocket.DataReceived += OnClientDataReceived;

			PacketPublished += OnClientPacketPublished;
		}

		public BaseRemoteClient(ClientConfig config, Socket socket) : this(config, socket, 0) { }

		public void UpdateConnection() => UpdateConnection(Direction);
		public void UpdateConnection(ConnectionDirectionType directionType) {
			OnPreConnect(RemoteEndPoint ?? EndPointFactory.CreateLocalEndPoint(0));
			_BaseSocket.UpdateConnection();
			if(_BaseSocket.IsConnected) {
				RemoteEndPoint = _BaseSocket.Socket.RemoteEndPoint as IPEndPoint;
				Direction = directionType;
			} else {
				Direction = ConnectionDirectionType.Unknown;
			}

			OnPostConnect(RemoteEndPoint ?? EndPointFactory.CreateLocalEndPoint(0), IsConnected);
		}

		protected abstract void OnClientDisconnected(object? sender, EventArgs e);
		protected abstract void OnClientConnected(object? sender, EventArgs e);

		protected virtual void OnClientDataReceived(object? sender, Memory<byte> e) {
			OnPreDeserializeData(e);

			BasePacket basePacket = new();
			basePacket.FromBytes(e);

			if(basePacket.Options.HasFlag(PacketOptions.HasData)) {
				Packet packet = new();
				packet.FromBytes(e);

				basePacket = packet;
			}

			Task.Run(() => {
				OnPrePublishPacket(basePacket);
				PacketPublished?.Invoke(this, basePacket);
				OnPostPublishPacket(basePacket);
			});
		}

		protected abstract void OnClientPacketPublished(object? sender, BasePacket e);

		protected virtual void OnPreDeserializeData(Memory<byte> buffer) { }
		protected virtual void OnPrePublishPacket(BasePacket packet) { }
		protected virtual void OnPostPublishPacket(BasePacket packet) { }

		public virtual async Task<bool> ConnectAsync(IPEndPoint remoteEndPoint, CancellationToken token) {
			ObjectDisposedException.ThrowIf(_IsDisposed || _IsDisposing, this);
			token.ThrowIfCancellationRequested();

			await _MethodLock.WaitAsync(token);
			try {

				if(IsConnected) {
					throw new InvalidOperationException("Client is already connected");
				}

				OnPreConnect(remoteEndPoint);
				bool success = await _BaseSocket.ConnectAsync(remoteEndPoint, token);
				if(success) {
					RemoteEndPoint = _BaseSocket.Socket.RemoteEndPoint as IPEndPoint;
					Direction = ConnectionDirectionType.Initiator;
				}
				OnPostConnect(remoteEndPoint, success);

				return success;

			} finally {
				if(!_IsDisposing && !_IsDisposed) {
					_MethodLock.Release();
				}
			}
		}

		protected virtual void OnPreConnect(IPEndPoint remoteEndPoint) { }
		protected virtual void OnPostConnect(IPEndPoint remoteEndPoint, bool success) { }

		protected virtual async Task<int> InternalBaseSendAsync(Memory<byte> buffer, CancellationToken token) {
			ObjectDisposedException.ThrowIf(_IsDisposed || _IsDisposing, this);

			if(!IsConnected) {
				throw new NotConnectedException();
			}

			using CancellationTokenSource tokenSource = CancellationTokenSource.CreateLinkedTokenSource(token, _BaseSocket.DisconnectToken);
			token = tokenSource.Token;

			token.ThrowIfCancellationRequested();

			await _MethodLock.WaitAsync(token);
			try {
				return await _BaseSocket.SendAsync(buffer, token);
			} finally {
				if(!_IsDisposing && !_IsDisposed) {
					_MethodLock.Release();
				}
			}
		}

		protected virtual Task<int> SendAsync(BasePacket basePacket, CancellationToken token) {
			return InternalBaseSendAsync(basePacket.GetBytes(), token);
		}

		public virtual Task<int> SendAsync(Packet packet, CancellationToken token) {
			return InternalBaseSendAsync(packet.GetBytes(), token);
		}

		public virtual void DisconnectFast() => _BaseSocket.Disconnect();

		public void Dispose() {
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		private readonly object _lock = new();
		protected bool _IsDisposing, _IsDisposed = false;
		protected virtual void Dispose(bool disposing) {
			lock(_lock) {
				if(!( _IsDisposing || _IsDisposed )) { // Only call Dispose() once regardless of disposing flag
					_IsDisposing = true;
					_BaseSocket.Disconnect();
					_BaseSocket.Dispose();
					_MethodLock.Dispose();
					_IsDisposed = true;
				}
			}
		}

		~BaseRemoteClient() {
			Dispose(false);
		}
	}
}