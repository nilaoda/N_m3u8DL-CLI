using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace N_m3u8DL_CLI
{
    class DecodeDdyun
    {
        public static string DecryptM3u8(byte[] byteArray)
        {
            string tmp = DecodeNfmovies.DecryptM3u8(byteArray);
            if (tmp.StartsWith("duoduo.key"))
            {
                tmp = Regex.Replace(tmp, @"#EXT-X-BYTERANGE:.*\s", "");
                tmp = tmp.Replace("https:", "jump/https:")
                    .Replace("inews.gtimg.com", "puui.qpic.cn");
            }
            return tmp;
        }

        //https://player.ddyunp.com/jQuery.min.js?v1.5
        public static string GetVaildM3u8Url(string url)
        {
            //url: https://hls.ddyunp.com/ddyun/id/1/key/playlist.m3u8
            string id = Regex.Match(url, @"\w{20,}").Value;
            string tm = Global.GetTimeStamp(false);
            string t = ((long.Parse(tm) / 0x186a0) * 0x64).ToString();
            string tmp = id + "duoduo" + "1" + t;
            MD5 md5 = MD5.Create();
            byte[] bs = Encoding.UTF8.GetBytes(tmp);
            byte[] hs = md5.ComputeHash(bs);
            StringBuilder sb = new StringBuilder();
            foreach (byte b in hs)
            {
                sb.Append(b.ToString("x2"));
            }
            string key = sb.ToString();
            return Regex.Replace(url, @"1/\w{20,}", "1/" + key);
        }
    }
}
