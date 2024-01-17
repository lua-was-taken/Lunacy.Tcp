using Lunacy.Tcp.Enums;
using Lunacy.Tcp.Exceptions;
using Lunacy.Tcp.Extensions;
using Lunacy.Tcp.Utility;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

namespace Lunacy.Tcp.Connectivity.Clients {
	public class RemoteClient(ClientConfig config, Socket socket) : BaseRemoteClient(config, socket), IClient {
		public new event EventHandler? Connected, Disconnected;
		public new event EventHandler<Packet>? PacketPublished;

		public bool GracefulDisconnect { get; set; } = false;
		public string SessionId { get; protected set; } = string.Empty;

		protected volatile bool _IsDisconnecting = false;
		protected internal readonly GlobalBarrier _RemoteEndProcessedBarrier = new(blocking: true);
		protected internal readonly GlobalBarrier _SessionIdResolvedBarrier = new(blocking: true);
		protected internal readonly AsyncTrigger _DisconnectedTrigger = new();

		protected readonly SemaphoreSlim _SendLock = new(1, 1);
		protected readonly SemaphoreSlim _DisconnectLock = new(1, 1);

		protected readonly ExtendedSynchronizedCollection<BasePacket> _UnconfirmedPackets = [];

		protected byte[] _LocalSessionIdPart = [];
		protected byte[] _RemoteSessionIdPart = [];

		protected override void OnClientConnected(object? sender, EventArgs e) {
			InvokeConnectedEvent(this, e);
		}

		protected virtual void InvokeConnectedEvent(object? sender, EventArgs e) {
			Connected?.Invoke(this, e);
		}

		protected override void OnPreConnect(IPEndPoint remoteEndPoint) {
			_RemoteEndProcessedBarrier.Block();
			_LocalSessionIdPart = new byte[16];

			Random.Shared.NextBytes(_LocalSessionIdPart);
			GracefulDisconnect = false;
		}

		// Calls before OnClientConnected on a connection attempt
		protected override void OnPostConnect(IPEndPoint remoteEndPoint, bool success) {
			if(success) {
				Packet localPartPacket = new(_LocalSessionIdPart) {
					Options = PacketOptions.HasData | PacketOptions.Internal | PacketOptions.SessionIdPart
				};

				base.SendAsync(localPartPacket, CancellationToken.None);
			}
		}

		protected override void OnClientDisconnected(object? sender, EventArgs e) {
			try {
				if(!_IsDisposing && !_IsDisposed) {
					_LocalSessionIdPart = [];
					_RemoteSessionIdPart = [];

					_RemoteEndProcessedBarrier.Block();
					_SessionIdResolvedBarrier.Block();
				}
			} catch(ObjectDisposedException) { }

			InvokeDisconnectedEvent(this, e);
		}

		protected virtual void InvokeDisconnectedEvent(object? sender, EventArgs e) {
			Disconnected?.Invoke(this, e);
		}

		protected sealed override void OnClientPacketPublished(object? sender, BasePacket e) {
			// Packet requires confirmation
			if(e.Options.HasFlag(PacketOptions.RequiresConfirmation)) {
				BasePacket confirmationPacket = new() {
					Id = e.Id,
					Options = PacketOptions.Confirmation
				};

				// Confirm received packet, regardless of _SendLock
				Task.Run(async () => await base.SendAsync(confirmationPacket, CancellationToken.None));
			}

			// Packet is confirmation for sent packet
			if(e.Options.HasFlag(PacketOptions.Confirmation)) {
				BasePacket? localPacket = _UnconfirmedPackets.FirstOrDefault(x => x.Id == e.Id);
				if(localPacket != null) {
					_UnconfirmedPackets.Remove(localPacket);

					Debug.WriteLineIf(Config.DebugLog && Config.DebugPackets, $"Packet id {e.Id} confirmed");
				}

				return;
			}

			// Packet contains data
			if(e.Options.HasFlag(PacketOptions.HasData)) {
				Packet packet = (Packet)e;

				// Is internal packet
				if(e.Options.HasFlag(PacketOptions.Internal)) {

					if(e.Options.HasFlag(PacketOptions.SessionIdPart)) {
						if(packet.Payload.Length != 16) {
							throw new NotSupportedException("Received unsupported payload length for session id");
						}

						byte[] bSessionId = new byte[16];
						_RemoteSessionIdPart = packet.Payload.ToArray();

						for(int i = 0; i < 16; i++) {
							bSessionId[i] = (byte)( _LocalSessionIdPart[i] ^ _RemoteSessionIdPart[i] );
						}

						SessionId = bSessionId.ToHex();

						_SessionIdResolvedBarrier.Unblock();
					} else {
						string internalSignature = packet.Payload.AsString();
						switch(internalSignature) {
							case Signatures.DisconnectSignature:
								Task.Run(HandleRemoteDisconnectAsync);
								break;

							case Signatures.PacketsProcessedConfirmationSignature:
								_RemoteEndProcessedBarrier.Unblock();
								break;
							default: InvokePacketPublishedEvent(this, packet); break;
						}
					}

				} else {
					// Publish packet to user
					InvokePacketPublishedEvent(this, packet);
				}
			}
		}

		protected virtual void InvokePacketPublishedEvent(object? sender, Packet e) {
			PacketPublished?.Invoke(this, e);
		}

		protected virtual async Task HandleRemoteDisconnectAsync() {
			if(_IsDisconnecting) { // Disconnection procedure already initiated locally
				return;
			}

			await _DisconnectLock.WaitAsync();
			try {
				if(_IsDisconnecting) {
					return;
				} else {
					_IsDisconnecting = true;
				}
			} finally {
				if(!_IsDisposing && !_IsDisposed) {
					_DisconnectLock.Release();
				}
			}

			try {
				using CancellationTokenSource gracefulTimeoutTokenSource = new(Config.GracefulDisconnectTimeout);
				using var linkedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(gracefulTimeoutTokenSource.Token, DisconnectToken);

				CancellationToken gracefulToken = linkedTokenSource.Token;

				// Obtain send lock (prevents the user from continuing to send packets)
				await _SendLock.WaitAsync(gracefulToken);

				// Create ready to disconnect signal packet
				Packet signalPacket = new(Signatures.PacketsProcessedConfirmationSignature.AsBytes()) {
					Options = PacketOptions.Internal | PacketOptions.HasData
				};

				// Wait for all packets to get confirmed
				if(_UnconfirmedPackets.Count > 0) {
					await _UnconfirmedPackets.WaitForEmptiedCollectionAsync(gracefulToken);
				}

				// Send ready to disconnect signal packet
				await base.SendAsync(signalPacket, gracefulToken);

				// Wait for remote end processed confirmation
				await _RemoteEndProcessedBarrier.WaitAsync(gracefulToken);

				GracefulDisconnect = true;
			} catch(Exception ex) when(_BaseSocket.IsDisconnectedException(ex)) {
				GracefulDisconnect = false;
			} finally {
				_BaseSocket.Disconnect();
			}
		}

		public async Task<string> GetSessionIdAsync(CancellationToken token) {
			if(!string.IsNullOrWhiteSpace(SessionId)) {
				return SessionId;
			}
			
			ObjectDisposedException.ThrowIf(_IsDisposing || _IsDisposed, this);
			if(!IsConnected) {
				throw new NotConnectedException();
			}

			if(!string.IsNullOrWhiteSpace(SessionId)) {
				return SessionId;
			}

			using CancellationTokenSource linkedTokenSoruce = CancellationTokenSource.CreateLinkedTokenSource(token, DisconnectToken);
			token = linkedTokenSoruce.Token;

			token.ThrowIfCancellationRequested();

			await _SessionIdResolvedBarrier.WaitAsync(token);

			return SessionId;
		}

		public async Task<bool> DisconnectAsync() {
			if(!IsConnected) {
				return GracefulDisconnect;
			}

			try {
				using CancellationTokenSource gracefulTimeoutTokenSource = new(Config.GracefulDisconnectTimeout);
				using var linkedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(gracefulTimeoutTokenSource.Token, DisconnectToken);

				CancellationToken gracefulToken = linkedTokenSource.Token;

				await _DisconnectLock.WaitAsync(gracefulToken);
				try {
					if(IsConnected && _IsDisconnecting) { // Disconnection procedure already initiated by remote end
						await _DisconnectedTrigger.WaitAsync(gracefulToken);
					} else if(IsConnected) {
						_IsDisconnecting = true;

						// Obtain send lock (prevents the user from continuing to send packets)
						await _SendLock.WaitAsync(gracefulToken);

						// Create disconnect announcement packet
						Packet disconnectPacket = new(Signatures.DisconnectSignature.AsBytes()) {
							Options = PacketOptions.Internal | PacketOptions.RequiresConfirmation | PacketOptions.HasData
						};

						// Send disconnect packet
						await base.SendAsync(disconnectPacket, gracefulToken);

						// Create ready to disconnect signal packet
						Packet signalPacket = new(Signatures.PacketsProcessedConfirmationSignature.AsBytes()) {
							Options = PacketOptions.Internal | PacketOptions.HasData
						};

						// Wait for all packets to get confirmed
						if(_UnconfirmedPackets.Count > 0) {
							await _UnconfirmedPackets.WaitForEmptiedCollectionAsync(gracefulToken);
						}

						// Send ready to disconnect signal packet
						await base.SendAsync(signalPacket, gracefulToken);

						// Wait for remote end processed confirmation
						await _RemoteEndProcessedBarrier.WaitAsync(gracefulToken);

						GracefulDisconnect = true;
					}
				} finally {
					if(!_IsDisposing && !_IsDisposed) {
						_DisconnectLock.Release();
					}
				}
			} catch(Exception ex) {
				if(!_BaseSocket.IsDisconnectedException(ex)) {
					Debug.WriteLine(ex);
				}
			} finally {
				_BaseSocket.Disconnect();
			}

			return GracefulDisconnect;
		}

		// Send method including tracking unconfirmed packets
		public override async Task<int> SendAsync(Packet packet, CancellationToken token) => await SendAsync(packet, Config.PacketOrderType, token);

		public virtual async Task<int> SendAsync(Packet packet, PacketOrderType packetOrderType, CancellationToken token) {
			ObjectDisposedException.ThrowIf(_IsDisposing || _IsDisposed, this);

			using var tokenSource = CancellationTokenSource.CreateLinkedTokenSource(token, _BaseSocket.DisconnectToken);
			token = tokenSource.Token;

			token.ThrowIfCancellationRequested();

			await _SendLock.WaitAsync(token);
			try {

				int packetSize = await base.SendAsync(packet, token);
				if(Config.DebugLog && Config.DebugPackets) {
					Debug.WriteLine($"Sent packet id {packet.Id}");
				}

				if(packetSize > 0 && packet.Options.HasFlag(PacketOptions.RequiresConfirmation)) {

					BasePacket copyPacket = packet.CopyBasePacket();

					_UnconfirmedPackets.Add(copyPacket);
					if(packetOrderType == PacketOrderType.Sequential) {
						using TimeoutTokenSource timeoutTokenSource = new();
						timeoutTokenSource.AddToken(token);
						timeoutTokenSource.AddTimeout(Config.ConfirmationTimeout);

						timeoutTokenSource.Build();
						try {

						} catch(OperationCanceledException ex) {
							if(!token.IsCancellationRequested) {
								throw new TimeoutException("Confirmation not received");
							}

							if(!IsConnected) {
								throw new NotConnectedException(string.Empty, ex);
							}
							
							if(token.IsCancellationRequested) {
								throw;
							}
						}
						await _UnconfirmedPackets.WaitForItemRemovedAsync(copyPacket, timeoutTokenSource.Token);
					}
				}

				return packetSize;

			} finally {
				if(!_IsDisposing && !_IsDisposed) {
					_SendLock.Release();
				}
			}
		}

		// Send method including tracking unconfirmed packets
		protected override async Task<int> SendAsync(BasePacket basePacket, CancellationToken token) => await SendAsync(basePacket, Config.PacketOrderType, token);

		protected virtual async Task<int> SendAsync(BasePacket basePacket, PacketOrderType packetOrderType, CancellationToken token) {
			ObjectDisposedException.ThrowIf(_IsDisposing || _IsDisposed, this);

			using var tokenSource = CancellationTokenSource.CreateLinkedTokenSource(token, _BaseSocket.DisconnectToken);
			token = tokenSource.Token;

			token.ThrowIfCancellationRequested();

			await _SendLock.WaitAsync(token);
			try {

				int packetSize = await base.SendAsync(basePacket, token);
				if(packetSize > 0 && basePacket.Options.HasFlag(PacketOptions.RequiresConfirmation)) {
					_UnconfirmedPackets.Add(basePacket);
					if(packetOrderType == PacketOrderType.Sequential) {
						using TimeoutTokenSource timeoutTokenSource = new();
						timeoutTokenSource.AddToken(token);
						timeoutTokenSource.AddTimeout(Config.ConfirmationTimeout);

						timeoutTokenSource.Build();

						await _UnconfirmedPackets.WaitForItemRemovedAsync(basePacket, timeoutTokenSource.Token);
					}
				}

				return packetSize;

			} finally {
				if(!_IsDisposing && !_IsDisposed) {
					_SendLock.Release();
				}
			}
		}

		private readonly object _lock = new();
		protected override void Dispose(bool disposing) {
			lock(_lock) {
				if(!( _IsDisposing || _IsDisposed )) { // only dispose once regardless of disposing flag
					_IsDisposing = true;
					_RemoteEndProcessedBarrier.Dispose();
					_SessionIdResolvedBarrier.Dispose();
					_DisconnectedTrigger.Dispose();
					_SendLock.Dispose();
					_DisconnectLock.Dispose();
					_IsDisposed = true;
				}

				base.Dispose(disposing);
			}
		}
	}
}