using Lunacy.Tcp.Exceptions;
using System.Net;

namespace Lunacy.Tcp.Connectivity {
	internal static class NameResolver {
		public const string DefaultHost = "127.0.0.1:0";
		public static readonly IPEndPoint DefaultEndPoint = IPEndPoint.Parse("127.0.0.1:0");

		public static IPEndPoint Resolve(string host) {
			if(!host.Contains(':') || !int.TryParse(host.Split(":").LastOrDefault(), out int port)
			|| !host.EndsWith($":{port}")) {
				throw new FormatException(nameof(host));
			}

			string hostname = host[..^( port.ToString().Length + 1 )];
			return Resolve(hostname, port);
		}

		public static IPEndPoint Resolve(string hostname, int port) {
			switch(Uri.CheckHostName(hostname)) {
				case UriHostNameType.IPv6:
				case UriHostNameType.IPv4: return new IPEndPoint(IPAddress.Parse(hostname), port);

				case UriHostNameType.Basic:
				case UriHostNameType.Dns:
					IPHostEntry hostEntry = Dns.GetHostEntry(hostname);
					if(hostEntry.AddressList.Length > 0) {
						IPAddress? address = null;
						foreach(IPAddress _address in hostEntry.AddressList) {
							if(Uri.CheckHostName(_address.ToString()) == UriHostNameType.IPv4) {
								address = _address; 
								break;
							}
						}

						address ??= hostEntry.AddressList.FirstOrDefault();
						if(address == null) {
							throw new UnresolvedHostException(hostname);
						}

						if(address.ToString() == "::1") {
							address = IPAddress.Parse("127.0.0.1");
						} else {
							address = address.MapToIPv4();
						}

						return new IPEndPoint(address, port);
					}

					goto default;

				default: throw new UnresolvedHostException(hostname);
			}
		}
	}
}
