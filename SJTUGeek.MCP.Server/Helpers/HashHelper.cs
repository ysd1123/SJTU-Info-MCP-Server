using System.Security.Cryptography;
using System.Text;
using System.IO.Hashing;

namespace SJTUGeek.MCP.Server.Helpers
{
    public static class HashHelper
    {
        public static string SHA1Hash(string str)
        {
            var buffer = Encoding.UTF8.GetBytes(str);
            var data = SHA1.Create().ComputeHash(buffer);

            StringBuilder sub = new StringBuilder();
            foreach (var t in data)
            {
                sub.Append(t.ToString("x2"));
            }

            return sub.ToString();
        }

        public static string SHA256Hash(string str)
        {
            var buffer = Encoding.UTF8.GetBytes(str);
            var data = SHA256.Create().ComputeHash(buffer);

            StringBuilder sub = new StringBuilder();
            foreach (var t in data)
            {
                sub.Append(t.ToString("x2"));
            }

            return sub.ToString();
        }

        public static string SHA256Hash(MemoryStream str)
        {
            var data = SHA256.Create().ComputeHash(str);

            StringBuilder sub = new StringBuilder();
            foreach (var t in data)
            {
                sub.Append(t.ToString("x2"));
            }

            return sub.ToString();
        }

        public static SHA256 SHA256Hash_Start()
        {
            return SHA256.Create();
        }

        public static int SHA256Hash_Proc(this SHA256 sha256, byte[] input)
        {
            return sha256.TransformBlock(input, 0, input.Length, null, 0);
        }

        public static byte[] SHA256Hash_Finish(this SHA256 sha256)
        {
            sha256.TransformFinalBlock(new byte[] { }, 0, 0);
            return sha256.Hash;
        }

        public static string MD5Hash(string str)
        {
            var buffer = Encoding.UTF8.GetBytes(str);
            var data = MD5.Create().ComputeHash(buffer);

            StringBuilder sub = new StringBuilder();
            foreach (var t in data)
            {
                sub.Append(t.ToString("x2"));
            }

            return sub.ToString();
        }

        public static string MD5Hash(MemoryStream str)
        {
            var data = MD5.Create().ComputeHash(str);

            StringBuilder sub = new StringBuilder();
            foreach (var t in data)
            {
                sub.Append(t.ToString("x2"));
            }

            return sub.ToString();
        }

        public static string CRC32Hash(string str)
        {
            var buffer = Encoding.UTF8.GetBytes(str);
            var crc32 = new Crc32();
            crc32.Append(buffer);
            var data = crc32.GetCurrentHash();

            StringBuilder sub = new StringBuilder();
            foreach (var t in data)
            {
                sub.Append(t.ToString("x2"));
            }

            return sub.ToString();
        }
    }
}
