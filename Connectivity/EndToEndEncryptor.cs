using System.Security.Cryptography;

namespace Lunacy.Tcp.Connectivity {
	public sealed class EndToEndEncryptor : IDisposable {

		public bool HasAesKey { get; private set; } = false;
		public Memory<byte> AesKey {
			get {
				return Aes?.Key ?? Memory<byte>.Empty;
			}
		}

		public Memory<byte> AesIV {
			get {
				return Aes?.IV ?? Memory<byte>.Empty;
			}
		}

		public int AesIVSize {
			get {
				return Aes?.IV?.Length ?? -1;
			}
		}

		public int AesKeySize {
			get {
				return Aes?.KeySize ?? -1;
			}
		}

		public bool HasPublicKey { get; private set; } = false;
		public Memory<byte> RSAPublicKey {
			get {
				if(RSA != default && HasPublicKey) {
					return RSA.ExportRSAPublicKey();
				}

				return Memory<byte>.Empty;
			}
		}

		public bool HasPrivateKey { get; private set; } = false;
		public Memory<byte> RSAPrivateKey {
			get {
				if(RSA != default && HasPrivateKey) {
					return RSA.ExportRSAPrivateKey();
				}

				return Memory<byte>.Empty;
			}
		}

		public int RSAKeySize {
			get {
				return RSA?.KeySize ?? -1;
			}
		}

		public Aes? Aes { get; private set; } = default;
		public RSA? RSA { get; private set; } = default;

		public EndToEndEncryptor() {

		}

		public void GenerateAes() {
			if(HasAesKey) {
				throw new InvalidOperationException("Aes key/iv is already defined");
			}

			Aes ??= Aes.Create();

            Aes.BlockSize = 128;
            Aes.KeySize = 256;

            Aes.GenerateKey();
			Aes.GenerateIV();
		
			HasAesKey = true;
		}

		public void SetAesFromKeyAndIV(Memory<byte> key, Memory<byte> iv, int keySize = -1) {
			if(key.IsEmpty || iv.IsEmpty) {
				throw new ArgumentException("Aes key/iv cannot be empty", nameof(key));
			}

			if(HasAesKey) {
				throw new InvalidOperationException("Aes key/iv is already defined");
			}
			
			Aes ??= Aes.Create();
			if(keySize != -1) {
				Aes.KeySize = keySize;
			} 
			
			Aes.BlockSize = 128;
			Aes.KeySize = 256;

			Aes.Key = key.ToArray();
			Aes.IV = iv.ToArray();

			HasAesKey = true;
		}

		public void SetRSAPublicKey(Memory<byte> publicKey) {
			if(HasPublicKey) {
				throw new InvalidOperationException("Public key is already defined");
			}
			
			RSA ??= RSA.Create();
			RSA.ImportRSAPublicKey(publicKey.Span, out _);

			HasPublicKey = true;
		}

		public void SetRSAPrivateKey(Memory<byte> privateKey) {
			if(HasPrivateKey) {
				throw new InvalidOperationException("Private key is already defined");
			}

			RSA ??= RSA.Create();
			RSA.ImportRSAPrivateKey(privateKey.Span, out _);

			HasPrivateKey = true;
		}

		public void GenerateRSA() {
			if(HasPublicKey || HasPrivateKey) {
				throw new InvalidOperationException("Public key or private key is already defined");
			}

			RSA ??= RSA.Create();
			HasPublicKey = true;
			HasPrivateKey = true;
		}

		public Memory<byte> EncryptAes(Memory<byte> value) {
			if(!HasAesKey || Aes == default) {
				throw new InvalidOperationException("Aes not initialized");
			}

			if(value.IsEmpty) { return Memory<byte>.Empty; }

			return Aes.EncryptCbc(value.Span, Aes.IV, PaddingMode.PKCS7);
		}

		public Memory<byte> DecryptAes(Memory<byte> value) {
			if(!HasAesKey || Aes == default) {
				throw new InvalidOperationException("Aes not initialized");
			}

			if(value.IsEmpty) { return Memory<byte>.Empty; }

			return Aes.DecryptCbc(value.Span, Aes.IV, PaddingMode.PKCS7);
		}

		public Memory<byte> EncryptRSA(Memory<byte> value) {
			if(!HasPublicKey || RSA == default) {
				throw new InvalidOperationException("RSA not initialized for encryption");
			}

			if(value.IsEmpty) { return Memory<byte>.Empty; }

			return RSA.Encrypt(value.Span, RSAEncryptionPadding.Pkcs1);
		}

		public Memory<byte> DecryptRSA(Memory<byte> value) {
			if(!HasPrivateKey || RSA == default) {
				throw new InvalidOperationException("RSA not initialized for decryption");
			}

			if(value.IsEmpty) { return Memory<byte>.Empty; }

			return RSA.Decrypt(value.Span, RSAEncryptionPadding.Pkcs1);
		}

		public void Reset() {
			if(Aes != null) {
				Aes.Dispose();
				Aes = null;
			}

			if(RSA != null) {
				RSA.Dispose();
				RSA = null;
			}

			HasAesKey = false;
			HasPrivateKey = false;
			HasPublicKey = false;
		}

		public void Dispose() {
			Dispose(false);
			GC.SuppressFinalize(this);
		}

		private volatile bool _IsDisposing = false;
		private volatile bool _IsDisposed = false;
		private void Dispose(bool disposing) {
			if(!(_IsDisposing || _IsDisposed) || disposing) {
				_IsDisposing = true;
				if(Aes != default) {
					Aes.Dispose();
				}

				if(RSA != default) {
					RSA.Dispose();
				}
				_IsDisposed = true;
			}
		}

		~EndToEndEncryptor() {
			Dispose(true);
		}
	}
}