using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace N_m3u8DL_CLI
{
    class Parser
    {
        struct Audio
        {
            public string Name;
            public string Language;
            public string Uri;
            public string Channels;
            public override string ToString()
            {
                return $"[{Name}] [{Language}] [{(string.IsNullOrEmpty(Channels) ? "" : $"{Channels}ch")}]".Replace("[]", "");
            }
        }

        struct Subtitle
        {
            public string Name;
            public string Language;
            public string Uri;
            public override string ToString()
            {
                return $"[{Name}] [{Language}]";
            }
        }

        //存储上一行key的信息，如果一样，就跳过下载key这一步
        private string lastKeyLine = string.Empty;
        //METHOD, KEY, IV
        string[] m3u8CurrentKey = new string[] { "NONE", "", "" };
        private string m3u8SavePath = string.Empty;
        private string jsonSavePath = string.Empty;
        private long bestBandwidth = 0;
        private string bestUrl = string.Empty;
        private string bestUrlAudio = string.Empty;
        private string bestUrlSub = string.Empty;
        Dictionary<string, List<Audio>> MEDIA_AUDIO_GROUP = new Dictionary<string, List<Audio>>(); //外挂音频所有分组信息
        private string audioUrl = string.Empty; //音轨地址
        Dictionary<string, List<Subtitle>> MEDIA_SUB_GROUP = new Dictionary<string, List<Subtitle>>(); //外挂字幕所有分组信息
        private string subUrl = string.Empty; //字幕地址
        //存放多轨道的信息
        private ArrayList extLists = new ArrayList();
        //标记是否已清除优酷广告分片
        private static bool hasAd = false;

        public string BaseUrl { get; set; } = string.Empty;
        public string M3u8Url { get; set; } = string.Empty;
        public string DownDir { get; set; } = string.Empty;
        public string DownName { get; set; } = string.Empty;
        public string Headers { get; set; } = string.Empty;
        //存放Range信息，允许用户只下载部分视频
        public static int RangeStart { get; set; } = 0;
        public static int RangeEnd { get; set; } = -1;
        //是否自动清除优酷广告分片
        public static bool DelAd { get; set; } = true;
        //存放Range信息，允许用户只下载部分视频
        public static string DurStart { get; set; } = "";
        public static string DurEnd { get; set; } = "";
        public string KeyFile { get; set; } = string.Empty;
        public string KeyBase64 { get; set; } = string.Empty;
        public bool LiveStream { get; set; } = false;
        public string KeyIV { get; set; } = string.Empty;

        public void Parse()
        {
            FFmpeg.REC_TIME = "";

            m3u8SavePath = Path.Combine(DownDir, "raw.m3u8");
            jsonSavePath = Path.Combine(DownDir, "meta.json");

            if (!Directory.Exists(DownDir))//若文件夹不存在则新建文件夹   
                Directory.CreateDirectory(DownDir); //新建文件夹  

            //存放分部的所有信息(#EXT-X-DISCONTINUITY)
            JArray parts = new JArray();
            //存放分片的所有信息
            JArray segments = new JArray();
            JObject segInfo = new JObject();
            extLists.Clear();
            MEDIA_AUDIO_GROUP.Clear();
            MEDIA_SUB_GROUP.Clear();
            string m3u8Content = string.Empty;
            string m3u8Method = string.Empty;
            string[] extMAP = { "", "" };
            string[] extList = new string[10];
            long segIndex = 0;
            long startIndex = 0;
            int targetDuration = 0;
            double totalDuration = 0;
            bool expectSegment = false, expectPlaylist = false, isIFramesOnly = false,
                isIndependentSegments = false, isEndlist = false, isAd = false, isM3u = false;


            //获取m3u8内容
            //if (!LiveStream)
            //    LOGGER.PrintLine(strings.downloadingM3u8, LOGGER.Warning);

            if (M3u8Url.Contains(".cntv."))
            {
                M3u8Url = M3u8Url.Replace("/h5e/", "/");
            }

            if (M3u8Url.StartsWith("http"))
            {
                if (M3u8Url.Contains("nfmovies.com/hls"))
                    m3u8Content = DecodeNfmovies.DecryptM3u8(Global.HttpDownloadFileToBytes(M3u8Url, Headers));
                else if (M3u8Url.Contains("hls.ddyunp.com/ddyun") || M3u8Url.Contains("hls.90mm.me/ddyun"))
                    m3u8Content = DecodeDdyun.DecryptM3u8(Global.HttpDownloadFileToBytes(DecodeDdyun.GetVaildM3u8Url(M3u8Url), Headers));
                else
                    m3u8Content = Global.GetWebSource(M3u8Url, Headers);
            }
            else if (M3u8Url.StartsWith("file:"))
            {
                Uri t = new Uri(M3u8Url);
                m3u8Content = File.ReadAllText(t.LocalPath);
            }
            else if (File.Exists(M3u8Url))
            {
                m3u8Content = File.ReadAllText(M3u8Url);
                if (!M3u8Url.Contains("\\"))
                    M3u8Url = Path.Combine(Environment.CurrentDirectory, M3u8Url);
                Uri t = new Uri(M3u8Url);
                M3u8Url = t.ToString();
            }

            if (m3u8Content == "")
                return;

            if (M3u8Url.Contains("tlivecloud-playback-cdn.ysp.cctv.cn") && M3u8Url.Contains("endtime="))
                isEndlist = true;

            if (M3u8Url.Contains("imooc.com/"))
            {
                m3u8Content = DecodeImooc.DecodeM3u8(m3u8Content);
            }

            if (m3u8Content.Contains("</MPD>") && m3u8Content.Contains("<MPD"))
            {
                //LOGGER.PrintLine(strings.startParsingMpd, LOGGER.Warning);
                //LOGGER.WriteLine(strings.startParsingMpd);
                var mpdSavePath = Path.Combine(DownDir, "dash.mpd");
                //输出mpd文件
                File.WriteAllText(mpdSavePath, m3u8Content);
                //分析mpd文件
                M3u8Url = Global.Get302(M3u8Url, Headers);
                var newUri = MPDParser.Parse(DownDir, M3u8Url, m3u8Content, BaseUrl);
                M3u8Url = newUri;
                m3u8Content = File.ReadAllText(new Uri(M3u8Url).LocalPath);
            }

            if (m3u8Content.StartsWith("{\"payload\""))
            {
                var iqJsonPath = Path.Combine(DownDir, "iq.json");
                //输出mpd文件
                File.WriteAllText(iqJsonPath, m3u8Content);
                //分析json文件
                var newUri = IqJsonParser.Parse(DownDir, m3u8Content);
                M3u8Url = newUri;
                m3u8Content = File.ReadAllText(new Uri(M3u8Url).LocalPath);
            }

            //输出m3u8文件
            File.WriteAllText(m3u8SavePath, m3u8Content);

            //针对优酷#EXT-X-VERSION:7杜比视界片源修正
            if (m3u8Content.Contains("#EXT-X-DISCONTINUITY") && m3u8Content.Contains("#EXT-X-MAP") && m3u8Content.Contains("ott.cibntv.net") && m3u8Content.Contains("ccode="))
            {
                Regex ykmap = new Regex("#EXT-X-DISCONTINUITY\\s+#EXT-X-MAP:URI=\\\"(.*?)\\\",BYTERANGE=\\\"(.*?)\\\"");
                foreach (Match m in ykmap.Matches(m3u8Content))
                {
                    m3u8Content = m3u8Content.Replace(m.Value, $"#EXTINF:0.000000,\n#EXT-X-BYTERANGE:{m.Groups[2].Value}\n{m.Groups[1].Value}");
                }
            }

            //针对Disney+修正
            if (m3u8Content.Contains("#EXT-X-DISCONTINUITY") && m3u8Content.Contains("#EXT-X-MAP") && M3u8Url.Contains("media.dssott.com/"))
            {
                Regex ykmap = new Regex("#EXT-X-MAP:URI=\\\".*?BUMPER/[\\s\\S]+?#EXT-X-DISCONTINUITY");
                if (ykmap.IsMatch(m3u8Content))
                {
                    m3u8Content = m3u8Content.Replace(ykmap.Match(m3u8Content).Value, "#XXX");
                }
            }

            //针对AppleTv修正
            if (m3u8Content.Contains("#EXT-X-DISCONTINUITY") && m3u8Content.Contains("#EXT-X-MAP") && (M3u8Url.Contains(".apple.com/") || Regex.IsMatch(m3u8Content, "#EXT-X-MAP.*\\.apple\\.com/"))) 
            {
                //只取加密部分即可
                Regex ykmap = new Regex("(#EXT-X-KEY:[\\s\\S]*?)(#EXT-X-DISCONTINUITY|#EXT-X-ENDLIST)");
                if (ykmap.IsMatch(m3u8Content))
                {
                    m3u8Content = "#EXTM3U\r\n" + ykmap.Match(m3u8Content).Groups[1].Value + "\r\n#EXT-X-ENDLIST";
                }
            }

            //如果BaseUrl为空则截取字符串充当
            if (BaseUrl == "")
            {
                if (new Regex("#YUMING\\|(.*)").IsMatch(m3u8Content))
                    BaseUrl = new Regex("#YUMING\\|(.*)").Match(m3u8Content).Groups[1].Value;
                else
                    BaseUrl = GetBaseUrl(M3u8Url, Headers);
            }

            //if (!LiveStream)
            //{
            //    LOGGER.WriteLine(strings.parsingM3u8);
            //    LOGGER.PrintLine(strings.parsingM3u8);
            //}

            if (!string.IsNullOrEmpty(KeyBase64))
            {
                string line = "";
                if (string.IsNullOrEmpty(KeyIV))
                    line = $"#EXT-X-KEY:METHOD=AES-128,URI=\"base64:{KeyBase64}\"";
                else
                    line = $"#EXT-X-KEY:METHOD=AES-128,URI=\"base64:{KeyBase64}\",IV=0x{KeyIV.Replace("0x", "")}";
                m3u8CurrentKey = ParseKey(line);
            }
            if (!string.IsNullOrEmpty(KeyFile))
            {
                string line = "";
                Uri u = new Uri(KeyFile);
                if (string.IsNullOrEmpty(KeyIV))
                    line = $"#EXT-X-KEY:METHOD=AES-128,URI=\"{u.ToString()}\"";
                else
                    line = $"#EXT-X-KEY:METHOD=AES-128,URI=\"{u.ToString()}\",IV=0x{KeyIV.Replace("0x", "")}";

                m3u8CurrentKey = ParseKey(line);
            }

            //逐行分析
            using (StringReader sr = new StringReader(m3u8Content))
            {
                string line;
                double segDuration = 0;
                string segUrl = string.Empty;
                //#EXT-X-BYTERANGE:<n>[@<o>]
                long expectByte = -1; //parm n
                long startByte = 0;  //parm o

                while ((line = sr.ReadLine()) != null)
                {
                    if (string.IsNullOrEmpty(line))
                        continue;
                    if (line.StartsWith(HLSTags.ext_m3u))
                        isM3u = true;
                    //只下载部分字节
                    else if (line.StartsWith(HLSTags.ext_x_byterange))
                    {
                        string[] t = line.Replace(HLSTags.ext_x_byterange + ":", "").Split('@');
                        if (t.Length > 0)
                        {
                            if (t.Length == 1)
                            {
                                expectByte = Convert.ToInt64(t[0]);
                                segInfo.Add("expectByte", expectByte);
                            }
                            if (t.Length == 2)
                            {
                                expectByte = Convert.ToInt64(t[0]);
                                startByte = Convert.ToInt64(t[1]);
                                segInfo.Add("expectByte", expectByte);
                                segInfo.Add("startByte", startByte);
                            }
                        }
                        expectSegment = true;
                    }
                    //国家地理去广告
                    else if (line.StartsWith("#UPLYNK-SEGMENT"))
                    {
                        if (line.Contains(",ad"))
                            isAd = true;
                        else if (line.Contains(",segment"))
                            isAd = false;
                    }
                    //国家地理去广告
                    else if (isAd)
                    {
                        continue;
                    }
                    //解析定义的分段长度
                    else if (line.StartsWith(HLSTags.ext_x_targetduration))
                    {
                        targetDuration = Convert.ToInt32(Convert.ToDouble(line.Replace(HLSTags.ext_x_targetduration + ":", "").Trim()));
                    }
                    //解析起始编号
                    else if (line.StartsWith(HLSTags.ext_x_media_sequence))
                    {
                        segIndex = Convert.ToInt64(line.Replace(HLSTags.ext_x_media_sequence + ":", "").Trim());
                        startIndex = segIndex;
                    }
                    else if (line.StartsWith(HLSTags.ext_x_discontinuity_sequence)) ;
                    else if (line.StartsWith(HLSTags.ext_x_program_date_time))
                    {
                        if (string.IsNullOrEmpty(FFmpeg.REC_TIME))
                        {
                            FFmpeg.REC_TIME = line.Replace(HLSTags.ext_x_program_date_time + ":", "").Trim();
                        }
                    }
                    //解析不连续标记，需要单独合并（timestamp不同）
                    else if (line.StartsWith(HLSTags.ext_x_discontinuity))
                    {
                        //修复优酷去除广告后的遗留问题
                        if (hasAd && parts.Count > 0)
                        {
                            segments = (JArray)parts[parts.Count - 1];
                            parts.RemoveAt(parts.Count - 1);
                            hasAd = false;
                            continue;
                        }
                        //常规情况的#EXT-X-DISCONTINUITY标记，新建part
                        if (!hasAd && segments.Count > 1)
                        {
                            parts.Add(segments);
                            segments = new JArray();
                        }
                    }
                    else if (line.StartsWith(HLSTags.ext_x_cue_out)) ;
                    else if (line.StartsWith(HLSTags.ext_x_cue_out_start)) ;
                    else if (line.StartsWith(HLSTags.ext_x_cue_span)) ;
                    else if (line.StartsWith(HLSTags.ext_x_version)) ;
                    else if (line.StartsWith(HLSTags.ext_x_allow_cache)) ;
                    //解析KEY
                    else if (line.StartsWith(HLSTags.ext_x_key))
                    {
                        //自定义KEY情况 判断是否需要读取IV
                        if (!string.IsNullOrEmpty(KeyFile) || !string.IsNullOrEmpty(KeyBase64))
                        {
                            if (m3u8CurrentKey[2] == "" && line.Contains("IV=0x"))
                            {
                                var temp = Global.GetTagAttribute(line.Replace(HLSTags.ext_x_key + ":", ""), "IV");
                                m3u8CurrentKey[2] = temp; //使用m3u8中的IV
                            }
                        }
                        else
                        {
                            m3u8CurrentKey = ParseKey(line);
                            //存储为上一行的key信息
                            lastKeyLine = line;
                        }
                    }
                    //解析分片时长(暂时不考虑标题属性)
                    else if (line.StartsWith(HLSTags.extinf))
                    {
                        string[] tmp = line.Replace(HLSTags.extinf + ":", "").Split(',');
                        segDuration = Convert.ToDouble(tmp[0]);
                        segInfo.Add("index", segIndex);
                        segInfo.Add("method", m3u8CurrentKey[0]);
                        //是否有加密，有的话写入KEY和IV
                        if (m3u8CurrentKey[0] != "NONE")
                        {
                            segInfo.Add("key", m3u8CurrentKey[1]);
                            //没有读取到IV，自己生成
                            if (m3u8CurrentKey[2] == "")
                                segInfo.Add("iv", "0x" + Convert.ToString(segIndex, 16).PadLeft(32, '0'));
                            else
                                segInfo.Add("iv", m3u8CurrentKey[2]);
                        }
                        totalDuration += segDuration;
                        segInfo.Add("duration", segDuration);
                        expectSegment = true;
                        segIndex++;
                    }
                    //解析STREAM属性
                    else if (line.StartsWith(HLSTags.ext_x_stream_inf))
                    {
                        expectPlaylist = true;
                        string bandwidth = Global.GetTagAttribute(line, "BANDWIDTH");
                        string average_bandwidth = Global.GetTagAttribute(line, "AVERAGE-BANDWIDTH");
                        string codecs = Global.GetTagAttribute(line, "CODECS");
                        string resolution = Global.GetTagAttribute(line, "RESOLUTION");
                        string frame_rate = Global.GetTagAttribute(line, "FRAME-RATE");
                        string hdcp_level = Global.GetTagAttribute(line, "HDCP-LEVEL");
                        string audio = Global.GetTagAttribute(line, "AUDIO");
                        string video = Global.GetTagAttribute(line, "VIDEO");
                        string subtitles = Global.GetTagAttribute(line, "SUBTITLES");
                        string closed_captions = Global.GetTagAttribute(line, "CLOSED-CAPTIONS");
                        extList = new string[] { bandwidth, average_bandwidth, codecs, resolution,
                            frame_rate,hdcp_level,audio,video,subtitles,closed_captions };
                    }
                    else if (line.StartsWith(HLSTags.ext_x_i_frame_stream_inf)) ;
                    else if (line.StartsWith(HLSTags.ext_x_media))
                    {
                        var groupId = Global.GetTagAttribute(line, "GROUP-ID");
                        if (Global.GetTagAttribute(line, "TYPE") == "AUDIO")
                        {
                            var audio = new Audio();
                            audio.Channels = Global.GetTagAttribute(line, "CHANNELS");
                            audio.Language = Global.GetTagAttribute(line, "LANGUAGE");
                            audio.Name = Global.GetTagAttribute(line, "NAME");
                            audio.Uri = CombineURL(BaseUrl, Global.GetTagAttribute(line, "URI"));
                            if (!MEDIA_AUDIO_GROUP.ContainsKey(groupId))
                            {
                                MEDIA_AUDIO_GROUP.Add(groupId, new List<Audio>() { audio });
                            }
                            else
                            {
                                MEDIA_AUDIO_GROUP[groupId].Add(audio);
                            }
                        }
                        else if (Global.GetTagAttribute(line, "TYPE") == "SUBTITLES")
                        {
                            var sub = new Subtitle();
                            sub.Language = Global.GetTagAttribute(line, "LANGUAGE");
                            sub.Name = Global.GetTagAttribute(line, "NAME");
                            sub.Uri = CombineURL(BaseUrl, Global.GetTagAttribute(line, "URI"));
                            if (!MEDIA_SUB_GROUP.ContainsKey(groupId))
                            {
                                MEDIA_SUB_GROUP.Add(groupId, new List<Subtitle>() { sub });
                            }
                            else
                            {
                                MEDIA_SUB_GROUP[groupId].Add(sub);
                            }
                        }
                    }
                    else if (line.StartsWith(HLSTags.ext_x_playlist_type)) ;
                    else if (line.StartsWith(HLSTags.ext_i_frames_only))
                    {
                        isIFramesOnly = true;
                    }
                    else if (line.StartsWith(HLSTags.ext_is_independent_segments))
                    {
                        isIndependentSegments = true;
                    }
                    //m3u8主体结束
                    else if (line.StartsWith(HLSTags.ext_x_endlist))
                    {
                        if (segments.Count > 0)
                            parts.Add(segments);
                        segments = new JArray();
                        isEndlist = true;
                    }
                    //#EXT-X-MAP
                    else if (line.StartsWith(HLSTags.ext_x_map))
                    {
                        if (extMAP[0] == "")
                        {
                            extMAP[0] = Global.GetTagAttribute(line, "URI");
                            if (line.Contains("BYTERANGE"))
                                extMAP[1] = Global.GetTagAttribute(line, "BYTERANGE");
                            if (!extMAP[0].StartsWith("http")) extMAP[0] = CombineURL(BaseUrl, extMAP[0]);
                        }
                        //遇到了其他的map，说明已经不是一个视频了，全部丢弃即可
                        else
                        {
                            if (segments.Count > 0)
                                parts.Add(segments);
                            segments = new JArray();
                            isEndlist = true;
                            break;
                        }
                    }
                    else if (line.StartsWith(HLSTags.ext_x_start)) ;
                    //评论行不解析
                    else if (line.StartsWith("#")) continue;
                    //空白行不解析
                    else if (line.StartsWith("\r\n")) continue;
                    //解析分片的地址
                    else if (expectSegment)
                    {
                        segUrl = CombineURL(BaseUrl, line);
                        if (M3u8Url.Contains("?__gda__"))
                        {
                            segUrl += new Regex("\\?__gda__.*").Match(M3u8Url).Value;
                        }
                        if (M3u8Url.Contains("//dlsc.hcs.cmvideo.cn") && (segUrl.EndsWith(".ts") || segUrl.EndsWith(".mp4")))
                        {
                            segUrl += new Regex("\\?.*").Match(M3u8Url).Value;
                        }
                        segInfo.Add("segUri", segUrl);
                        segments.Add(segInfo);
                        segInfo = new JObject();
                        //优酷的广告分段则清除此分片
                        //需要注意，遇到广告说明程序对上文的#EXT-X-DISCONTINUITY做出的动作是不必要的，
                        //其实上下文是同一种编码，需要恢复到原先的part上
                        if (DelAd && segUrl.Contains("ccode=") && segUrl.Contains("/ad/") && segUrl.Contains("duration="))
                        {
                            segments.RemoveAt(segments.Count - 1);
                            segIndex--;
                            hasAd = true;
                        }
                        //优酷广告(4K分辨率测试)
                        if (DelAd && segUrl.Contains("ccode=0902") && segUrl.Contains("duration="))
                        {
                            segments.RemoveAt(segments.Count - 1);
                            segIndex--;
                            hasAd = true;
                        }
                        expectSegment = false;
                    }
                    //解析STREAM属性的URI
                    else if (expectPlaylist)
                    {
                        string listUrl;
                        listUrl = CombineURL(BaseUrl, line);
                        if (M3u8Url.Contains("?__gda__"))
                        {
                            listUrl += new Regex("\\?__gda__.*").Match(M3u8Url).Value;
                        }
                        StringBuilder sb = new StringBuilder();
                        sb.Append("{");
                        sb.Append("\"URL\":\"" + listUrl + "\",");
                        for (int i = 0; i < 10; i++)
                        {
                            if (extList[i] != "")
                            {
                                switch (i)
                                {
                                    case 0:
                                        sb.Append("\"BANDWIDTH\":\"" + extList[i] + "\",");
                                        break;
                                    case 1:
                                        sb.Append("\"AVERAGE-BANDWIDTH\":\"" + extList[i] + "\",");
                                        break;
                                    case 2:
                                        sb.Append("\"CODECS\":\"" + extList[i] + "\",");
                                        break;
                                    case 3:
                                        sb.Append("\"RESOLUTION\":\"" + extList[i] + "\",");
                                        break;
                                    case 4:
                                        sb.Append("\"FRAME-RATE\":\"" + extList[i] + "\",");
                                        break;
                                    case 5:
                                        sb.Append("\"HDCP-LEVEL\":\"" + extList[i] + "\",");
                                        break;
                                    case 6:
                                        sb.Append("\"AUDIO\":\"" + extList[i] + "\",");
                                        break;
                                    case 7:
                                        sb.Append("\"VIDEO\":\"" + extList[i] + "\",");
                                        break;
                                    case 8:
                                        sb.Append("\"SUBTITLES\":\"" + extList[i] + "\",");
                                        break;
                                    case 9:
                                        sb.Append("\"CLOSED-CAPTIONS\":\"" + extList[i] + "\",");
                                        break;
                                }
                            }
                        }
                        sb.Append("}");
                        extLists.Add(sb.ToString().Replace(",}", "}"));
                        if (Convert.ToInt64(extList[0]) >= bestBandwidth)
                        {
                            bestBandwidth = Convert.ToInt64(extList[0]);
                            bestUrl = listUrl;
                            bestUrlAudio = extList[6];
                            bestUrlSub = extList[8];
                        }
                        extList = new string[8];
                        expectPlaylist = false;
                    }
                }
            }

            if (isM3u == false)
            {
                LOGGER.WriteLineError(strings.invalidM3u8);
                LOGGER.PrintLine(strings.invalidM3u8, LOGGER.Error);
                return;
            }

            //直播的情况，无法遇到m3u8结束标记，需要手动将segments加入parts
            if (parts.HasValues == false)
                parts.Add(segments);

            //处理外挂音轨的AudioOnly逻辑
            if (audioUrl != "" && Global.VIDEO_TYPE == "IGNORE")
            {
                LOGGER.WriteLine(strings.startParsing + audioUrl);
                LOGGER.WriteLine(strings.downloadingExternalAudioTrack);
                LOGGER.PrintLine(strings.downloadingExternalAudioTrack, LOGGER.Warning);
                try
                {
                    DirectoryInfo directoryInfo = new DirectoryInfo(DownDir);
                    directoryInfo.Delete(true);
                }
                catch (Exception) { }
                M3u8Url = audioUrl;
                BaseUrl = "";
                audioUrl = "";
                bestUrlAudio = "";
                Parse();
                return;
            }

            //构造JSON文件
            JObject jsonResult = new JObject();
            jsonResult.Add("m3u8", M3u8Url);
            jsonResult.Add("m3u8BaseUri", BaseUrl);
            jsonResult.Add("updateTime", DateTime.Now.ToString("o"));
            JObject jsonM3u8Info = new JObject();
            jsonM3u8Info.Add("originalCount", segIndex - startIndex);
            jsonM3u8Info.Add("count", segIndex - startIndex);
            jsonM3u8Info.Add("vod", isEndlist);
            jsonM3u8Info.Add("targetDuration", targetDuration);
            jsonM3u8Info.Add("totalDuration", totalDuration);
            if (bestUrlAudio != "" && MEDIA_AUDIO_GROUP.ContainsKey(bestUrlAudio))
            {
                if (MEDIA_AUDIO_GROUP[bestUrlAudio].Count == 1)
                {
                    audioUrl = MEDIA_AUDIO_GROUP[bestUrlAudio][0].Uri;
                }
                //多种音频语言 让用户选择
                else
                {
                    var startCursorIndex = Console.CursorTop;
                    var cursorIndex = startCursorIndex;
                    LOGGER.PrintLine("Found Multiple Language Audio Tracks.", LOGGER.Warning);
                    for (int i = 0; i < MEDIA_AUDIO_GROUP[bestUrlAudio].Count; i++)
                    {
                        Console.WriteLine("".PadRight(13) + $"[{i.ToString().PadLeft(2)}]. {bestUrlAudio} => {MEDIA_AUDIO_GROUP[bestUrlAudio][i]}");
                        cursorIndex++;
                    }
                    LOGGER.PrintLine("Please Select What You Want.(Up To 1 Track)");
                    Console.Write("".PadRight(13) + "Enter Number: ");
                    var input = Console.ReadLine();
                    cursorIndex += 2;
                    for (int i = startCursorIndex; i < cursorIndex; i++)
                    {
                        Console.SetCursorPosition(0, i);
                        Console.Write("".PadRight(300));
                    }
                    Console.SetCursorPosition(0, startCursorIndex);
                    audioUrl = MEDIA_AUDIO_GROUP[bestUrlAudio][int.Parse(input)].Uri;
                }
            }
            if (bestUrlSub != "" && MEDIA_SUB_GROUP.ContainsKey(bestUrlSub))
            {
                if (MEDIA_SUB_GROUP[bestUrlSub].Count == 1)
                {
                    subUrl = MEDIA_SUB_GROUP[bestUrlSub][0].Uri;
                }
                //多种字幕语言 让用户选择
                else
                {
                    var startCursorIndex = Console.CursorTop;
                    var cursorIndex = startCursorIndex;
                    LOGGER.PrintLine("Found Multiple Language Subtitle Tracks.", LOGGER.Warning);
                    for (int i = 0; i < MEDIA_SUB_GROUP[bestUrlSub].Count; i++)
                    {
                        Console.WriteLine("".PadRight(13) + $"[{i.ToString().PadLeft(2)}]. {bestUrlSub} => {MEDIA_SUB_GROUP[bestUrlSub][i]}");
                        cursorIndex++;
                    }
                    LOGGER.PrintLine("Please Select What You Want.(Up To 1 Track)");
                    Console.Write("".PadRight(13) + "Enter Number: ");
                    var input = Console.ReadLine();
                    cursorIndex += 2;
                    for (int i = startCursorIndex; i < cursorIndex; i++)
                    {
                        Console.SetCursorPosition(0, i);
                        Console.Write("".PadRight(300));
                    }
                    Console.SetCursorPosition(0, startCursorIndex);
                    subUrl = MEDIA_SUB_GROUP[bestUrlSub][int.Parse(input)].Uri;
                }
            }
            if (audioUrl != "")
                jsonM3u8Info.Add("audio", audioUrl);
            if (subUrl != "")
                jsonM3u8Info.Add("sub", subUrl);
            if (extMAP[0] != "")
            {
                DownloadManager.HasExtMap = true;
                if (extMAP[1] == "")
                    jsonM3u8Info.Add("extMAP", extMAP[0]);
                else
                    jsonM3u8Info.Add("extMAP", extMAP[0] + "|" + extMAP[1]);
            }
            else
            {
                DownloadManager.HasExtMap = false;
            }

            //根据DurRange来生成分片Range
            if (DurStart != "" || DurEnd != "")
            {
                double secStart = 0;
                double secEnd = -1;

                if (DurEnd == "")
                {
                    secEnd = totalDuration;
                }

                //时间码
                Regex reg2 = new Regex(@"(\d+):(\d+):(\d+)");
                if (reg2.IsMatch(DurStart))
                {
                    int HH = Convert.ToInt32(reg2.Match(DurStart).Groups[1].Value);
                    int MM = Convert.ToInt32(reg2.Match(DurStart).Groups[2].Value);
                    int SS = Convert.ToInt32(reg2.Match(DurStart).Groups[3].Value);
                    secStart = SS + MM * 60 + HH * 60 * 60;
                }
                if (reg2.IsMatch(DurEnd))
                {
                    int HH = Convert.ToInt32(reg2.Match(DurEnd).Groups[1].Value);
                    int MM = Convert.ToInt32(reg2.Match(DurEnd).Groups[2].Value);
                    int SS = Convert.ToInt32(reg2.Match(DurEnd).Groups[3].Value);
                    secEnd = SS + MM * 60 + HH * 60 * 60;
                }

                bool flag1 = false;
                bool flag2 = false;
                if (secEnd - secStart > 0)
                {
                    double dur = 0; //当前时间
                    foreach (JArray part in parts)
                    {
                        foreach (var seg in part)
                        {
                            dur += Convert.ToDouble(seg["duration"].ToString());
                            if (flag1 == false && dur > secStart)
                            {
                                RangeStart = seg["index"].Value<int>();
                                flag1 = true;
                            }

                            if (flag2 == false && dur >= secEnd)
                            {
                                RangeEnd = seg["index"].Value<int>();
                                flag2 = true;
                            }
                        }
                    }
                }
            }


            //根据Range来清除部分分片
            if (RangeStart != 0 || RangeEnd != -1)
            {
                if (RangeEnd == -1)
                    RangeEnd = (int)(segIndex - startIndex - 1);
                int newCount = 0;
                double newTotalDuration = 0;
                JArray newParts = new JArray();
                foreach (JArray part in parts)
                {
                    JArray newPart = new JArray();
                    foreach (var seg in part)
                    {
                        if (RangeStart <= seg["index"].Value<int>() && seg["index"].Value<int>() <= RangeEnd)
                        {
                            newPart.Add(seg);
                            newCount++;
                            newTotalDuration += Convert.ToDouble(seg["duration"].ToString());
                        }
                    }
                    if (newPart.Count != 0)
                        newParts.Add(newPart);
                }
                parts = newParts;
                jsonM3u8Info["count"] = newCount;
                jsonM3u8Info["totalDuration"] = newTotalDuration;
            }


            //添加
            jsonM3u8Info.Add("segments", parts);
            jsonResult.Add("m3u8Info", jsonM3u8Info);


            //输出JSON文件
            if (!LiveStream)
            {
                LOGGER.WriteLine(strings.wrtingMeta);
                LOGGER.PrintLine(strings.wrtingMeta);
            }
            File.WriteAllText(jsonSavePath, jsonResult.ToString());
            //检测是否为master list
            MasterListCheck();
        }

        bool downloadingM3u8KeyTip = false;
        public string[] ParseKey(string line)
        {
            if (!downloadingM3u8KeyTip)
            {
                LOGGER.PrintLine(strings.downloadingM3u8Key, LOGGER.Warning);
                downloadingM3u8KeyTip = true;
            }
            string[] tmp = line.Replace(HLSTags.ext_x_key + ":", "").Split(',');
            string[] key = new string[] { "NONE", "", "" };
            string u_l = Global.GetTagAttribute(lastKeyLine.Replace(HLSTags.ext_x_key + ":", ""), "URI");
            string m = Global.GetTagAttribute(line.Replace(HLSTags.ext_x_key + ":", ""), "METHOD");
            string u = Global.GetTagAttribute(line.Replace(HLSTags.ext_x_key + ":", ""), "URI");
            string i = Global.GetTagAttribute(line.Replace(HLSTags.ext_x_key + ":", ""), "IV");

            //存在加密
            if (m != "")
            {
                if (m != "AES-128")
                {
                    LOGGER.PrintLine(string.Format(strings.notSupportMethod, m), LOGGER.Error);
                    DownloadManager.BinaryMerge = true;
                    return new string[] { $"{m}(NOTSUPPORTED)", "", "" };
                }
                //METHOD
                key[0] = m;
                //URI
                key[1] = u;
                if (u_l == u)
                {
                    key[1] = m3u8CurrentKey[1];
                }
                else
                {
                    LOGGER.WriteLine(strings.downloadingM3u8Key + " " + key[1]);
                    if (key[1].StartsWith("http"))
                    {
                        string keyUrl = key[1];
                        if (key[1].Contains("imooc.com/"))
                        {
                            key[1] = DecodeImooc.DecodeKey(Global.GetWebSource(key[1], Headers));
                        }
                        else if (key[1] == "https://hls.ventunotech.com/m3u8/pc_videosecurevtnkey.key")
                        {
                            string temp = Global.GetWebSource(keyUrl, Headers);
                            LOGGER.PrintLine(temp);
                            byte[] tempKey = new byte[16];
                            for (int d = 0; d < 16; d++)
                            {
                                tempKey[d] = Convert.ToByte(temp.Substring(2 * d, 2), 16);
                            }
                            key[1] = Convert.ToBase64String(tempKey);
                        }
                        else if (key[1].Contains("drm.vod2.myqcloud.com/getlicense"))
                        {
                            var temp = Global.HttpDownloadFileToBytes(keyUrl, Headers);
                            key[1] = DecodeHuke88Key.DecodeKey(key[1], temp);
                        }
                        else
                        {
                            if (keyUrl.Contains("https://keydeliver.linetv.tw/jurassicPark"))  //linetv
                                keyUrl = keyUrl + "?time=" + Global.GetTimeStamp(false);
                            key[1] = Convert.ToBase64String(Global.HttpDownloadFileToBytes(keyUrl, Headers));
                        }
                    }
                    //DMM网站
                    else if (key[1].StartsWith("base64:"))
                    {
                        key[1] = key[1].Replace("base64:", "");
                    }
                    else
                    {
                        string keyUrl = CombineURL(BaseUrl, key[1]);
                        if (keyUrl.Contains("edu.51cto.com")) //51cto
                        {
                            string lessonId = Global.GetQueryString("lesson_id", keyUrl);
                            keyUrl = keyUrl + "&sign=" + Decode51CtoKey.GetSign(lessonId);
                            var encodeKey = Encoding.UTF8.GetString(Global.HttpDownloadFileToBytes(keyUrl, Headers));
                            key[1] = Decode51CtoKey.GetDecodeKey(encodeKey, lessonId);
                        }
                        else
                        {
                            key[1] = Convert.ToBase64String(Global.HttpDownloadFileToBytes(keyUrl, Headers));
                        }
                    }
                }
                //IV
                key[2] = i;
            }

            return key;
        }

        public void MasterListCheck()
        {
            //若存在多个清晰度条目，输出另一个json文件存放
            if (extLists.Count != 0)
            {
                File.Copy(m3u8SavePath, Path.GetDirectoryName(m3u8SavePath) + "\\master.m3u8", true);
                LOGGER.WriteLine("Master List Found");
                LOGGER.PrintLine(strings.masterListFound, LOGGER.Warning);
                var json = new JObject();
                json.Add("masterUri", M3u8Url);
                json.Add("updateTime", DateTime.Now.ToString("o"));
                json.Add("playLists", JArray.Parse("[" + string.Join(",", extLists.ToArray()) + "]"));
                if (MEDIA_AUDIO_GROUP.Keys.Count > 0)
                {
                    var audioGroup = JObject.FromObject(MEDIA_AUDIO_GROUP);
                    json.Add("audioTracks", audioGroup);
                }
                if (MEDIA_SUB_GROUP.Keys.Count > 0)
                {
                    var subGroup = JObject.FromObject(MEDIA_SUB_GROUP);
                    json.Add("subtitleTracks", subGroup);
                }
                //输出json文件
                LOGGER.WriteLine(strings.wrtingMasterMeta);
                LOGGER.PrintLine(strings.wrtingMasterMeta);
                File.WriteAllText(Path.GetDirectoryName(jsonSavePath) + "\\playLists.json", json.ToString());
                LOGGER.WriteLine(strings.selectPlaylist + ": " + bestUrl);
                LOGGER.PrintLine(strings.selectPlaylist);
                LOGGER.WriteLine(strings.startReParsing);
                LOGGER.PrintLine(strings.startReParsing, LOGGER.Warning);
                //重置Baseurl并重新解析
                M3u8Url = bestUrl;
                BaseUrl = "";
                Parse();
            }
        }

        //解决低版本.Net框架的一个BUG（XP上百分之百复现）
        //https://stackoverflow.com/questions/781205/getting-a-url-with-an-url-encoded-slash#
        private void ForceCanonicalPathAndQuery(Uri uri)
        {
            string paq = uri.PathAndQuery; // need to access PathAndQuery
            FieldInfo flagsFieldInfo = typeof(Uri).GetField("m_Flags", BindingFlags.Instance | BindingFlags.NonPublic);
            ulong flags = (ulong)flagsFieldInfo.GetValue(uri);
            flags &= ~((ulong)0x30); // Flags.PathNotCanonical|Flags.QueryNotCanonical
            flagsFieldInfo.SetValue(uri, flags);
        }

        /// <summary>
        /// 拼接Baseurl和RelativeUrl
        /// </summary>
        /// <param name="baseurl">Baseurl</param>
        /// <param name="url">RelativeUrl</param>
        /// <returns></returns>
        public string CombineURL(string baseurl, string url)
        {
            /*
            //本地文件形式
            if (File.Exists(Path.Combine(baseurl, url)))
            {
                return Path.Combine(baseurl, url);
            }*/


            Uri uri1 = new Uri(baseurl);  //这里直接传完整的URL即可
            Uri uri2 = new Uri(uri1, url);  
            ForceCanonicalPathAndQuery(uri2);  //兼容XP的低版本.Net
            url = uri2.ToString();


            /*
            if (!url.StartsWith("http")) 
            {
                if (url.StartsWith("/"))
                {
                    if (!url.Contains(":"))  // => /livelvy:livelvy/lvy1/WysAABmKyDctEW8V-13959.ts?n=vdn-gdzh-tel-1-6
                        url = baseurl.Substring(0, baseurl.Length - 1) + url;
                    else
                        url = baseurl.Substring(0, baseurl.Length - 1) + url.Substring(url.IndexOf(':'));
                }
                else
                    url = baseurl + url;
            }*/

            return url;
        }
        
        /// <summary>
        /// 从url中截取字符串充当baseurl
        /// </summary>
        /// <param name="m3u8url"></param>
        /// <returns></returns>
        public static string GetBaseUrl(string m3u8url, string headers)
        {
            string url = Global.Get302(m3u8url, headers);
            if (url.Contains("?"))
                url = url.Remove(url.LastIndexOf('?'));
            url = url.Substring(0, url.LastIndexOf('/') + 1);
            return url;
        }
    }
}
