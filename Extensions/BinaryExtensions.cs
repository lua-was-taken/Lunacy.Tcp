using System.Text;

namespace Lunacy.Tcp.Extensions {
	public static class BinaryExtensions {
		public static string AsString(this byte[] value) => AsString((Memory<byte>)value);
		public static string AsString(this Memory<byte> value) {
			return Encoding.UTF8.GetString(value.Span);
		}

		public static Memory<byte> AsBytes(this string value) {
			return Encoding.UTF8.GetBytes(value);
		}

		public static Memory<byte> Combine(this byte[] array, params Memory<byte>[] arrays) => Combine((Memory<byte>)array, arrays);
		public static Memory<byte> Combine(this Memory<byte> array, params Memory<byte>[] arrays) {
			return Combine([array, .. arrays]);
		}
		public static Memory<byte> Combine(params Memory<byte>[] arrays) {
			byte[] rv = new byte[arrays.Sum(a => a.Length)];

			int offset = 0;
			foreach(Memory<byte> array in arrays.Where(x => x.Length > 0)) {
				Buffer.BlockCopy(array.ToArray(), 0, rv, offset, array.Length);
				offset += array.Length;
			}

			return rv;
		}

		public static string ToHex(this byte[] value) => ToHex((Memory<byte>)value);
		public static string ToHex(this Memory<byte> value, int limit = -1) {
			if(limit > 0) {
				return string.Join(string.Empty, value[..Math.Min(value.Length, limit)].ToArray().Select(x => x.ToString("X2")));
			}

			return string.Join(string.Empty, value.ToArray().Select(x => x.ToString("X2")));
		}

		public static byte[] FromHex(this string hex) {
			return Enumerable.Range(0, hex.Length)
							 .Where(x => x % 2 == 0)
							 .Select(x => Convert.ToByte(hex.Substring(x, 2), 16))
							 .ToArray();
		}
	}
}