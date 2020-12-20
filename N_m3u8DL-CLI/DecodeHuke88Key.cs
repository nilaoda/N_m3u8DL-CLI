using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace N_m3u8DL_CLI
{
    //https://js.huke88.com/assets/revision/js/plugins/tcplayer/tcplayer.v4.1.min.js?v=930
    //https://js.huke88.com/assets/revision/js/plugins/tcplayer/libs/hls.min.0.13.2m.js?v=930
    class DecodeHuke88Key
    {
        private static string[] GetOverlayInfo(string url)
        {
            var enc = new Regex("eyJ\\w{100,}").Match(url).Value;
            var json = Encoding.UTF8.GetString(Convert.FromBase64String(enc));
            JObject jObject = JObject.Parse(json);
            var key = jObject["overlayKey"].ToString();
            var iv = jObject["overlayIv"].ToString();
            return new string[] { key, iv };
        }

        public static string DecodeKey(string url, byte[] data)
        {
            var info = GetOverlayInfo(url);
            var overlayKey = info[0];
            var overlayIv = info[1];
            var l = new List<byte>();
            var c = new List<byte>();
            for (int h = 0; h < 16; h++)
            {
                var f = overlayKey.Substring(2 * h, 2);
                var g = overlayIv.Substring(2 * h, 2);
                l.Add(Convert.ToByte(f, 16));
                c.Add(Convert.ToByte(g, 16));
            }

            var _lastCipherblock = c.ToArray();

            var t = new byte[data.Length];
            var r = data;
            r = Decrypter.AES128Decrypt(data, l.ToArray(), Decrypter.HexStringToBytes("00000000000000000000000000000000"), CipherMode.CBC, PaddingMode.Zeros);

            for (var o = 0; o < 16; o++)
                t[o] = (byte)(r[o] ^ _lastCipherblock[o]);

            var key = Convert.ToBase64String(t);

            return key;
        }
    }
}
