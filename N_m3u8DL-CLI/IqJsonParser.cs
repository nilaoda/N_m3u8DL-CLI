using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace N_m3u8DL_CLI
{
    class IqJsonParser
    {
        public static string Parse(string downDir, string json)
        {
            JObject jObject = JObject.Parse(json);
            var aClips = jObject["payload"]["wm_a"]["audio_track1"]["files"].Value<JArray>();
            var vClips = jObject["payload"]["wm_a"]["video_track1"]["files"].Value<JArray>();

            var codecsList = new List<string>();

            var audioPath = "";
            var videoPath = "";
            var audioInitPath = "";
            var videoInitPath = "";

            if (aClips.Count > 0)
            {
                var init = jObject["payload"]["wm_a"]["audio_track1"]["codec_init"].Value<string>();
                byte[] bytes = Convert.FromBase64String(init);
                //输出init文件
                audioInitPath = Path.Combine(downDir, "iqAudioInit.mp4");
                File.WriteAllBytes(audioInitPath, bytes);
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("#EXTM3U");
                sb.AppendLine("#EXT-X-VERSION:3");
                sb.AppendLine("#EXT-X-PLAYLIST-TYPE:VOD");
                sb.AppendLine("#CREATED-BY:N_m3u8DL-CLI");
                sb.AppendLine($"#EXT-CODEC:{jObject["payload"]["wm_a"]["audio_track1"]["codec"].Value<string>()}");
                sb.AppendLine($"#EXT-KID:{jObject["payload"]["wm_a"]["audio_track1"]["key_id"].Value<string>()}");
                sb.AppendLine($"#EXT-X-MAP:URI=\"{new Uri(Path.Combine(downDir + "(Audio)", "iqAudioInit.mp4")).ToString()}\"");
                sb.AppendLine("#EXT-X-KEY:METHOD=PLZ-KEEP-RAW,URI=\"None\"");
                foreach (var a in aClips)
                {
                    sb.AppendLine($"#EXTINF:{a["duration_second"].ToString()}");
                    sb.AppendLine(a["file_name"].Value<string>());
                }
                sb.AppendLine("#EXT-X-ENDLIST");
                //输出m3u8文件
                var _path = Path.Combine(downDir, "iqAudio.m3u8");
                File.WriteAllText(_path, sb.ToString());
                audioPath = new Uri(_path).ToString();
                codecsList.Add(jObject["payload"]["wm_a"]["audio_track1"]["codec"].Value<string>());
            }

            if (vClips.Count > 0)
            {
                var init = jObject["payload"]["wm_a"]["video_track1"]["codec_init"].Value<string>();
                byte[] bytes = Convert.FromBase64String(init);
                //输出init文件
                videoInitPath = Path.Combine(downDir, "iqVideoInit.mp4");
                File.WriteAllBytes(videoInitPath, bytes);
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("#EXTM3U");
                sb.AppendLine("#EXT-X-VERSION:3");
                sb.AppendLine("#EXT-X-PLAYLIST-TYPE:VOD");
                sb.AppendLine("#CREATED-BY:N_m3u8DL-CLI");
                sb.AppendLine($"#EXT-CODEC:{jObject["payload"]["wm_a"]["video_track1"]["codec"].Value<string>()}");
                sb.AppendLine($"#EXT-KID:{jObject["payload"]["wm_a"]["video_track1"]["key_id"].Value<string>()}");
                sb.AppendLine($"#EXT-X-MAP:URI=\"{new Uri(videoInitPath).ToString()}\"");
                sb.AppendLine("#EXT-X-KEY:METHOD=PLZ-KEEP-RAW,URI=\"None\"");
                foreach (var a in vClips)
                {
                    var start = a["seekable"]["pos_start"].Value<long>();
                    var size = a["size"].Value<long>();
                    sb.AppendLine($"#EXTINF:{a["duration_second"].ToString()}");
                    sb.AppendLine($"#EXT-X-BYTERANGE:{size}@{start}");
                    sb.AppendLine(a["file_name"].Value<string>());
                }
                sb.AppendLine("#EXT-X-ENDLIST");
                //输出m3u8文件
                var _path = Path.Combine(downDir, "iqVideo.m3u8");
                File.WriteAllText(_path, sb.ToString());
                videoPath = new Uri(_path).ToString();
                codecsList.Add(jObject["payload"]["wm_a"]["video_track1"]["codec"].Value<string>());
            }

            var content = "";
            if ((videoPath == "" && audioPath != "") || Global.VIDEO_TYPE == "IGNORE")
            {
                return audioPath;
            }
            else if (audioPath == "" && videoPath != "")
            {
                return videoPath;
            }
            else
            {
                if (!Directory.Exists(downDir + "(Audio)"))
                    Directory.CreateDirectory(downDir + "(Audio)");
                var _path = Path.Combine(downDir + "(Audio)", "iqAudio.m3u8");
                var _pathInit = Path.Combine(downDir + "(Audio)", "iqAudioInit.mp4");
                File.Copy(new Uri(audioPath).LocalPath, _path, true);
                File.Copy(new Uri(audioInitPath).LocalPath, _pathInit, true);
                audioPath = new Uri(_path).ToString();
                content = $"#EXTM3U\r\n" +
                    $"#EXT-X-MEDIA:TYPE=AUDIO,URI=\"{audioPath}\",GROUP-ID=\"default-audio-group\",NAME=\"stream_0\",AUTOSELECT=YES,CHANNELS=\"0\"\r\n" +
                    $"#EXT-X-STREAM-INF:BANDWIDTH=99999,CODECS=\"{string.Join(",", codecsList)}\",RESOLUTION=0x0,AUDIO=\"default-audio-group\"\r\n" +
                    $"{videoPath}";
            }

            var _masterPath = Path.Combine(downDir, "master.m3u8");
            File.WriteAllText(_masterPath, content);
            return new Uri(_masterPath).ToString();
        }
    }
}
