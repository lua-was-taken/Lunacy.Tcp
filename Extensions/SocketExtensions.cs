using CommunityToolkit.HighPerformance;
using Lunacy.Tcp.Exceptions;
using System.Net.Sockets;

namespace Lunacy.Tcp.Extensions {
	internal static class SocketExtensions {
		public const int TCP_MCU = 65535;

		public static Task<Memory<byte>> ReceiveExactlyAsync(this Socket socket, int length) => ReceiveExactlyAsync(socket, length, CancellationToken.None);
		public static async Task<Memory<byte>> ReceiveExactlyAsync(this Socket socket, int length, CancellationToken token) {
			Memory<byte> buffer = new byte[length];
			int readSize = await ReceiveExactlyAsync(socket, buffer, length, token);

			if(readSize != length) {
				if(!socket.Connected || !socket.Poll(1000, SelectMode.SelectWrite)
							|| !socket.Poll(1000, SelectMode.SelectRead)
							|| !socket.Poll(1000, SelectMode.SelectError)) {
					throw new NotConnectedException();
				} else {
					throw new CorruptDataException("Did not receive expected data");
				}
			}

			return buffer;
		}

		public static Task<int> ReceiveExactlyAsync(this Socket socket, Memory<byte> buffer, int length) => ReceiveExactlyAsync(socket, buffer, length, CancellationToken.None);
		public static async Task<int> ReceiveExactlyAsync(this Socket socket, Memory<byte> buffer, int length, CancellationToken token) {
			if(length <= 0) {
				throw new ArgumentException("Length cannot be less than 0", nameof(length));
			}

			if(!socket.Connected) {
				throw new NotConnectedException("Socket is not connected");
			}

			Stream? stream = default;

			int readSize = 0;
			while(readSize != length) {
				int remainder = Math.Min(length - readSize, TCP_MCU);

				Memory<byte> partBuffer = new byte[remainder];
				socket.ReceiveBufferSize = remainder;

				int partReadSize = await socket.ReceiveAsync(partBuffer, token);
				if(partReadSize == 0) {
					break;
				}

				if(readSize == 0 && partReadSize >= length) {
					partBuffer.CopyTo(buffer);
					return partReadSize;
				}

				( stream ??= buffer.AsStream() ).Position = readSize;
				await stream.WriteAsync(partBuffer, token);

				readSize += partReadSize;
			}

			if(stream != default) {
				stream.Position = 0;
				await stream.DisposeAsync();
			}

			return readSize;
		}
	}
}