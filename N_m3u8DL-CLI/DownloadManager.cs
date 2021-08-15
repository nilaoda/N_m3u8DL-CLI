using Newtonsoft.Json.Linq;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace N_m3u8DL_CLI
{
    class DownloadManager
    {
        private int stopCount = 0;           //速度为零的停止

        private string jsonFile = string.Empty;
        private int total = 0;
        public static string partsPadZero = string.Empty;
        string segsPadZero = string.Empty;
        private bool isVTT = false;
        bool externalAudio = false;  //额外的音轨
        string externalAudioUrl = "";
        bool externalSub = false;  //额外的字幕
        string externalSubUrl = "";
        string fflogName = "_ffreport.log";
        public static bool BinaryMerge = false;

        public int Threads { get; set; } = 1;
        public int RetryCount { get; set; } = 5;
        public string Headers { get; set; } = string.Empty;
        public string DownDir { get; set; } = string.Empty;
        public string DownName { get; set; } = string.Empty;
        public bool DelAfterDone { get; set; } = false;
        public string MuxFormat { get; set; } = "mp4";
        public bool MuxFastStart { get; set; } = true;
        public string MuxSetJson { get; set; } = string.Empty;
        public int TimeOut { get; set; } = 10000;         //超时设置
        public static double DownloadedSize { get; set; } = 0;   //已下载大小
        public static double ToDoSize { get; set; } = 0;   //待下载大小
        public static bool HasSetDir { get; set; } = false;
        public bool NoMerge { get; set; } = false;
        public static int CalcTime { get; set; } = 1;            //计算速度的间隔
        public static int Count { get; set; } = 0;
        public static int PartsCount { get; set; } = 0;
        public static bool DisableIntegrityCheck { get; set; } = false; //关闭完整性检查
        public static bool HasExtMap { get; set; } = false; //是否有MAP

        static CancellationTokenSource cts = new CancellationTokenSource();
        //计算下载速度
        static System.Timers.Timer timer = new System.Timers.Timer(1000 * CalcTime);   //实例化Timer类

        public DownloadManager()
        {
            timer.AutoReset = true;
            timer.Elapsed += delegate
            {
                var eta = "";
                if (ToDoSize != 0)
                {
                    eta = " @ " + Global.FormatTime(Convert.ToInt32(ToDoSize / (Global.BYTEDOWN / CalcTime)));
                }
                var print = Global.FormatFileSize((Global.BYTEDOWN) / CalcTime) + "/s" + eta;
                ProgressReporter.Report("", "(" + print + ")");

                if (Global.HadReadInfo && Global.BYTEDOWN <= Global.STOP_SPEED * 1024 * CalcTime)
                {
                    stopCount++;
                    eta = "";
                    if (ToDoSize != 0)
                    {
                        eta = " @ " + Global.FormatTime(Convert.ToInt32(ToDoSize / (Global.BYTEDOWN / CalcTime)));
                    }
                    print = Global.FormatFileSize((Global.BYTEDOWN) / CalcTime) + "/s [" + stopCount + "]" + eta;
                    ProgressReporter.Report("", "(" + print + ")");

                    if (stopCount >= 12)
                    {
                        Global.ShouldStop = true;
                        cts.Cancel();
                        timer.Enabled = false;
                    }
                }
                else
                {
                    stopCount = 0;
                    Global.BYTEDOWN = 0;
                    Global.ShouldStop = false;
                }
            };
        }

        public void DoDownload()
        {
            jsonFile = Path.Combine(DownDir, "meta.json");
            if (!File.Exists(jsonFile))
                return;

            string jsonContent = File.ReadAllText(jsonFile);
            JObject initJson = JObject.Parse(jsonContent);
            JArray parts = JArray.Parse(initJson["m3u8Info"]["segments"].ToString()); //大分组
            string segCount = initJson["m3u8Info"]["count"].ToString();
            string oriCount = initJson["m3u8Info"]["originalCount"].ToString(); //原始分片数量
            string isVOD = initJson["m3u8Info"]["vod"].ToString();
            try
            {
                if (initJson["m3u8Info"]["audio"].ToString() != "")
                    externalAudio = true;
                externalAudioUrl = initJson["m3u8Info"]["audio"].ToString();
                LOGGER.WriteLine(strings.hasExternalAudioTrack);
                LOGGER.PrintLine(strings.hasExternalAudioTrack, LOGGER.Warning);
            }
            catch (Exception) {}
            try
            {
                if (initJson["m3u8Info"]["sub"].ToString() != "")
                    externalSub = true;
                externalSubUrl = initJson["m3u8Info"]["sub"].ToString();
                LOGGER.WriteLine(strings.hasExternalSubtitleTrack);
                LOGGER.PrintLine(strings.hasExternalSubtitleTrack, LOGGER.Warning);
            }
            catch (Exception) { }
            total = Convert.ToInt32(segCount);
            PartsCount = parts.Count;
            segsPadZero = string.Empty.PadRight(oriCount.Length, '0');
            partsPadZero = string.Empty.PadRight(Convert.ToString(parts.Count).Length, '0');

            //是直播视频
            if (isVOD == "False")
            {
                return;
            }

            Global.ShouldStop = false; //是否该停止下载

            if (!Directory.Exists(DownDir))
                Directory.CreateDirectory(DownDir); //新建文件夹  
            Watcher watcher = new Watcher(DownDir);
            watcher.Total = total;
            watcher.PartsCount = PartsCount;
            watcher.WatcherStrat();

            cts = new CancellationTokenSource();

            //开始调用下载
            LOGGER.WriteLine(strings.startDownloading);
            LOGGER.PrintLine(strings.startDownloading, LOGGER.Warning);

            //下载MAP文件（若有）
            downloadMap:
            if (HasExtMap)
            {
                LOGGER.PrintLine(strings.downloadingMapFile);
                Downloader sd = new Downloader();
                sd.TimeOut = TimeOut;
                sd.FileUrl = initJson["m3u8Info"]["extMAP"].Value<string>();
                sd.Headers = Headers;
                sd.Method = "NONE";
                if (sd.FileUrl.Contains("|"))  //有range
                {
                    string[] tmp = sd.FileUrl.Split('|');
                    sd.FileUrl = tmp[0];
                    sd.StartByte = Convert.ToUInt32(tmp[1].Split('@')[1]);
                    sd.ExpectByte = Convert.ToUInt32(tmp[1].Split('@')[0]);
                }
                sd.SavePath = DownDir + "\\!MAP.tsdownloading";
                if (File.Exists(sd.SavePath))
                    File.Delete(sd.SavePath);
                if (File.Exists(DownDir + "\\Part_0\\!MAP.ts"))
                    File.Delete(DownDir + "\\Part_0\\!MAP.ts");
                sd.Down();  //开始下载
                if (!File.Exists(DownDir + "\\!MAP.ts")) //检测是否成功下载
                {
                    Thread.Sleep(1000);
                    goto downloadMap;
                }
            }

            //首先下载第一个分片
            JToken firstSeg = JArray.Parse(parts[0].ToString())[0];
            if (!File.Exists(DownDir + "\\Part_" + 0.ToString(partsPadZero) + "\\" + firstSeg["index"].Value<int>().ToString(segsPadZero) + ".ts"))
            {
                try
                {
                    Downloader sd = new Downloader();
                    sd.TimeOut = TimeOut;
                    sd.SegDur = firstSeg["duration"].Value<double>();
                    if (sd.SegDur < 0) sd.SegDur = 0; //防止负数
                    sd.FileUrl = firstSeg["segUri"].Value<string>();
                    //VTT字幕
                    if (isVTT == false && (sd.FileUrl.Trim('\"').EndsWith(".vtt") || sd.FileUrl.Trim('\"').EndsWith(".webvtt")))
                        isVTT = true;
                    sd.Method = firstSeg["method"].Value<string>();
                    if (sd.Method != "NONE")
                    {
                        sd.Key = firstSeg["key"].Value<string>();
                        sd.Iv = firstSeg["iv"].Value<string>();
                    }
                    if (firstSeg["expectByte"] != null)
                        sd.ExpectByte = firstSeg["expectByte"].Value<long>();
                    if (firstSeg["startByte"] != null)
                        sd.StartByte = firstSeg["startByte"].Value<long>();
                    sd.Headers = Headers;
                    sd.SavePath = DownDir + "\\Part_" + 0.ToString(partsPadZero) + "\\" + firstSeg["index"].Value<int>().ToString(segsPadZero) + ".tsdownloading";
                    if (File.Exists(sd.SavePath))
                        File.Delete(sd.SavePath);
                    LOGGER.PrintLine(strings.downloadingFirstSegement);
                    //开始计算速度
                    timer.Enabled = true;
                    if (!Global.ShouldStop)
                        sd.Down();  //开始下载
                }
                catch (Exception e)
                {
                    //LOG.WriteLineError(e.ToString());
                }
            }

            if (Global.HadReadInfo == false)
            {
                string href = DownDir + "\\Part_" + 0.ToString(partsPadZero) + "\\" + firstSeg["index"].Value<int>().ToString(segsPadZero) + ".ts";
                if (File.Exists(DownDir + "\\!MAP.ts"))
                    href = DownDir + "\\!MAP.ts";
                Global.GzipHandler(href);
                bool flag = false;
                foreach (string ss in (string[])Global.GetVideoInfo(href).ToArray(typeof(string)))
                {
                    LOGGER.WriteLine(ss.Trim());
                    LOGGER.PrintLine(ss.Trim(), 0);
                    if (ss.Trim().Contains("Error in reading file"))
                        flag = true;
                }
                LOGGER.PrintLine(strings.waitForCompletion, LOGGER.Warning);
                if (!flag)
                    Global.HadReadInfo = true;
            }

            //多线程设置
            ParallelOptions parallelOptions = new ParallelOptions
            {
                MaxDegreeOfParallelism = Threads,
                CancellationToken = cts.Token
            };

            //构造包含所有分片的新的segments
            JArray segments = new JArray();
            for (int i = 0; i < parts.Count; i++)
            {
                var tmp = JArray.Parse(parts[i].ToString());
                for (int j = 0; j < tmp.Count; j++)
                {
                    JObject t = (JObject)tmp[j];
                    t.Add("part", i);
                    segments.Add(t);
                }
            }

            //剔除第一个分片（已下载过）
            segments.RemoveAt(0);

            try
            {
                ParallelLoopResult result = Parallel.ForEach(segments,
                    parallelOptions,
                    () => new Downloader(),
                    (info, loopstate, index, sd) =>
                    {
                        if (Global.ShouldStop)
                            loopstate.Stop();
                        else
                        {
                            sd.TimeOut = TimeOut;
                            sd.SegDur = info["duration"].Value<double>();
                            if (sd.SegDur < 0) sd.SegDur = 0; //防止负数
                                sd.FileUrl = info["segUri"].Value<string>();
                            //VTT字幕
                            if (isVTT == false && (sd.FileUrl.Trim('\"').EndsWith(".vtt") || sd.FileUrl.Trim('\"').EndsWith(".webvtt")))
                                isVTT = true;
                            sd.Method = info["method"].Value<string>();
                            if (sd.Method != "NONE")
                            {
                                sd.Key = info["key"].Value<string>();
                                sd.Iv = info["iv"].Value<string>();
                            }
                            if (firstSeg["expectByte"] != null)
                                sd.ExpectByte = info["expectByte"].Value<long>();
                            if (firstSeg["startByte"] != null)
                                sd.StartByte = info["startByte"].Value<long>();
                            sd.Headers = Headers;
                            sd.SavePath = DownDir + "\\Part_" + info["part"].Value<int>().ToString(partsPadZero) + "\\" + info["index"].Value<int>().ToString(segsPadZero) + ".tsdownloading";
                            if (File.Exists(sd.SavePath))
                                File.Delete(sd.SavePath);
                            if (!Global.ShouldStop)
                                sd.Down();  //开始下载
                            }
                        return sd;
                    },
                    (sd) => { });

                if (result.IsCompleted)
                {
                    //LOGGER.WriteLine("Part " + (info["part"].Value<int>() + 1).ToString(partsPadZero) + " of " + parts.Count + " Completed");
                }
            }
            catch (Exception)
            {
                ;//捕获取消循环产生的异常
            }
            finally
            {
                cts.Dispose();
            }

            watcher.WatcherStop();

            //停止速度监测
            timer.Enabled = false;

            //检测是否下完
            IsComplete(Convert.ToInt32(segCount));
        }
        
        public void IsComplete(int segCount)
        {
            int tsCount = 0;

            if (DisableIntegrityCheck)
            {
                tsCount = segCount;
                goto ll;
            }

            for (int i = 0; i < PartsCount; i++) 
            {
                tsCount += Global.GetFileCount(DownDir + "\\Part_" + i.ToString(partsPadZero), ".ts");
            }

        ll:
            if (tsCount != segCount)
            {
                LOGGER.PrintLine(strings.downloadedCount + tsCount + " / " + segCount);
                LOGGER.WriteLine(strings.downloadedCount + tsCount + " of " + segCount);
                if (Count <= RetryCount)
                {
                    Count++;
                    LOGGER.WriteLine(strings.retryCount + Count + " / " + RetryCount);
                    LOGGER.PrintLine(strings.retryCount + Count + " / " + RetryCount, LOGGER.Warning);
                    Thread.Sleep(3000);
                    DoDownload();
                }
            }
            else  //开始合并
            {
                LOGGER.PrintLine(strings.downloadComplete + (DisableIntegrityCheck ? "(" + strings.disableIntegrityCheck + ")" : ""));
                if (NoMerge == false)
                {
                    string exePath = Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName);
                    string driverName = exePath.Remove(exePath.IndexOf(':'));
                    Console.Title = "Done.";
                    LOGGER.WriteLine(strings.startMerging);
                    LOGGER.PrintLine(strings.startMerging, LOGGER.Warning);
                    //VTT字幕
                    if (isVTT == true)
                    {
                        MuxFormat = "vtt";
                        Global.ReAdjustVtt(Global.GetFiles(DownDir + "\\Part_0", ".ts"));
                    }
                    //只有一个Part直接用ffmpeg合并
                    if (PartsCount == 1)
                    {
                        /*
                         * FFREPORT=file=C\:/Users/nilao/Desktop/新建文件夹/3.log:level=32
                         * Test with Powershell, its C:/Users/nilao/Desktop/新建文件夹/3.log
                         */
                        FFmpeg.OutPutPath = Path.Combine(Directory.GetParent(DownDir).FullName, DownName);
                        FFmpeg.ReportFile = driverName + "\\:" + exePath.Remove(0, exePath.IndexOf(':') + 1).Replace("\\", "/") + "/Logs/" + Path.GetFileNameWithoutExtension(LOGGER.LOGFILE) + fflogName;
                        if (File.Exists(DownDir + "\\!MAP.ts"))
                            File.Move(DownDir + "\\!MAP.ts", DownDir + "\\Part_0\\!MAP.ts");

                        if (BinaryMerge)
                        {
                            LOGGER.PrintLine(strings.binaryMergingPleaseWait);
                            MuxFormat = "ts";
                            //有MAP文件，一般为mp4，采取默认动作
                            if(File.Exists(DownDir + "\\Part_0\\!MAP.ts"))
                                MuxFormat = "mp4";
                            if (isVTT)
                                MuxFormat = "vtt";

                            if (Global.AUDIO_TYPE != "")
                                MuxFormat = Global.AUDIO_TYPE;
                            Global.CombineMultipleFilesIntoSingleFile(Global.GetFiles(DownDir + "\\Part_0", ".ts"), FFmpeg.OutPutPath + $".{MuxFormat}");
                        }
                        else
                        {
                            if (Global.VIDEO_TYPE != "DV") //不是杜比视界
                            {
                                //检测是否为MPEG-TS封装，不是的话就转换为TS封装
                                foreach (string s in Global.GetFiles(DownDir + "\\Part_0", ".ts"))
                                {
                                    //跳过有MAP的情况
                                    if (!isVTT && !File.Exists(DownDir + "\\Part_0\\!MAP.ts") && !FFmpeg.CheckMPEGTS(s))
                                    {
                                        //转换
                                        LOGGER.PrintLine(strings.remuxToMPEGTS + Path.GetFileName(s));
                                        LOGGER.WriteLine(strings.remuxToMPEGTS + Path.GetFileName(s));
                                        FFmpeg.ConvertToMPEGTS(s);
                                    }
                                }

                                //分片过多的情况
                                if (tsCount >= 1800)
                                {
                                    LOGGER.WriteLine(strings.partialMergingPleaseWait);
                                    LOGGER.PrintLine(strings.partialMergingPleaseWait, LOGGER.Warning);
                                    Global.PartialCombineMultipleFiles(Global.GetFiles(DownDir + "\\Part_0", ".ts"));
                                }

                                if (Global.AUDIO_TYPE != "")
                                    MuxFormat = Global.AUDIO_TYPE;

                                LOGGER.PrintLine(strings.ffmpegMergingPleaseWait);
                                if (!File.Exists(MuxSetJson))
                                    FFmpeg.Merge(Global.GetFiles(DownDir + "\\Part_0", ".ts"), MuxFormat, MuxFastStart);
                                else
                                {
                                    JObject json = JObject.Parse(File.ReadAllText(MuxSetJson, Encoding.UTF8));
                                    string muxFormat = json["muxFormat"].Value<string>();
                                    bool fastStart = Convert.ToBoolean(json["fastStart"].Value<string>());
                                    string poster = json["poster"].Value<string>();
                                    string audioName = json["audioName"].Value<string>();
                                    string title = json["title"].Value<string>();
                                    string copyright = json["copyright"].Value<string>();
                                    string comment = json["comment"].Value<string>();
                                    string encodingTool = "";
                                    try { encodingTool = json["encodingTool"].Value<string>(); } catch (Exception) {; }
                                    FFmpeg.Merge(Global.GetFiles(DownDir + "\\Part_0", ".ts"), muxFormat, fastStart, poster, audioName, title, copyright, comment, encodingTool);
                                }
                                //Global.CombineMultipleFilesIntoSingleFile(Global.GetFiles(DownDir + "\\Part_0", ".ts"), FFmpeg.OutPutPath + ".ts");

                                //Global.ExplorerFile(FFmpeg.OutPutPath + ".mp4");
                            }
                            else
                            {
                                LOGGER.PrintLine(strings.dolbyVisionContentMerging);
                                Global.CombineMultipleFilesIntoSingleFile(Global.GetFiles(DownDir + "\\Part_0", ".ts"), FFmpeg.OutPutPath + ".mp4");
                            }
                        }

                        LOGGER.WriteLine(strings.taskDone
                                + "\r\n\r\nTask End: " + DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss")
                                + "\r\nFile: " + FFmpeg.OutPutPath + "." + (MuxFormat == "aac" ? "m4a" : MuxFormat) + "\r\n\r\n");

                        //删除文件夹
                        if (DelAfterDone)
                        {
                            try
                            {
                                DirectoryInfo directoryInfo = new DirectoryInfo(DownDir);
                                directoryInfo.Delete(true);
                            }
                            catch (Exception) { }
                        }
                        if (externalAudio)  //下载独立音轨
                        {
                            externalAudio = false;
                            DownloadedSize = 0;
                            Global.WriteInit();
                            LOGGER.PrintLine(strings.downloadingExternalAudioTrack, LOGGER.Warning);
                            Parser parser = new Parser();
                            parser.Headers = Headers; //继承Header
                            parser.BaseUrl = "";
                            parser.M3u8Url = externalAudioUrl;
                            parser.DownName = DownName + "(Audio)";
                            parser.DownDir = Path.Combine(Path.GetDirectoryName(DownDir), parser.DownName);
                            LOGGER.WriteLine(strings.startParsing + externalAudioUrl);
                            LOGGER.WriteLine(strings.downloadingExternalAudioTrack);
                            DownName = DownName + "(Audio)";
                            fflogName = "_ffreport(Audio).log";
                            DownDir = parser.DownDir;
                            parser.Parse();  //开始解析
                            Thread.Sleep(1000);
                            Global.HadReadInfo = false;
                            Global.VIDEO_TYPE = "";
                            Global.AUDIO_TYPE = "";
                            DoDownload();
                        }
                        if (externalSub)  //下载独立字幕
                        {
                            externalSub = false;
                            DownloadedSize = 0;
                            Global.WriteInit();
                            LOGGER.PrintLine(strings.downloadingExternalSubtitleTrack, LOGGER.Warning);
                            Parser parser = new Parser();
                            parser.Headers = Headers; //继承Header
                            parser.BaseUrl = "";
                            parser.M3u8Url = externalSubUrl;
                            parser.DownName = DownName.Replace("(Audio)", "") + "(Subtitle)";
                            parser.DownDir = Path.Combine(Path.GetDirectoryName(DownDir), parser.DownName);
                            LOGGER.WriteLine(strings.startParsing + externalSubUrl);
                            LOGGER.WriteLine(strings.downloadingExternalSubtitleTrack);
                            DownName = parser.DownName;
                            fflogName = "_ffreport(Subtitle).log";
                            DownDir = parser.DownDir;
                            parser.Parse();  //开始解析
                            Thread.Sleep(1000);
                            Global.HadReadInfo = false;
                            Global.VIDEO_TYPE = "";
                            Global.AUDIO_TYPE = "";
                            DoDownload();
                        }
                        LOGGER.PrintLine(strings.taskDone, LOGGER.Warning);
                        Environment.Exit(0);  //正常退出程序
                        return;
                    }

                    FFmpeg.OutPutPath = Path.Combine(Directory.GetParent(DownDir).FullName, DownName);
                    FFmpeg.ReportFile = driverName + "\\:" + exePath.Remove(0, exePath.IndexOf(':') + 1).Replace("\\", "/") + "/Logs/" + Path.GetFileNameWithoutExtension(LOGGER.LOGFILE) + fflogName;

                    //合并分段
                    LOGGER.PrintLine(strings.startMerging);
                    for (int i = 0; i < PartsCount; i++)
                    {
                        string outputFilePath = DownDir + "\\Part_" + i.ToString(partsPadZero) + ".ts";
                        Global.CombineMultipleFilesIntoSingleFile(
                           Global.GetFiles(DownDir + "\\Part_" + i.ToString(partsPadZero), ".ts"),
                           outputFilePath);
                        try
                        {
                            DirectoryInfo directoryInfo = new DirectoryInfo(DownDir + "\\Part_" + i.ToString(partsPadZero));
                            directoryInfo.Delete(true);
                        }
                        catch (Exception) { }
                    }
                    

                    if (BinaryMerge)
                    {
                        LOGGER.PrintLine(strings.binaryMergingPleaseWait);
                        MuxFormat = "ts";
                        //有MAP文件，一般为mp4，采取默认动作
                        if (File.Exists(DownDir + "\\!MAP.ts")) 
                            MuxFormat = "mp4";
                        if (isVTT)
                            MuxFormat = "vtt";
                        Global.CombineMultipleFilesIntoSingleFile(Global.GetFiles(DownDir, ".ts"), FFmpeg.OutPutPath + $".{MuxFormat}");
                    }
                    else
                    {
                        if (Global.VIDEO_TYPE != "DV")  //不是爱奇艺杜比视界
                        {
                            //检测是否为MPEG-TS封装，不是的话就转换为TS封装
                            foreach (string s in Global.GetFiles(DownDir, ".ts"))
                            {
                                //跳过有MAP的情况
                                if (!isVTT && !File.Exists(DownDir + "\\!MAP.ts") && !FFmpeg.CheckMPEGTS(s))
                                {
                                    //转换
                                    LOGGER.PrintLine(strings.remuxToMPEGTS + Path.GetFileName(s));
                                    LOGGER.WriteLine(strings.remuxToMPEGTS + Path.GetFileName(s));
                                    FFmpeg.ConvertToMPEGTS(s);
                                }
                            }

                            if (Global.AUDIO_TYPE != "")
                                MuxFormat = Global.AUDIO_TYPE;

                            LOGGER.PrintLine(strings.ffmpegMergingPleaseWait);
                            if (!File.Exists(MuxSetJson))
                                FFmpeg.Merge(Global.GetFiles(DownDir, ".ts"), MuxFormat, MuxFastStart);
                            else
                            {
                                JObject json = JObject.Parse(File.ReadAllText(MuxSetJson, Encoding.UTF8));
                                string muxFormat = json["muxFormat"].Value<string>();
                                bool fastStart = Convert.ToBoolean(json["fastStart"].Value<string>());
                                string poster = json["poster"].Value<string>();
                                string audioName = json["audioName"].Value<string>();
                                string title = json["title"].Value<string>();
                                string copyright = json["copyright"].Value<string>();
                                string comment = json["comment"].Value<string>();
                                string encodingTool = "";
                                try { encodingTool = json["encodingTool"].Value<string>(); } catch (Exception) {; }
                                FFmpeg.Merge(Global.GetFiles(DownDir, ".ts"), muxFormat, fastStart, poster, audioName, title, copyright, comment, encodingTool);
                            }
                        }
                        else
                        {
                            LOGGER.PrintLine(strings.dolbyVisionContentMerging);
                            Global.CombineMultipleFilesIntoSingleFile(Global.GetFiles(DownDir, ".ts"), FFmpeg.OutPutPath + ".mp4");
                        }
                    }

                    LOGGER.WriteLine(strings.taskDone
                        + "\r\n\r\nTask End: " + DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss")
                        + "\r\nFile: " + FFmpeg.OutPutPath + "." + (MuxFormat == "aac" ? "m4a" : MuxFormat) + "\r\n\r\n");
                    //Global.ExplorerFile(FFmpeg.OutPutPath + ".mp4");
                    //删除文件夹
                    if (DelAfterDone)
                    {
                        try
                        {
                            DirectoryInfo directoryInfo = new DirectoryInfo(DownDir);
                            directoryInfo.Delete(true);
                        }
                        catch (Exception) { }
                    }
                    if (externalAudio)  //下载独立音轨
                    {
                        externalAudio = false;
                        DownloadedSize = 0;
                        Global.WriteInit();
                        LOGGER.PrintLine(strings.downloadingExternalAudioTrack, LOGGER.Warning);
                        Parser parser = new Parser();
                        parser.Headers = Headers; //继承Header
                        parser.BaseUrl = "";
                        parser.M3u8Url = externalAudioUrl;
                        parser.DownName = DownName + "(Audio)";
                        parser.DownDir = Path.Combine(Path.GetDirectoryName(DownDir), parser.DownName);
                        LOGGER.WriteLine(strings.startParsing + externalAudioUrl);
                        LOGGER.WriteLine(strings.downloadingExternalAudioTrack);
                        DownName = parser.DownName;
                        fflogName = "_ffreport(Audio).log";
                        DownDir = parser.DownDir;
                        parser.Parse();  //开始解析
                        Thread.Sleep(1000);
                        Global.HadReadInfo = false;
                        Global.VIDEO_TYPE = "";
                        Global.AUDIO_TYPE = "";
                        DoDownload();
                    }
                    if (externalSub)  //下载独立字幕
                    {
                        externalSub = false;
                        DownloadedSize = 0;
                        Global.WriteInit();
                        LOGGER.PrintLine(strings.downloadingExternalSubtitleTrack, LOGGER.Warning);
                        Parser parser = new Parser();
                        parser.Headers = Headers; //继承Header
                        parser.BaseUrl = "";
                        parser.M3u8Url = externalSubUrl;
                        parser.DownName = DownName.Replace("(Audio)", "") + "(Subtitle)";
                        parser.DownDir = Path.Combine(Path.GetDirectoryName(DownDir), parser.DownName);
                        LOGGER.WriteLine(strings.startParsing + externalSubUrl);
                        LOGGER.WriteLine(strings.downloadingExternalSubtitleTrack);
                        DownName = parser.DownName;
                        fflogName = "_ffreport(Subtitle).log";
                        DownDir = parser.DownDir;
                        parser.Parse();  //开始解析
                        Thread.Sleep(1000);
                        Global.HadReadInfo = false;
                        Global.VIDEO_TYPE = "";
                        Global.AUDIO_TYPE = "";
                        DoDownload();
                    }
                    LOGGER.PrintLine(strings.taskDone, LOGGER.Warning);
                    Environment.Exit(0);  //正常退出程序
                }
                else
                {
                    Console.Title = "Done.";
                    LOGGER.PrintLine(strings.taskDone, LOGGER.Warning);
                    LOGGER.WriteLine(strings.taskDone
                        + "\r\n\r\nTask End: " + DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss"));
                    Environment.Exit(0);  //正常退出程序
                }
            }
        }
    }
}
