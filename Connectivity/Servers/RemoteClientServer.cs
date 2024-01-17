using Lunacy.Tcp.Connectivity.Clients;
using Lunacy.Tcp.Enums;
using Lunacy.Tcp.Utility;
using System.Net;
using System.Net.Sockets;

namespace Lunacy.Tcp.Connectivity.Servers {
	public class RemoteClientServer(ClientConfig defaultClientConfig, Func<IServer, Socket, IClient> clientFactoryMethod) : RemoteClientServer<IClient>(defaultClientConfig, clientFactoryMethod);
	public class RemoteClientServer<T>(ClientConfig defaultClientConfig, Func<IServer, Socket, T> clientFactoryMethod) : IServer where T : IClient {
		public event EventHandler<IClient>? ClientConnected, ClientDisconnected, ClientCreated, ClientDestroyed;
		public event EventHandler<Packet>? PacketPublished;

		public SynchronizedCollection<IClient> ConnectedClients { get; } = [];
		public ClientConfig DefaultClientConfig { get; set; } = defaultClientConfig;

		public Func<IServer, Socket, T> ClientFactoryMethod { get; protected init; } = clientFactoryMethod;

		public Socket Socket { get; set; } = new(SocketType.Stream, ProtocolType.IPv4 | ProtocolType.Tcp);
		public bool IsOpen { get; private set; } = false;

		protected CancellationTokenSource? _ListeningTokenSource = null;
		protected CancellationToken ListeningToken {
			get {
				if(_ListeningTokenSource == null) {
					return CancelledToken.Token;
				}

				return _ListeningTokenSource.Token;
			}
		}

		public RemoteClientServer(Func<IServer, Socket, T> clientFactoryMethod) : this(ClientConfig.Default, clientFactoryMethod) { }

		public virtual void Open(int port) {
			ObjectDisposedException.ThrowIf(_IsDisposing || _IsDisposed, this);

			if(IsOpen) {
				throw new InvalidOperationException("Server is already opened");
			}

			IPEndPoint localEndPoint = new(IPAddress.Loopback, port);
			Socket.Bind(localEndPoint);

			_ListeningTokenSource = new();
			Task.Run(SocketAcceptClientsAsync);

			IsOpen = true;
		}

		public virtual bool Close() {
			if(IsOpen) {
				_ListeningTokenSource?.Cancel();
				_ListeningTokenSource?.Dispose();

				_ListeningTokenSource = null;
			}

			return false;
		}

		public virtual async Task DisconnectAllAsync() {
			foreach(IClient client in ConnectedClients) {
				await client.DisconnectAsync();
			}
		}

		protected virtual async Task SocketAcceptClientsAsync() {
			ObjectDisposedException.ThrowIf(_IsDisposing || _IsDisposed, this);
			ListeningToken.ThrowIfCancellationRequested();

			try {

				Socket.Listen();
				while(true) {
					Socket clientSocket = await Socket.AcceptAsync(ListeningToken);
					T client = ClientFactoryMethod(this, clientSocket);
					OnPostCreateClient(client);

					ClientCreated?.Invoke(this, client);
					AttachClientEvent(client);

					ConnectedClients.Add(client);

					client.UpdateConnection(ConnectionDirectionType.Receiver);
				}

			} catch(Exception ex) {
				if(ex is not OperationCanceledException and not ObjectDisposedException
					and not SocketException and not IOException and not EndOfStreamException) {
					throw;
				}
			} finally {
				Socket.Close(1000);
				IsOpen = false;
			}
		}

		protected virtual void OnPostCreateClient(T client) { }

		protected virtual void AttachClientEvent(IClient client) {
			client.Connected += OnClientConnected;
			client.Disconnected += OnClientDisconnected;
			client.PacketPublished += OnClientPacketPublished;
		}

		protected virtual void DetachClientEvents(IClient client) {
			client.Disconnected -= OnClientDisconnected;
			client.Connected -= OnClientConnected;
			client.PacketPublished -= OnClientPacketPublished;
		}

		protected virtual void OnClientPacketPublished(object? sender, Packet e) {
			PacketPublished?.Invoke(sender, e);
		}

		protected virtual void OnClientConnected(object? sender, EventArgs e) {
			ClientConnected?.Invoke(sender, (IClient)sender!);
		}

		protected virtual void OnClientDisconnected(object? sender, EventArgs e) {
			IClient client = (IClient)sender!;
			ClientDisconnected?.Invoke(sender, client);

			DetachClientEvents(client);

			client.DisconnectFast();
			client.Dispose();

			ClientDestroyed?.Invoke(sender, client);
		}

		public void Dispose() {
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		protected volatile bool _IsDisposing, _IsDisposed;
		protected virtual void Dispose(bool disposing) {
			if(!( !_IsDisposing || _IsDisposed ) || disposing) {
				_IsDisposing = true;

				foreach(IClient client in ConnectedClients) {
					try {
						client.DisconnectFast();
					} catch(Exception) { }
				}

				Close();
				_IsDisposed = true;
			}
		}

		~RemoteClientServer() {
			Dispose(false);
		}
	}
}