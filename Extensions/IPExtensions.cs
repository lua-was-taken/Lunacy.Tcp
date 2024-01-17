using System.Net;

namespace Lunacy.Tcp.Extensions {
	public static class IPExtensions {
		public static string ToEndPointString(this IPEndPoint endPoint) {
			string addressStr = endPoint.Address.ToString();
			if(addressStr != "::1") {
				addressStr = endPoint.Address.MapToIPv4().ToString();
			}

			if(addressStr == "127.0.0.1" || addressStr == "::1") { addressStr = "localhost"; }

			return addressStr + ":" + endPoint.Port;
		}
	}
}