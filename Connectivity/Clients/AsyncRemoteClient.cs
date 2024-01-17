using Lunacy.Tcp.Enums;
using System.Net;

namespace Lunacy.Tcp.Connectivity.Clients {
    public sealed class AsyncRemoteClient : IAsyncClient {

        public event EventHandler<PacketHandle>? PacketPublished;
        public event EventHandler? Connected, Disconnected;

        public IClient BaseClient { get; private init; }
        private readonly PacketDistributor _Distributor;

        public bool GracefulDisconnect {
            get {
                return BaseClient.GracefulDisconnect;
            }
        }
        public ClientConfig Config => BaseClient.Config;
        public CancellationToken DisconnectToken => BaseClient.DisconnectToken;

        public ConnectionDirectionType Direction {
            get {
                return BaseClient.Direction;
            }
        }
        public bool IsConnected => BaseClient.IsConnected;

        public IPEndPoint LocalEndPoint => BaseClient.LocalEndPoint;
        public IPEndPoint? RemoteEndPoint => BaseClient.RemoteEndPoint;

        public string SessionId {
            get {
                return BaseClient.SessionId;
            }
        }

        event EventHandler<Packet>? IClient.PacketPublished {
            add {
                BaseClient.PacketPublished += value;
            }

            remove {
                BaseClient.PacketPublished -= value;
            }
        }

        public AsyncRemoteClient(IClient baseClient) {
            BaseClient = baseClient;
            _Distributor = new(baseClient.Config);

            baseClient.PacketPublished += OnPacketPublished;
			baseClient.Connected += OnClientConnected;
			baseClient.Disconnected += OnClientDisconnected;
        }

		private void OnClientDisconnected(object? sender, EventArgs e) {
            Disconnected?.Invoke(this, e);
		}

		private void OnClientConnected(object? sender, EventArgs e) {
            Connected?.Invoke(this, e);
		}

		private void OnPacketPublished(object? sender, Packet e) {
            PacketHandle handle = _Distributor.AddNext(e);
            PacketPublished?.Invoke(this, handle);
        }

        public Task<PacketHandle?> GetPacketAsync(CancellationToken token, bool waitForPacket = true) {
            ObjectDisposedException.ThrowIf(_IsDisposing || _IsDisposed, this);

            using CancellationTokenSource linkedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(token, DisconnectToken);
            token = linkedTokenSource.Token;

            token.ThrowIfCancellationRequested();

            return _Distributor.GetNextAsync(waitForPacket, token);
        }

        public Task<bool> ConnectAsync(IPEndPoint remoteEndPoint, CancellationToken token) => BaseClient.ConnectAsync(remoteEndPoint, token);
        public Task<bool> DisconnectAsync() => BaseClient.DisconnectAsync();

        public void DisconnectFast() => BaseClient.DisconnectFast();

        public string GetLocalHost() {
            return BaseClient.GetLocalHost();
        }

        public string GetRemoteHost() => BaseClient.GetRemoteHost();

        public Task<string> GetSessionIdAsync(CancellationToken token) => BaseClient.GetSessionIdAsync(token);

        public Task<int> SendAsync(Packet packet, CancellationToken token) => BaseClient.SendAsync(packet, token);
        public Task<int> SendAsync(Packet packet, PacketOrderType orderType, CancellationToken token) => BaseClient.SendAsync(packet, orderType, token);
        
        public void UpdateConnection(ConnectionDirectionType directionType) => BaseClient.UpdateConnection(directionType);

        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private volatile bool _IsDisposing, _IsDisposed;
        private void Dispose(bool disposing) {
            if(!(_IsDisposing || _IsDisposed) || disposing) {
                _IsDisposing = true;
                _Distributor.Dispose();
                BaseClient.Dispose();
                _IsDisposed = true;
            }
        }

        ~AsyncRemoteClient() {
            Dispose(false);
        }
    }
}