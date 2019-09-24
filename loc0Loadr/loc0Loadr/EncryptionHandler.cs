using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Modes;
using Org.BouncyCastle.Crypto.Paddings;
using Org.BouncyCastle.Crypto.Parameters;
using TagLib.Riff;

namespace loc0Loadr
{
    internal static class EncryptionHandler
    { // all credit for the actual logic goes to the devs at smloadr, this is just my implementation

        public static string GetDownloadUrl(TrackInfo trackInfo, int qualityId)
        {
            char cdn = trackInfo.TrackTags.Md5Origin[0];
            string encryptedFilename = GetEncryptedFilename(trackInfo, qualityId.ToString());

            return $"https://e-cdns-proxy-{cdn}.dzcdn.net/mobile/1/{encryptedFilename}";
        }

        public static byte[] DecryptTrack(byte[] downloadBytes, string sngId)
        {
            string blowfishKey = GetBlowfishKey(sngId);
            byte[] keyBytes = Encoding.UTF8.GetBytes(blowfishKey);
            long streamLength = downloadBytes.Length;
            const int chunk = 6144;
            const int workers = 4;

            var e = streamLength / chunk;
            var f = e / workers;
            var r = f * workers * chunk;
            var q = streamLength - r;
            
            List<Worker> y = new List<Worker>();

            for (var i = 0; i < workers; i++)
            {
                var worker = new Worker()
                {
                    StartingChunk = i * f * chunk,
                    EndChunk = (i + 1) * f * chunk,
                    OrderId = i
                };
  
                if (i + 1 == workers)
                {
                    worker.EndChunk += q;
                }
                
                y.Add(worker);
            }

            var decryptedBytes = new byte[streamLength];
            var chunkSize = 2048;
            var progress = 0;

            while (progress < streamLength)
            {
                if (streamLength - progress < 2048)
                {
                    chunkSize = (int) streamLength - progress;
                }

                var encryptedChunk = new byte[chunkSize];
                Buffer.BlockCopy(downloadBytes, progress, encryptedChunk, 0, chunkSize);

                // this will only decrypt every third chunk and if it's not at the end
                if (progress % (chunkSize * 3) == 0 && chunkSize == 2048)
                {
                    var blowfishEngine = new BlowfishEngine();
                    var cipher = new PaddedBufferedBlockCipher(new CbcBlockCipher(blowfishEngine), new ZeroBytePadding());
                    var keyParameter = new KeyParameter(keyBytes);
                    var parameters = new ParametersWithIV(keyParameter, new byte [] {0, 1, 2, 3, 4, 5, 6, 7});
                    cipher.Init(false, parameters);

                    var output = new byte[cipher.GetOutputSize(encryptedChunk.Length)];
                    int len = cipher.ProcessBytes(encryptedChunk, 0, encryptedChunk.Length, output, 0);
                    cipher.DoFinal(output, len);
                    Buffer.BlockCopy(output, 0, encryptedChunk, 0, output.Length);
                }
                
                Buffer.BlockCopy(encryptedChunk, 0, decryptedBytes, progress, encryptedChunk.Length);

                progress += chunkSize;
            }

            return decryptedBytes;
        }

        private static string GetEncryptedFilename(TrackInfo trackInfo, string qualityId)
        {
            string md5Origin = trackInfo.TrackTags.Md5Origin;
            string sngId = trackInfo.TrackTags.Id;
            string mediaVersion = trackInfo.TrackTags.MediaVersion;
            
            string itemsJoined = string.Join("¤", md5Origin, qualityId, sngId, mediaVersion);
            string newHash = string.Empty;

            using (MD5 md5 = MD5.Create())
            {
                byte[] itemsJoinedBytes = Encoding.ASCII.GetBytes(itemsJoined);
                itemsJoinedBytes = FixStarCharBytes(itemsJoinedBytes);

                byte[] itemsJoinedHashed = md5.ComputeHash(itemsJoinedBytes);

                var hexBuilder = new StringBuilder(itemsJoinedHashed.Length * 2);

                foreach (byte b in itemsJoinedHashed)
                {
                    hexBuilder.Append(b.ToString("x2"));
                }

                hexBuilder.Append("¤")
                    .Append(itemsJoined)
                    .Append("¤");

                newHash = hexBuilder.ToString();
            }

            while (newHash.Length % 16 != 0)
            {
                newHash += " ";
            }

            return AesEncryptHash(newHash);
        }

        private static string AesEncryptHash(string hash)
        {
            byte[] keyBytes = Encoding.ASCII.GetBytes("jo6aey6haid2Teih");
            var iV = new byte[] {0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0};

            var aes = new AesManaged
            {
                KeySize = 128,
                BlockSize = 128,
                Mode = CipherMode.ECB,
                Key = keyBytes,
                IV = iV
            };

            ICryptoTransform aesEncryptor = aes.CreateEncryptor(keyBytes, iV);

            var newHashBytes = Encoding.ASCII.GetBytes(hash);
            newHashBytes = FixStarCharBytes(newHashBytes);

            var encryptedHash = aesEncryptor.TransformFinalBlock(newHashBytes, 0, newHashBytes.Length);

            var encryptedHashBuilder = new StringBuilder();

            foreach (byte b in encryptedHash)
            {
                encryptedHashBuilder.Append(b.ToString("x2"));
            }

            string finalHash = encryptedHashBuilder.ToString();

            finalHash = finalHash.Substring(0, finalHash.Length - 32); // not sure why these extra 32 chars appear, maybe the IV?

            return finalHash;
        }

        private static byte[] FixStarCharBytes(byte[] bytes) // since the star symbol is unicode and i need ascii this is the hacky solution
        { // there's probably a correct way to do this but i haven't figured it out
            for (var index = 0; index < bytes.Length; index++)
            {
                byte itemsJoinedByte = bytes[index];

                if (itemsJoinedByte == 63)
                {
                    bytes[index] = 164;
                }

            }

            return bytes;
        }

        private static string GetBlowfishKey(string sngId)
        {
            const string secret = "g4el58wc0zvf9na1";
            string idHashedHex;
            
            using (MD5 md5 = MD5.Create())
            {
                byte[] idBytes = Encoding.ASCII.GetBytes(sngId);
                byte[] idHashed = md5.ComputeHash(idBytes);
                
                var hexBuilder = new StringBuilder();
                
                foreach (byte b in idHashed)
                {
                    hexBuilder.Append(b.ToString("x2"));
                }

                idHashedHex = hexBuilder.ToString();
            }

            string blowfishKey = string.Empty;

            for (var i = 0; i < 16; i++)
            { // some things don't get nice names :)
                int b = idHashedHex[i];
                int n = idHashedHex[i + 16];
                int m = secret[i];
                char a = Convert.ToChar(b ^ n ^ m);
                string s = a.ToString();
                blowfishKey += s;
            }

            return blowfishKey;
        }
    }

    internal class Worker
    {
        public long StartingChunk { get; set; }
        public long EndChunk { get; set; }
        public int OrderId { get; set; }
    }
}