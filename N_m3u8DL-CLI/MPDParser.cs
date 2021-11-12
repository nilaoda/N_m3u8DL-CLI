using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;

namespace N_m3u8DL_CLI
{
    //code from https://github.com/ytdl-org/youtube-dl/blob/master/youtube_dl/extractor/common.py#L2076
    class MPDParser
    {
        static Dictionary<string, dynamic> ExtractMultisegmentInfo(XmlElement Period, XmlNamespaceManager nsMgr, Dictionary<string, dynamic> info)
        {
            var MultisegmentInfo = new Dictionary<string, dynamic>(info);
            void ExtractCommon(XmlNode source)
            {
                var sourceE = (XmlElement)source;
                var segmentTimeline = source.SelectSingleNode("ns:SegmentTimeline", nsMgr);
                if (segmentTimeline != null)
                {
                    var sE = segmentTimeline.SelectNodes("ns:S", nsMgr);
                    if (sE.Count > 0)
                    {
                        MultisegmentInfo["TotalNumber"] = 0;
                        var SList = new List<Dictionary<string, dynamic>>();
                        foreach (XmlElement s in sE)
                        {
                            var r = string.IsNullOrEmpty(s.GetAttribute("r")) ? 0 : Convert.ToInt64(s.GetAttribute("r"));
                            MultisegmentInfo["TotalNumber"] += 1 + r;
                            SList.Add(new Dictionary<string, dynamic>()
                            {
                                ["t"] = string.IsNullOrEmpty(s.GetAttribute("t")) ? 0 : Convert.ToInt64(s.GetAttribute("t")),
                                ["d"] = Convert.ToInt64(s.GetAttribute("d")),
                                ["r"] = r
                            });
                        }
                        MultisegmentInfo.Add("S", SList);
                    }
                }
                var startNumber = sourceE.GetAttribute("startNumber");
                if (!string.IsNullOrEmpty(startNumber))
                {
                    MultisegmentInfo["StartNumber"] = Convert.ToInt32(startNumber);
                }
                var timescale = sourceE.GetAttribute("timescale");
                if (!string.IsNullOrEmpty(timescale))
                {
                    MultisegmentInfo["Timescale"] = Convert.ToInt32(timescale);
                }
                var segmentDuration = sourceE.GetAttribute("duration");
                if (!string.IsNullOrEmpty(segmentDuration))
                {
                    MultisegmentInfo["SegmentDuration"] = Convert.ToDouble(segmentDuration);
                }
            }

            void ExtractInitialization(XmlNode source)
            {
                var initialization = source.SelectSingleNode("ns:Initialization", nsMgr);
                if (initialization != null)
                {
                    MultisegmentInfo["InitializationUrl"] = ((XmlElement)initialization).GetAttribute("sourceURL");
                    if (((XmlElement)initialization).HasAttribute("range"))
                    {
                        MultisegmentInfo["InitializationUrl"] += "$$Range=" + ((XmlElement)initialization).GetAttribute("range");
                    }
                }
            }

            var segmentList = Period.SelectSingleNode("ns:SegmentList", nsMgr);
            if (segmentList != null)
            {
                ExtractCommon(segmentList);
                ExtractInitialization(segmentList);
                var segmentUrlsE = segmentList.SelectNodes("ns:SegmentURL", nsMgr);
                MultisegmentInfo["SegmentUrls"] = new List<string>();
                foreach (XmlElement segment in segmentUrlsE)
                {
                    if (segment.HasAttribute("mediaRange"))
                    {
                        MultisegmentInfo["SegmentUrls"].Add("$$Range=" + segment.GetAttribute("mediaRange"));
                    }
                    else
                    {
                        MultisegmentInfo["SegmentUrls"].Add(segment.GetAttribute("media"));
                    }
                }
            }
            else
            {
                var segmentTemplate = Period.SelectSingleNode("ns:SegmentTemplate", nsMgr);
                if (segmentTemplate != null)
                {
                    ExtractCommon(segmentTemplate);
                    var media = ((XmlElement)segmentTemplate).GetAttribute("media");
                    if (!string.IsNullOrEmpty(media))
                    {
                        MultisegmentInfo["Media"] = media;
                    }
                    var initialization = ((XmlElement)segmentTemplate).GetAttribute("initialization");
                    if (!string.IsNullOrEmpty(initialization))
                    {
                        MultisegmentInfo["Initialization"] = initialization;
                    }
                    else
                    {
                        ExtractInitialization(segmentTemplate);
                    }
                }
            }

            return MultisegmentInfo;
        }

        /// <summary>
        /// 返回生成的master文件地址
        /// </summary>
        /// <param name="downDir">文件存储目录</param>
        /// <param name="mpdUrl">MPD链接</param>
        /// <param name="mpdContent">MPD内容</param>
        /// <param name="defaultBase">BaseUrl</param>
        /// <returns></returns>
        public static string Parse(string downDir, string mpdUrl, string mpdContent, string defaultBase = "")
        {
            //XiGua
            if (mpdContent.Contains("<mas:") && !mpdContent.Contains("xmlns:mas"))
                mpdContent = mpdContent.Replace("<MPD ", "<MPD xmlns:mas=\"urn:marlin:mas:1-0:services:schemas:mpd\" ");

            XmlDocument mpdDoc = new XmlDocument();
            mpdDoc.LoadXml(mpdContent);

            XmlNode xn = null;
            //Select MPD node
            foreach (XmlNode node in mpdDoc.ChildNodes)
            {
                if (node.NodeType == XmlNodeType.Element && node.Name == "MPD")
                {
                    xn = node;
                    break;
                }
            }
            var mediaPresentationDuration = ((XmlElement)xn).GetAttribute("mediaPresentationDuration");
            var ns = ((XmlElement)xn).GetAttribute("xmlns");

            XmlNamespaceManager nsMgr = new XmlNamespaceManager(mpdDoc.NameTable);
            nsMgr.AddNamespace("ns", ns);

            TimeSpan ts = XmlConvert.ToTimeSpan(mediaPresentationDuration); //时长

            var formatList = new List<Dictionary<string, dynamic>>(); //存放所有音视频清晰度
            var periodIndex = 0; //解决同一个period且同id导致被重复添加分片

            foreach (XmlElement period in xn.SelectNodes("ns:Period", nsMgr))
            {
                periodIndex++;
                var periodDuration = string.IsNullOrEmpty(period.GetAttribute("duration")) ? XmlConvert.ToTimeSpan(mediaPresentationDuration) : XmlConvert.ToTimeSpan(period.GetAttribute("duration"));
                var periodMsInfo = ExtractMultisegmentInfo(period, nsMgr, new Dictionary<string, dynamic>()
                {
                    ["StartNumber"] = 1,
                    ["Timescale"] = 1
                });
                foreach (XmlElement adaptationSet in period.SelectNodes("ns:AdaptationSet", nsMgr))
                {
                    var adaptionSetMsInfo = ExtractMultisegmentInfo(adaptationSet, nsMgr, periodMsInfo);
                    foreach (XmlElement representation in adaptationSet.SelectNodes("ns:Representation", nsMgr))
                    {
                        string GetAttribute(string key)
                        {
                            var v1 = representation.GetAttribute(key);
                            if (string.IsNullOrEmpty(v1))
                                return adaptationSet.GetAttribute(key);
                            return v1;
                        }

                        var mimeType = GetAttribute("mimeType");
                        var contentType = mimeType.Split('/')[0];
                        if (contentType == "text")
                        {
                            continue; //暂不支持字幕下载
                        }
                        else if (contentType == "video" || contentType == "audio")
                        {
                            var baseUrl = "";
                            bool CheckBaseUrl()
                            {
                                return Regex.IsMatch(baseUrl, @"^https?://");
                            }

                            var list = new List<XmlNodeList>()
                            {
                                representation.ChildNodes,
                                adaptationSet.ChildNodes,
                                period.ChildNodes,
                                mpdDoc.ChildNodes
                            };

                            foreach (XmlNodeList xmlNodeList in list)
                            {
                                foreach (XmlNode node in xmlNodeList)
                                {
                                    if (node.Name == "BaseURL")
                                    {
                                        baseUrl = node.InnerText + baseUrl;
                                        if (CheckBaseUrl()) break;
                                    }
                                }
                                if (CheckBaseUrl()) break;
                            }

                            string GetBaseUrl(string url)
                            {
                                if (url.Contains("?"))
                                    url = url.Remove(url.LastIndexOf('?'));
                                url = url.Substring(0, url.LastIndexOf('/') + 1);
                                return url;
                            }

                            var mpdBaseUrl = string.IsNullOrEmpty(defaultBase) ? GetBaseUrl(mpdUrl) : defaultBase;
                            if (!string.IsNullOrEmpty(mpdBaseUrl) && !CheckBaseUrl())
                            {
                                if (!mpdBaseUrl.EndsWith("/") && !baseUrl.StartsWith("/"))
                                {
                                    mpdBaseUrl += "/";
                                }
                                baseUrl = CombineURL(mpdBaseUrl, baseUrl);
                            }
                            var representationId = GetAttribute("id");
                            var lang = GetAttribute("lang");
                            var bandwidth = IntOrNull(GetAttribute("bandwidth"));
                            var f = new Dictionary<string, dynamic>
                            {
                                ["PeriodIndex"] = periodIndex,
                                ["ContentType"] = contentType,
                                ["FormatId"] = representationId,
                                ["ManifestUrl"] = mpdUrl,
                                ["Width"] = IntOrNull(GetAttribute("width")),
                                ["Height"] = IntOrNull(GetAttribute("height")),
                                ["Tbr"] = DoubleOrNull(bandwidth, 1000),
                                ["Asr"] = IntOrNull(GetAttribute("audioSamplingRate")),
                                ["Fps"] = IntOrNull(GetAttribute("frameRate")),
                                ["Language"] = lang,
                                ["Codecs"] = GetAttribute("codecs")
                            };

                            var representationMsInfo = ExtractMultisegmentInfo(representation, nsMgr, adaptionSetMsInfo);

                            string PrepareTemplate(string templateName, string[] identifiers)
                            {
                                var tmpl = representationMsInfo[templateName];
                                var t = new StringBuilder();
                                var inTemplate = false;
                                foreach (var ch in tmpl)
                                {
                                    t.Append(ch);
                                    if (ch == '$')
                                    {
                                        inTemplate = !inTemplate;
                                    }
                                    else if (ch == '%' && !inTemplate)
                                    {
                                        t.Append(ch);
                                    }
                                }
                                var str = t.ToString();
                                str = str.Replace("$RepresentationID$", representationId);
                                str = Regex.Replace(str, "\\$(" + string.Join("|", identifiers) + ")\\$", "{{$1}}");
                                str = Regex.Replace(str, "\\$(" + string.Join("|", identifiers) + ")%([^$]+)d\\$", "{{$1}}{0:D$2}");
                                str = str.Replace("$$", "$");
                                return str;
                            }

                            string PadNumber(string template, string key, long value)
                            {
                                string ReplaceFirst(string text, string search, string replace)
                                {
                                    int pos = text.IndexOf(search);
                                    if (pos < 0)
                                    {
                                        return text;
                                    }
                                    return text.Substring(0, pos) + replace + text.Substring(pos + search.Length);
                                }

                                template = template.Replace("{{" + key + "}}", "");
                                var m = Regex.Match(template, "{0:D(\\d+)}");
                                return ReplaceFirst(template, m.Value, value.ToString("0".PadRight(Convert.ToInt32(m.Groups[1].Value), '0')));
                            }

                            if (representationMsInfo.ContainsKey("Initialization"))
                            {
                                var initializationTemplate = PrepareTemplate("Initialization", new string[] { "Bandwidth" });
                                var initializationUrl = "";
                                if (initializationTemplate.Contains("{0:D"))
                                {
                                    if (initializationTemplate.Contains("{{Bandwidth}}"))
                                        initializationUrl = PadNumber(initializationTemplate, "Bandwidth", bandwidth);
                                }
                                else
                                {
                                    initializationUrl = initializationTemplate.Replace("{{Bandwidth}}", bandwidth.ToString());
                                }
                                representationMsInfo["InitializationUrl"] = CombineURL(baseUrl, initializationUrl);
                            }

                            string LocationKey(string location)
                            {
                                return Regex.IsMatch(location, "^https?://") ? "url" : "path";
                            }

                            if (!representationMsInfo.ContainsKey("SegmentUrls") && representationMsInfo.ContainsKey("Media"))
                            {
                                var mediaTemplate = PrepareTemplate("Media", new string[] { "Number", "Bandwidth", "Time" });
                                var mediaLocationKey = LocationKey(mediaTemplate);

                                if (mediaTemplate.Contains("{{Number") && !representationMsInfo.ContainsKey("S"))
                                {
                                    var segmentDuration = 0.0;
                                    if (!representationMsInfo.ContainsKey("TotalNumber") && representationMsInfo.ContainsKey("SegmentDuration"))
                                    {
                                        segmentDuration = DoubleOrNull(representationMsInfo["SegmentDuration"], representationMsInfo["Timescale"]);
                                        representationMsInfo["TotalNumber"] = (int)Math.Ceiling(periodDuration.TotalSeconds / segmentDuration);
                                    }
                                    var fragments = new List<Dictionary<string, dynamic>>();
                                    for (int i = representationMsInfo["StartNumber"]; i < representationMsInfo["StartNumber"] + representationMsInfo["TotalNumber"]; i++)
                                    {
                                        var segUrl = "";
                                        if (mediaTemplate.Contains("{0:D"))
                                        {
                                            if (mediaTemplate.Contains("{{Bandwidth}}"))
                                                segUrl = PadNumber(mediaTemplate, "Bandwidth", bandwidth);
                                            if (mediaTemplate.Contains("{{Number}}"))
                                                segUrl = PadNumber(mediaTemplate, "Number", i);
                                        }
                                        else
                                        {
                                            segUrl = mediaTemplate.Replace("{{Bandwidth}}", bandwidth.ToString());
                                            segUrl = segUrl.Replace("{{Number}}", i.ToString());
                                        }
                                        fragments.Add(new Dictionary<string, dynamic>()
                                        {
                                            [mediaLocationKey] = CombineURL(baseUrl, segUrl),
                                            ["duration"] = segmentDuration
                                        });
                                    }
                                    representationMsInfo["Fragments"] = fragments;
                                }
                                else
                                {
                                    var fragments = new List<Dictionary<string, dynamic>>();

                                    var segmentTime = 0L;
                                    var segmentD = 0L;
                                    var segmentNumber = representationMsInfo["StartNumber"];

                                    void addSegmentUrl()
                                    {
                                        var segUrl = "";
                                        if (mediaTemplate.Contains("{0:D"))
                                        {
                                            if (mediaTemplate.Contains("{{Bandwidth}}"))
                                                segUrl = PadNumber(mediaTemplate, "Bandwidth", bandwidth);
                                            if (mediaTemplate.Contains("{{Number}}"))
                                                segUrl = PadNumber(mediaTemplate, "Number", segmentNumber);
                                            if (mediaTemplate.Contains("{{Time}}"))
                                                segUrl = PadNumber(mediaTemplate, "Time", segmentTime);
                                        }
                                        else
                                        {
                                            segUrl = mediaTemplate.Replace("{{Bandwidth}}", bandwidth.ToString());
                                            segUrl = segUrl.Replace("{{Number}}", segmentNumber.ToString());
                                            segUrl = segUrl.Replace("{{Time}}", segmentTime.ToString());
                                        }
                                        fragments.Add(new Dictionary<string, dynamic>()
                                        {
                                            [mediaLocationKey] = CombineURL(baseUrl, segUrl),
                                            ["duration"] = DoubleOrNull(segmentD, representationMsInfo["Timescale"])
                                        });
                                    }

                                    if (representationMsInfo.ContainsKey("S"))
                                    {
                                        for (int i = 0; i < representationMsInfo["S"].Count; i++)
                                        {
                                            var s = representationMsInfo["S"][i];
                                            segmentTime = s["t"] == 0 ? segmentTime : s["t"];
                                            segmentD = s["d"];
                                            addSegmentUrl();
                                            segmentNumber++;
                                            for (int j = 0; j < s["r"]; j++)
                                            {
                                                segmentTime += segmentD;
                                                addSegmentUrl();
                                                segmentNumber++;
                                            }
                                            segmentTime += segmentD;
                                        }
                                    }
                                    representationMsInfo["Fragments"] = fragments;
                                }
                            }
                            else if (representationMsInfo.ContainsKey("SegmentUrls") && representationMsInfo.ContainsKey("S"))
                            {
                                var fragments = new List<Dictionary<string, dynamic>>();

                                var segmentIndex = 0;
                                var timescale = representationMsInfo["Timescale"];
                                foreach (var s in representationMsInfo["S"])
                                {
                                    var duration = DoubleOrNull(s["d"], timescale);
                                    for (int j = 0; j < s["r"] + 1; j++)
                                    {
                                        var segmentUri = representationMsInfo["SegmentUrls"][segmentIndex];
                                        fragments.Add(new Dictionary<string, dynamic>()
                                        {
                                            [LocationKey(segmentUri)] = CombineURL(baseUrl, segmentUri),
                                            ["duration"] = duration
                                        });
                                        segmentIndex++;
                                    }
                                }

                                representationMsInfo["Fragments"] = fragments;
                            }
                            else if (representationMsInfo.ContainsKey("SegmentUrls"))
                            {
                                var fragments = new List<Dictionary<string, dynamic>>();

                                var segmentDuration = DoubleOrNull(representationMsInfo["SegmentDuration"], representationMsInfo.ContainsKey("SegmentDuration") ? representationMsInfo["Timescale"] : 1);
                                foreach (var segmentUrl in representationMsInfo["SegmentUrls"])
                                {
                                    if (segmentDuration != null)
                                    {
                                        fragments.Add(new Dictionary<string, dynamic>()
                                        {
                                            [LocationKey(segmentUrl)] = CombineURL(baseUrl, segmentUrl),
                                            ["duration"] = segmentDuration
                                        });
                                    }
                                    else
                                    {
                                        fragments.Add(new Dictionary<string, dynamic>()
                                        {
                                            [LocationKey(segmentUrl)] = CombineURL(baseUrl, segmentUrl)
                                        });
                                    }
                                }

                                representationMsInfo["Fragments"] = fragments;
                            }

                            if (representationMsInfo.ContainsKey("Fragments"))
                            {
                                f["Url"] = string.IsNullOrEmpty(mpdUrl) ? baseUrl : mpdUrl;
                                f["FragmentBaseUrl"] = baseUrl;
                                if (representationMsInfo.ContainsKey("InitializationUrl"))
                                {
                                    f["InitializationUrl"] = CombineURL(baseUrl, representationMsInfo["InitializationUrl"]);
                                    if (f["InitializationUrl"].StartsWith("$$Range"))
                                    {
                                        f["InitializationUrl"] = CombineURL(baseUrl, f["InitializationUrl"]);
                                    }
                                    f["Fragments"] = representationMsInfo["Fragments"];
                                }
                            }
                            else
                            {
                                //整段mp4
                                f["Fragments"] = new List<Dictionary<string, dynamic>> {
                                    new Dictionary<string, dynamic>()
                                    {
                                        ["url"] = baseUrl,
                                        ["duration"] = ts.TotalSeconds
                                    }
                                };
                            }

                            //处理同一ID分散在不同Period的情况
                            if (formatList.Any(_f => _f["FormatId"] == f["FormatId"] && _f["Width"] == f["Width"] && _f["ContentType"] == f["ContentType"]))
                            {
                                for (int i = 0; i < formatList.Count; i++)
                                {
                                    //参数相同但不在同一个Period才可以
                                    if (formatList[i]["FormatId"] == f["FormatId"] && formatList[i]["Width"] == f["Width"] && formatList[i]["ContentType"] == f["ContentType"] && formatList[i]["PeriodIndex"] != f["PeriodIndex"]) 
                                    {
                                        formatList[i]["Fragments"].AddRange(f["Fragments"]);
                                        break;
                                    }
                                }
                            }
                            else
                            {
                                formatList.Add(f);
                            }

                        }
                    }
                }
            }

            //排序
            formatList.Sort((a, b) =>
            {
                return (a["Width"] + a["Height"]) * 1000 + a["Tbr"] > (b["Width"] + b["Height"]) * 1000 + b["Tbr"] ? -1 : 1;
            });

            //默认为最高码率的视频和音频
            var bestVideo = SelectBestVideo(formatList);
            var bestAudio = SelectBestAudio(formatList);

            var audioLangList = new List<string>();
            formatList.ForEach(f =>
            {
                if (f["ContentType"] == "audio" && !audioLangList.Contains(f["Language"])) audioLangList.Add(f["Language"]);
            });

            if (audioLangList.Count > 1)
            {
                string Stringify(Dictionary<string, dynamic> f)
                {
                    var type = f["ContentType"] == "audio" ? "Audio" : "Video";
                    var res = type == "Video" ? $"[{f["Width"]}x{f["Height"]}]" : "";
                    var id = $"[{f["FormatId"]}] ";
                    var tbr = $"[{((int)f["Tbr"]).ToString().PadLeft(4)} Kbps] ";
                    var asr = f["Asr"] != -1 ? $"[{f["Asr"]} Hz] " : "";
                    var fps = f["Fps"] != -1 ? $"[{f["Fps"]} fps] " : "";
                    var lang = string.IsNullOrEmpty(f["Language"]) ? "" : $"[{f["Language"]}] ";
                    var codecs = $"[{f["Codecs"]}] ";
                    return $"{type} => {id}{tbr}{asr}{fps}{lang}{codecs}{res}";
                }

                var startCursorIndex = Console.CursorTop;
                var cursorIndex = startCursorIndex;
                for (int i = 0; i < formatList.Count; i++)
                {
                    Console.WriteLine("".PadRight(13) + $"[{i.ToString().PadLeft(2)}]. {Stringify(formatList[i])}");
                    cursorIndex++;
                }
                LOGGER.PrintLine("Found Multiple Language Audio Tracks.\r\n" + "".PadRight(13) + "Please Select What You Want(Up to 1 Video and 1 Audio).");
                Console.Write("".PadRight(13) + "Enter Numbers Separated By A Space: ");
                var input = Console.ReadLine();
                cursorIndex += 2;
                for (int i = startCursorIndex; i < cursorIndex; i++)
                {
                    Console.SetCursorPosition(0, i);
                    Console.Write("".PadRight(300));
                }
                Console.SetCursorPosition(0, startCursorIndex);
                if (!string.IsNullOrEmpty(input))
                {
                    bestVideo = new Dictionary<string, dynamic>() { ["Tbr"] = 0 };
                    bestAudio = new Dictionary<string, dynamic>() { ["Tbr"] = 0 };
                    foreach (var index in input.Split())
                    {
                        var n = 0;
                        int.TryParse(index, out n);
                        if (formatList[n]["ContentType"] == "audio")
                        {
                            bestAudio = formatList[n];
                        }
                        else
                        {
                            bestVideo = formatList[n];
                        }
                    }
                }
            }

            if (bestVideo.Keys.Count > 1 && bestAudio.Keys.Count > 1)  //音视频
            {
                return GenerateMasterList(downDir, bestVideo, bestAudio);
            }
            else if (bestAudio.Keys.Count > 1)  //仅有音频
            {
                return GenerateMasterList(downDir, bestAudio);
            }
            else if (bestVideo.Keys.Count > 1)  //仅有视频
            {
                return GenerateMasterList(downDir, bestVideo);
            }
            else
            {
                return "ERROR";
            }
        }


        /// <summary>
        /// 返回master文件地址
        /// </summary>
        /// <param name="downDir">存储目录</param>
        /// <param name="fs">format列表</param>
        /// <returns></returns>
        static string GenerateMasterList(string downDir, params Dictionary<string, dynamic>[] fs)
        {
            var audioPath = "";
            var videoPath = "";
            var bandwidth = 0;
            var codecsList = new List<string>();
            var res = "";

            foreach (var f in fs)
            {
                var m3u8 = GenerateM3u8(f);
                bandwidth += Convert.ToInt32(f["Tbr"] * 1000);
                //Video
                if (m3u8.Contains("#EXT-VIDEO-WIDTH"))
                {
                    var _path = Path.Combine(downDir, "mpdVideo.m3u8");
                    File.WriteAllText(_path, m3u8);
                    videoPath = new Uri(_path).ToString();
                    res = f["Width"] + "x" + f["Height"];
                }
                else
                {
                    var _path = Path.Combine(downDir, "mpdAudio.m3u8");
                    File.WriteAllText(_path, m3u8);
                    audioPath = new Uri(_path).ToString();
                }
                codecsList.Add(f["Codecs"]);
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
                var _path = Path.Combine(downDir + "(Audio)", "mpdAudio.m3u8");
                File.Copy(new Uri(audioPath).LocalPath, _path, true);
                audioPath = new Uri(_path).ToString();
                content = $"#EXTM3U\r\n" +
                    $"#EXT-X-MEDIA:TYPE=AUDIO,URI=\"{audioPath}\",GROUP-ID=\"default-audio-group\",NAME=\"stream_0\",AUTOSELECT=YES,CHANNELS=\"0\"\r\n" +
                    $"#EXT-X-STREAM-INF:BANDWIDTH={bandwidth},CODECS=\"{string.Join(",", codecsList)}\",RESOLUTION={res},AUDIO=\"default-audio-group\"\r\n" +
                    $"{videoPath}";
            }

            var _masterPath = Path.Combine(downDir, "master.m3u8");
            File.WriteAllText(_masterPath, content);
            return new Uri(_masterPath).ToString();
        }

        static string GenerateM3u8(Dictionary<string, dynamic> f)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("#EXTM3U");
            sb.AppendLine("#EXT-X-VERSION:3");
            sb.AppendLine("#EXT-X-PLAYLIST-TYPE:VOD");
            sb.AppendLine("#CREATED-BY:N_m3u8DL-CLI");

            //Video
            if (f["ContentType"] != "audio")
            {
                sb.AppendLine($"#EXT-VIDEO-WIDTH:{f["Width"]}");
                sb.AppendLine($"#EXT-VIDEO-HEIGHT:{f["Height"]}");
            }

            sb.AppendLine($"#EXT-CODEC:{f["Codecs"]}");
            sb.AppendLine($"#EXT-TBR:{f["Tbr"]}");
            if (f.ContainsKey("InitializationUrl"))
            {
                string initUrl = f["InitializationUrl"];
                if (Regex.IsMatch(initUrl, "\\$\\$Range=(\\d+)-(\\d+)"))
                {
                    var match = Regex.Match(initUrl, "\\$\\$Range=(\\d+)-(\\d+)");
                    string rangeStr = match.Value;
                    long start = Convert.ToInt64(match.Groups[1].Value);
                    long end = Convert.ToInt64(match.Groups[2].Value);
                    sb.AppendLine($"#EXT-X-MAP:URI=\"{initUrl.Replace(rangeStr, "")}\",BYTERANGE=\"{end + 1 - start}@{start}\"");
                }
                else
                {
                    sb.AppendLine($"#EXT-X-MAP:URI=\"{initUrl}\"");
                }
            }
            sb.AppendLine("#EXT-X-KEY:METHOD=PLZ-KEEP-RAW,URI=\"None\""); //使下载器使用二进制合并

            List<Dictionary<string, dynamic>> fragments = f["Fragments"];

            //检测最后一片的有效性
            if (fragments.Count > 1)
            {
                bool checkValid(string url)
                {
                    try
                    {
                        HttpWebRequest request = (HttpWebRequest)HttpWebRequest.Create(new Uri(url));
                        request.Timeout = 120000;
                        HttpWebResponse response = (HttpWebResponse)request.GetResponse();
                        if (((int)response.StatusCode).ToString().StartsWith("2")) return true;
                        else return false;
                    }
                    catch (Exception) { return false; }
                }

                var last = fragments.Last();
                var secondToLast = fragments[fragments.Count - 2];
                var urlLast = last.ContainsKey("url") ? last["url"] : last["path"];
                var urlSecondToLast = secondToLast.ContainsKey("url") ? secondToLast["url"] : secondToLast["path"];
                //普通分段才判断
                if (urlLast.StartsWith("http") && !Regex.IsMatch(urlLast, "\\$\\$Range=(\\d+)-(\\d+)"))
                {
                    LOGGER.PrintLine(strings.checkingLast + (f["ContentType"] != "audio" ? "(Video)" : "(Audio)"));
                    LOGGER.WriteLine(strings.checkingLast + (f["ContentType"] != "audio" ? "(Video)" : "(Audio)"));
                    //倒数第二段正常，倒数第一段不正常
                    if (checkValid(urlSecondToLast) && !checkValid(urlLast))
                        fragments.RemoveAt(fragments.Count - 1);
                }
            }

            //添加分段
            foreach (var seg in fragments)
            {
                var dur = seg.ContainsKey("duration") ? seg["duration"] : 0.0;
                var url = seg.ContainsKey("url") ? seg["url"] : seg["path"];
                sb.AppendLine($"#EXTINF:{dur.ToString("0.00")}");
                if (Regex.IsMatch(url, "\\$\\$Range=(\\d+)-(\\d+)"))
                {
                    var match = Regex.Match(url, "\\$\\$Range=(\\d+)-(\\d+)");
                    string rangeStr = match.Value;
                    long start = Convert.ToInt64(match.Groups[1].Value);
                    long end = Convert.ToInt64(match.Groups[2].Value);
                    sb.AppendLine($"#EXT-X-BYTERANGE:{end + 1 - start}@{start}");
                    sb.AppendLine(url.Replace(rangeStr, ""));
                }
                else
                {
                    sb.AppendLine(url);
                }
            }

            sb.AppendLine("#EXT-X-ENDLIST");

            return sb.ToString();
        }

        static Dictionary<string, dynamic> SelectBestVideo(List<Dictionary<string, dynamic>> fs)
        {
            var best = new Dictionary<string, dynamic>() { ["Tbr"] = 0 };
            var bandwidth = best["Tbr"];
            var width = 0;

            foreach (var f in fs)
            {
                var w = f["Width"];
                var h = f["Height"];
                if (f["ContentType"] == "video")
                {
                    if (f["Tbr"] > bandwidth && w > width)
                    {
                        best = f;
                        bandwidth = f["Tbr"];
                        width = w;
                    }
                }
            }

            return best;
        }

        static Dictionary<string, dynamic> SelectBestAudio(List<Dictionary<string, dynamic>> fs)
        {
            var best = new Dictionary<string, dynamic>() { ["Tbr"] = 0 };
            var bandwidth = best["Tbr"];

            foreach (var f in fs)
            {
                if (f["ContentType"] == "audio")
                {
                    if (f["Tbr"] > bandwidth)
                    {
                        best = f;
                        bandwidth = f["Tbr"];
                    }
                }
            }

            return best;
        }

        static int IntOrNull(object text, int scale = 1)
        {
            try
            {
                return Convert.ToInt32(text) / scale;
            }
            catch (Exception)
            {
                return -1;
            }
        }

        static double DoubleOrNull(object text, int scale = 1)
        {
            try
            {
                return Convert.ToDouble(text) / scale;
            }
            catch (Exception)
            {
                return -1;
            }
        }

        /// <summary>
        /// 拼接Baseurl和RelativeUrl
        /// </summary>
        /// <param name="baseurl">Baseurl</param>
        /// <param name="url">RelativeUrl</param>
        /// <returns></returns>
        static string CombineURL(string baseurl, string url)
        {
            if (url.StartsWith("$$Range"))
            {
                return baseurl + url;
            }
            Uri uri1 = new Uri(baseurl);
            Uri uri2 = new Uri(uri1, url);
            url = uri2.ToString();

            return url;
        }
    }
}
