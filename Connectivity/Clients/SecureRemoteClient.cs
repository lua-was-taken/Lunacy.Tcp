using Lunacy.Tcp.Enums;
using Lunacy.Tcp.Exceptions;
using Lunacy.Tcp.Extensions;
using Lunacy.Tcp.Utility;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

namespace Lunacy.Tcp.Connectivity.Clients {
	public class SecureRemoteClient(ClientConfig config, Socket socket) : RemoteClient(config, socket) {
		public new event EventHandler? Connected, Disconnected;
		public new event EventHandler<Packet>? PacketPublished;

		protected internal readonly GlobalBarrier _ConnectionSecureBarrier = new(blocking: true);

		protected readonly EndToEndEncryptor _Encryptor = new();
		protected EndToEndStateType _EncryptionState = EndToEndStateType.Unencrypted;

		public override async Task<bool> ConnectAsync(IPEndPoint remoteEndPoint, CancellationToken token) {
			bool isConnected = await base.ConnectAsync(remoteEndPoint, token);
			if(!isConnected) {
				return false;
			}

			using CancellationTokenSource linkedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(token, DisconnectToken);
			token = linkedTokenSource.Token;

			token.ThrowIfCancellationRequested();

			try {
				using TimeoutTokenSource tokenSource = new();
				tokenSource.AddToken(token);
				tokenSource.AddToken(DisconnectToken);
				tokenSource.AddTimeout(TimeSpan.FromSeconds(10));

				Debug.WriteLine("Waiting for secure barrier release");
				await _ConnectionSecureBarrier.WaitAsync(tokenSource.Build());
				Debug.WriteLine("Secure connection built successfully");
			} catch(Exception ex) when (ex is OperationCanceledException) {
				if(!DisconnectToken.IsCancellationRequested && !token.IsCancellationRequested) {
					DisconnectFast();
					if(Config.DebugLog) {
						Debug.WriteLine("Exceeded E2EE handshake timeout limit");
					}
					return false;
				} else if(DisconnectToken.IsCancellationRequested) {
					DisconnectFast();
					if(Config.DebugLog) {
						Debug.WriteLine("Remote end disconnected during E2EE handshake");
					}
					return false;
				} else {
					DisconnectFast();
					isConnected = false;

					if(token.IsCancellationRequested) { 
						throw; 
					}
				}
			}
			
			return _BaseSocket.IsConnected;
		}

		protected override void OnClientConnected(object? sender, EventArgs e) {
			base.OnClientConnected(sender, e);
		}

		protected override void OnClientDisconnected(object? sender, EventArgs e) {
			base.OnClientDisconnected(sender, e);

			try {
				if(!_IsDisposing && !_IsDisposed) {
					_Encryptor.Reset();
					_EncryptionState = EndToEndStateType.Unencrypted;
					_ConnectionSecureBarrier.Block();
				}
			} catch(ObjectDisposedException) { }
		}

		protected override void InvokeConnectedEvent(object? sender, EventArgs e) {
			if(_EncryptionState == EndToEndStateType.Encrypted) {
				base.InvokeConnectedEvent(sender, e);
			} else {
				if(_EncryptionState == EndToEndStateType.Unencrypted && Direction == ConnectionDirectionType.Initiator) {
					lock(_E2EELock) {
						_Encryptor.GenerateRSA();

						Packet publicKeyPacket = new(payload: _Encryptor.RSAPublicKey) {
							Options = PacketOptions.HasData | PacketOptions.RequiresConfirmation | PacketOptions.Internal
						};

						if(Config.DebugLog) {
							Debug.WriteLine($"Generating & sending RSA key {publicKeyPacket.Id}, (Next: HANDSHAKE)");
						}

						Task.Run(() => base.SendAsync(publicKeyPacket, PacketOrderType.NonSequential, CancellationToken.None)).Wait(DisconnectToken);
						_EncryptionState = EndToEndStateType.Handshake;
					}
				}
			}
		}

		protected override void InvokeDisconnectedEvent(object? sender, EventArgs e) {
			if(_EncryptionState == EndToEndStateType.Encrypted) {
				base.InvokeDisconnectedEvent(sender, e);
			}
		}

		protected readonly object _E2EELock = new();
		protected override void InvokePacketPublishedEvent(object? sender, Packet e) {
			if(e.Options.HasFlag(PacketOptions.Internal)) {
				lock(_E2EELock) {
					try {
						switch(_EncryptionState) {
							case EndToEndStateType.Unencrypted:

								if(Config.DebugLog) {
									Debug.WriteLine($"UNENCRYPTED: Received RSA key {e.Id}");
								}

								_Encryptor.SetRSAPublicKey(e.Payload);
								_Encryptor.GenerateAes();

								Memory<byte> encryptedAesKeyAndIV = _Encryptor.EncryptRSA(_Encryptor.AesKey.Combine(_Encryptor.AesIV));
								Packet aesKeyPacket = new(payload: encryptedAesKeyAndIV) {
									Options = PacketOptions.HasData | PacketOptions.RequiresConfirmation | PacketOptions.Internal
								};

								if(Config.DebugLog) {
									Debug.WriteLine("UNENCRYPTED: Received RSA key, generatin Aes key, sending RSA encrypted aes key (Next: CONTROL)");
								}

								Task.Run(() => base.SendAsync(aesKeyPacket, PacketOrderType.NonSequential, CancellationToken.None)).Wait(DisconnectToken);
								_EncryptionState = EndToEndStateType.Control;
								break;

							case EndToEndStateType.Handshake:
								if(Direction == ConnectionDirectionType.Initiator) {
									
									if(Config.DebugLog) {
										Debug.WriteLine($"HANDSHAKE: Received aes key packet {e.Id}");
									}

									Memory<byte> decryptedAesKeyAndIV = _Encryptor.DecryptRSA(e.Payload);

									Memory<byte> decryptedAesKey = decryptedAesKeyAndIV[..32];
									Memory<byte> decryptedAesIV = decryptedAesKeyAndIV[32..48];

									_Encryptor.SetAesFromKeyAndIV(decryptedAesKey, decryptedAesIV);


									Packet e2eeTestPacket = new(payload: _Encryptor.EncryptAes(Signatures.EndToEndTestSignature.AsBytes())) {
										Options = PacketOptions.HasData | PacketOptions.RequiresConfirmation | PacketOptions.Internal
									};

									if(Config.DebugLog) {
										Debug.WriteLine($"HANDSHAKE: Decrypted Aes key, setting Aes key, sending e2ee test packet ({e2eeTestPacket.Id}) (Next: CONTROL)");
									}

									Task.Run(() => base.SendAsync(e2eeTestPacket, PacketOrderType.NonSequential, CancellationToken.None)).Wait(DisconnectToken);
									_EncryptionState = EndToEndStateType.Control;
								} else {
									throw new EndToEndException("Unexpected execution flow");
								}
								break;

							case EndToEndStateType.Control:
								Memory<byte> e2eeTestPayload = _Encryptor.DecryptAes(e.Payload);
								
								if(Config.DebugLog) {
									Debug.WriteLine($"CONTROL: Received e2ee test packet {e.Id}");
								}

								if(e2eeTestPayload.AsString() != Signatures.EndToEndTestSignature) {
									throw new EndToEndException("E2EE test signature mismatch");
								}

								if(Config.DebugLog) {
									Debug.WriteLine("CONTROL: Handshake succeeded (Next: ENCRYPTED)");
								}

								if(Direction == ConnectionDirectionType.Receiver) {
									Packet e2eeTestPacket = new(payload: _Encryptor.EncryptAes(Signatures.EndToEndTestSignature.AsBytes())) {
										Options = PacketOptions.HasData | PacketOptions.RequiresConfirmation | PacketOptions.Internal
									};

									if(Config.DebugLog) {
										Debug.WriteLine($"CONTROL: Sending e2ee test packet ({e2eeTestPacket.Id}) (Next: ENCRYPTED)");
									}

									Task.Run(() => base.SendAsync(e2eeTestPacket, PacketOrderType.NonSequential, CancellationToken.None)).Wait(DisconnectToken);
								}

								_EncryptionState = EndToEndStateType.Encrypted;
								break;

							case EndToEndStateType.Encrypted:

								if(Config.DebugLog) {
									Debug.WriteLine($"ENCRYPTED: Received internal packet post e2ee handshake, publishing {e.Id}");
								}

								base.InvokePacketPublishedEvent(sender, e);
								break;
							default:
								throw new NotSupportedException("Unknown encryption state");
						}
					} catch(Exception ex) when(ex is not NotConnectedException) {
						throw new EndToEndException(string.Empty, innerException: ex);
					} finally {

					}
				}

				if(_EncryptionState == EndToEndStateType.Encrypted) {

					_ConnectionSecureBarrier.Unblock();
					if(Config.DebugLog) {
						Debug.WriteLine("ENCRYPTED: Unblocking barrier, handshake complete");
					}

					InvokeConnectedEvent(this, EventArgs.Empty);
				}
			} else {

				_ConnectionSecureBarrier.Unblock();
				if(Config.DebugLog) {
					Debug.WriteLine($"ENCRYPTED: Decrypting payload {e.Id}");
				}

				e.Payload = _Encryptor.DecryptAes(e.Payload);
				base.InvokePacketPublishedEvent(sender, e);
			}
		}

		protected override async Task<int> SendAsync(BasePacket basePacket, PacketOrderType packetOrderType, CancellationToken token) {
			ObjectDisposedException.ThrowIf(_IsDisposing || _IsDisposed, this);
			if(!IsConnected) {
				throw new NotConnectedException();
			}

			if(_EncryptionState != EndToEndStateType.Encrypted) {
				using CancellationTokenSource linkedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(token, DisconnectToken);

				linkedTokenSource.Token.ThrowIfCancellationRequested();
				await _ConnectionSecureBarrier.WaitAsync(linkedTokenSource.Token);
			}

			return await base.SendAsync(basePacket, packetOrderType, token);
		}

		public override async Task<int> SendAsync(Packet packet, PacketOrderType packetOrderType, CancellationToken token) {
			if(!IsConnected) {
				throw new NotConnectedException();
			}

			if(_EncryptionState != EndToEndStateType.Encrypted) {
				using CancellationTokenSource linkedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(token, DisconnectToken);

				linkedTokenSource.Token.ThrowIfCancellationRequested();
				await _ConnectionSecureBarrier.WaitAsync(linkedTokenSource.Token);
			} else {
				packet.Payload = _Encryptor.EncryptAes(packet.Payload);
			}

			return await base.SendAsync(packet, packetOrderType, token);
		}

		protected override void Dispose(bool disposing) {
			if(!( _IsDisposing || _IsDisposed ) || disposing) {
				_Encryptor.Dispose();
				_ConnectionSecureBarrier.Dispose();
			}

			base.Dispose(disposing);
		}
	}
}