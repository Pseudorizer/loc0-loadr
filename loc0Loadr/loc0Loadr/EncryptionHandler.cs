using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using loc0Loadr.Models;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Modes;
using Org.BouncyCastle.Crypto.Paddings;
using Org.BouncyCastle.Crypto.Parameters;

namespace loc0Loadr
{
    internal static class EncryptionHandler
    { // all credit for most of the actual logic goes to the devs at smloadr/original maker of deezloader, this is just my implementation

        public static string GetDownloadUrl(TrackInfo trackInfo, int qualityId)
        {
            char cdn = trackInfo.TrackTags.Md5Origin[0];
            string encryptedFilename = GetEncryptedFilename(trackInfo, qualityId.ToString());

            return $"https://e-cdns-proxy-{cdn}.dzcdn.net/mobile/1/{encryptedFilename}";
        }

        public static async Task<byte[]> DecryptTrack(byte[] downloadBytes, string sngId)
        {
            string blowfishKey = GetBlowfishKey(sngId);
            byte[] keyBytes = Encoding.UTF8.GetBytes(blowfishKey);
            long streamLength = downloadBytes.Length;
            
            var decryptedChunks = new List<DecryptedBytes>();
            var decryptTasks = new List<Task>();

            // ReSharper disable once LoopCanBeConvertedToQuery
            foreach (Worker worker in GetWorkers(streamLength))
            {
                decryptTasks.Add(
                    Task.Run(() =>
                    {
                        var workerBytes = new byte[worker.ByteRange];
                        Buffer.BlockCopy(downloadBytes, (int) worker.StartingOffset, workerBytes, 0, (int) worker.ByteRange);

                        var decryptedBytes = new DecryptedBytes
                        {
                            OrderId = worker.OrderId,
                            StartingOffset = worker.StartingOffset,
                            Bytes = DecryptChunks(workerBytes, worker.ByteRange, keyBytes)
                        };

                        decryptedChunks.Add(decryptedBytes);
                    }));
            }

            await Task.WhenAll(decryptTasks);
            
            var final = new byte[streamLength];
            
            foreach (DecryptedBytes decryptedBytes in decryptedChunks.OrderBy(x => x.OrderId))
            {
                Buffer.BlockCopy(decryptedBytes.Bytes, 0, final, (int) decryptedBytes.StartingOffset, decryptedBytes.Bytes.Length);
            }

            return final;
        }

        private static IEnumerable<Worker> GetWorkers(long streamLength)
        { // Thanks to Chimera for the math!
            const int numberOfWorkers = 4;
            const int chunk = (numberOfWorkers - 1) * 2048;

            long totalNumberOfChunks = streamLength / chunk;
            long chunksPerWorker = totalNumberOfChunks / numberOfWorkers;
            long subTotal = chunksPerWorker * numberOfWorkers * chunk;
            long remainingBytes = streamLength - subTotal;

            for (var i = 0; i < numberOfWorkers; i++)
            {
                var worker = new Worker
                {
                    StartingOffset = i * chunksPerWorker * chunk,
                    EndOffset = (i + 1) * chunksPerWorker * chunk,
                    OrderId = i
                };

                if (i + 1 == numberOfWorkers)
                {
                    worker.EndOffset += remainingBytes;
                }

                yield return worker;
            }
        }

        private static byte[] DecryptChunks(byte[] downloadBytes, long streamLength, byte[] keyBytes)
        {
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
                    var parameters = new ParametersWithIV(keyParameter, new byte[] {0, 1, 2, 3, 4, 5, 6, 7});
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
            string newHash;

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
}