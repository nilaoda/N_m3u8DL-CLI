using System;
using System.IO;
using System.Linq;
using System.Text;

namespace N_m3u8DL_CLI
{
    class DecodeNfmovies
    {
        //https://jx.nfmovies.com/hls.min.js
        public static string DecryptM3u8(byte[] byteArray)
        {
            var t = byteArray;
            var decrypt = "";
            if (137 == t[0] && 80 == t[1] && 130 == t[354] && 96 == t[353]) t = t.Skip(355).ToArray();
            else
            {
                if (137 != t[0] || 80 != t[1] || 130 != t[394] || 96 != t[393])
                {
                    for (var i = 0; i < t.Length; i++) decrypt += Convert.ToChar(t[i]);
                    return decrypt;
                }
                t = t.Skip(395).ToArray();
            }
            using (var zipStream =
                new System.IO.Compression.GZipStream(new MemoryStream(t), System.IO.Compression.CompressionMode.Decompress))
            {
                using (StreamReader sr = new StreamReader(zipStream, Encoding.UTF8))
                {
                    decrypt = sr.ReadToEnd();
                }
            }
            return decrypt;
        }
    }
}
