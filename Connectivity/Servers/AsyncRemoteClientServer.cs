using Lunacy.Tcp.Connectivity.Clients;
using Lunacy.Tcp.Exceptions;
using Lunacy.Tcp.Utility;
using System.Net.Sockets;

namespace Lunacy.Tcp.Connectivity.Servers {
    public class AsyncRemoteClientServer(ClientConfig defaultClientConfig, Func<IServer, Socket, IAsyncClient> clientFactoryMethod) : AsyncRemoteClientServer<IAsyncClient>(defaultClientConfig, clientFactoryMethod);
    public class AsyncRemoteClientServer<T>(ClientConfig config, Func<IServer, Socket, T> clientFactoryMethod) : RemoteClientServer<T>(config, clientFactoryMethod), IAsyncServer<T> where T : IAsyncClient {
        public new event EventHandler<PacketHandle>? PacketPublished;

        protected readonly SynchronizedCollection<WeakReference> _AcceptedClients = [];
        protected readonly AsyncTrigger _ClientConnectedTrigger = new();
        protected readonly SemaphoreSlim _ClientGetLock = new(1, 1);
        protected readonly object _ClientLock = new();

        protected override void OnClientConnected(object? sender, EventArgs e) {
            IAsyncClient client = (IAsyncClient)sender!;
            lock(_ClientLock) {
                if(client.IsConnected) {
                    WeakReference clientReference = new(client, trackResurrection: false);
                    _AcceptedClients.Add(clientReference);
                    _ClientConnectedTrigger.Trigger();

				}
            }

            base.OnClientConnected(sender, e);
        }

        protected override void OnClientDisconnected(object? sender, EventArgs e) {
            IAsyncClient client = (IAsyncClient)sender!;
            lock(_ClientLock) {
                WeakReference? clientReference = _AcceptedClients.FirstOrDefault(x => x.IsAlive && (x.Target?.Equals(client) ?? false));
                if(clientReference != null) {
                    _AcceptedClients.Remove(clientReference);
                }

                ReleaseDeadClientReferences();
            }

            base.OnClientDisconnected(sender, e);
        }

        protected void ReleaseDeadClientReferences() {
            if(_AcceptedClients.Count == 0) return;

            foreach(WeakReference reference in _AcceptedClients.ToList()) {
                if(!reference.IsAlive) {
                    _AcceptedClients.Remove(reference);
                }
            }
        }

        public virtual async Task<T> GetClientAsync(CancellationToken token) {
            return (T)await ((IAsyncServer)this).GetClientAsync(token);
        }

        async Task<IAsyncClient> IAsyncServer.GetClientAsync(CancellationToken token) {
            ObjectDisposedException.ThrowIf(_IsDisposing || _IsDisposed, this);
            if(!IsOpen) {
                throw new NotListeningException();
            }

            using CancellationTokenSource linkedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(token, ListeningToken);
            token = linkedTokenSource.Token;

            token.ThrowIfCancellationRequested();

            Task? waitingTask = null;
            lock(_ClientLock) {
                ReleaseDeadClientReferences();

                if(_AcceptedClients.Count > 0) {
                    if(_AcceptedClients.FirstOrDefault(x => x.IsAlive && x.Target != null)?.Target is IAsyncClient client) {
                        return client;
                    }
                }

                token.ThrowIfCancellationRequested();
                waitingTask = _ClientConnectedTrigger.WaitAsync(token);
            }

            await _ClientGetLock.WaitAsync(token);
            try {
            TRY_GET_CLIENT:
                await waitingTask;
                if(_AcceptedClients.FirstOrDefault(x => x.IsAlive && x.Target != null)?.Target is IAsyncClient client) {
                    return client;
                }

                token.ThrowIfCancellationRequested();
                waitingTask = _ClientConnectedTrigger.WaitAsync(token);

                goto TRY_GET_CLIENT;
            } finally {
                if(!_IsDisposing || _IsDisposed) {
                    _ClientGetLock.Release();
                }
            }
        }

        protected override void AttachClientEvent(IClient client) {
            client.Connected += OnClientConnected;
            client.Disconnected += OnClientDisconnected;

            IAsyncClient asyncClient = (IAsyncClient)client;
            asyncClient.PacketPublished += OnClientPacketPublished;
        }

        protected override void DetachClientEvents(IClient client) {
            client.Connected -= OnClientConnected;
            client.Disconnected -= OnClientDisconnected;

            IAsyncClient asyncClient = (IAsyncClient)client;
            asyncClient.PacketPublished -= OnClientPacketPublished;
        }

        protected override void OnClientPacketPublished(object? sender, Packet e) { /* Unused */ }

        protected virtual void OnClientPacketPublished(object? sender, PacketHandle e) {
            PacketPublished?.Invoke(sender, e);
        }

        protected override void Dispose(bool disposing) {
            if(!(_IsDisposing || _IsDisposed) || disposing) {
                _IsDisposing = true;
                _ClientConnectedTrigger.Dispose();
                _AcceptedClients.Clear();
                _IsDisposed = true;
            }

            base.Dispose(disposing);
        }
    }
}