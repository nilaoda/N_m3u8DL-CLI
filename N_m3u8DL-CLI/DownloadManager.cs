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
        private static int calcTime = 1;            //计算文件夹大小的间隔
        private int stopCount = 0;           //速度为零的停止
        private int timeOut = 10000;         //超时设置
        private static double downloadedSize = 0;   //已下载大小
        private static bool disableIntegrityCheck = false; //关闭完整性检查

        private string jsonFile = string.Empty;
        private string headers = string.Empty;
        private string downDir = string.Empty;
        private string downName = string.Empty;
        private string muxSetJson = string.Empty;
        private int threads = 1;
        private int retryCount = 5;
        private static int count = 0;
        private static int partsCount = 0;
        private int total = 0;
        public static string partsPadZero = string.Empty;
        string segsPadZero = string.Empty;
        bool delAfterDone = false;
        private bool isVTT = false;
        bool externalAudio = false;  //额外的音轨
        string externalAudioUrl = "";
        bool externalSub = false;  //额外的字幕
        string externalSubUrl = "";
        string fflogName = "_ffreport.log";
        private bool binaryMerge = false;
        private bool noMerge = false;
        private bool muxFastStart = true;
        private string muxFormat = "mp4";
        private static bool hasSetDir = false;

        public int Threads { get => threads; set => threads = value; }
        public int RetryCount { get => retryCount; set => retryCount = value; }
        public string Headers { get => headers; set => headers = value; }
        public string DownDir { get => downDir; set => downDir = value; }
        public string DownName { get => downName; set => downName = value; }
        public bool DelAfterDone { get => delAfterDone; set => delAfterDone = value; }
        public string MuxFormat { get => muxFormat; set => muxFormat = value; }
        public bool MuxFastStart { get => muxFastStart; set => muxFastStart = value; }
        public string MuxSetJson { get => muxSetJson; set => muxSetJson = value; }
        public bool BinaryMerge { get => binaryMerge; set => binaryMerge = value; }
        public int TimeOut { get => timeOut; set => timeOut = value; }
        public static double DownloadedSize { get => downloadedSize; set => downloadedSize = value; }
        public static bool HasSetDir { get => hasSetDir; set => hasSetDir = value; }
        public bool NoMerge { get => noMerge; set => noMerge = value; }
        public static int CalcTime { get => calcTime; set => calcTime = value; }
        public static int Count { get => count; set => count = value; }
        public static int PartsCount { get => partsCount; set => partsCount = value; }
        public static bool DisableIntegrityCheck { get => disableIntegrityCheck; set => disableIntegrityCheck = value; }

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
                LOGGER.WriteLine("Has External Audio Track");
                LOGGER.PrintLine("识别到外挂音频轨道", LOGGER.Warning);
            }
            catch (Exception) {}
            try
            {
                if (initJson["m3u8Info"]["sub"].ToString() != "")
                    externalSub = true;
                externalSubUrl = initJson["m3u8Info"]["sub"].ToString();
                LOGGER.WriteLine("Has External Subtitle Track");
                LOGGER.PrintLine("识别到外挂字幕轨道", LOGGER.Warning);
            }
            catch (Exception) { }
            total = Convert.ToInt32(segCount);
            PartsCount = parts.Count;
            segsPadZero = string.Empty.PadRight(oriCount.Length, '0');
            partsPadZero = string.Empty.PadRight(Convert.ToString(parts.Count).Length, '0');
            CancellationTokenSource cts = new CancellationTokenSource();

            //是直播视频
            if (isVOD == "False")
            {
                return;
            }

            Global.ShouldStop = false; //是否该停止下载

            //监控文件夹变化
            if (!Directory.Exists(DownDir))
                Directory.CreateDirectory(DownDir); //新建文件夹  
            Watcher watcher = new Watcher(DownDir);
            watcher.Total = total;
            watcher.PartsCount = PartsCount;
            watcher.WatcherStrat();

            //监控文件夹大小变化 via https://stackoverflow.com/questions/2869561/what-is-the-fastest-way-to-calculate-a-windows-folders-size/12665904#12665904
            System.Timers.Timer timer = new System.Timers.Timer(1000 * CalcTime);   //实例化Timer类
            timer.AutoReset = true;
            timer.Enabled = true;
            //Scripting.FileSystemObject fso = new Scripting.FileSystemObject();
            //Scripting.Folder folder  = fso.GetFolder(DownDir);
            timer.Elapsed += delegate
            {
                //采用COM组件获取文件夹的大小，需要引入 "Microsoft Scripting Runtime" 
                //double sizeInBytes = folder.Size;
                Console.SetCursorPosition(0, 1);
                //Console.WriteLine("Speed: " + Global.FormatFileSize((sizeInBytes - lastSizeInBytes) / calcTime) + " / s               ");
                Console.Write("Speed: " + Global.FormatFileSize((Global.BYTEDOWN) / CalcTime) + " / s".PadRight(70));

                if (Global.HadReadInfo && Global.BYTEDOWN <= Global.STOP_SPEED * 1024 * CalcTime) 
                {
                    stopCount++;
                    Console.SetCursorPosition(0, 1);
                    Console.Write("Speed: " + Global.FormatFileSize((Global.BYTEDOWN) / CalcTime) + " / s [" + stopCount + "]".PadRight(70));

                    if (stopCount >= 12)
                    {
                        Global.ShouldStop = true;
                        cts.Cancel();
                        GC.Collect();
                        return;
                    }
                }
                else
                {
                    stopCount = 0;
                    Global.BYTEDOWN = 0;

                }
            };

            //开始调用下载
            LOGGER.WriteLine("Start Downloading");
            LOGGER.PrintLine("开始下载文件", LOGGER.Warning);

            //下载MAP文件（若有）
            try
            {
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
                LOGGER.PrintLine("下载MAP文件...");
                sd.Down();  //开始下载
            }
            catch (Exception e)
            {
                //LOG.WriteLineError(e.ToString());
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
                    if (isVTT == false && sd.FileUrl.Trim('\"').EndsWith(".vtt"))
                        isVTT = true;
                    sd.Method = firstSeg["method"].Value<string>();
                    if (sd.Method != "NONE")
                    {
                        sd.Key = firstSeg["key"].Value<string>();
                        sd.Iv = firstSeg["iv"].Value<string>();
                    }
                    try
                    {
                        sd.ExpectByte = firstSeg["expectByte"].Value<long>();
                    }
                    catch (Exception) { }
                    try
                    {
                        sd.StartByte = firstSeg["startByte"].Value<long>();
                    }
                    catch (Exception) { }
                    sd.Headers = Headers;
                    sd.SavePath = DownDir + "\\Part_" + 0.ToString(partsPadZero) + "\\" + firstSeg["index"].Value<int>().ToString(segsPadZero) + ".tsdownloading";
                    if (File.Exists(sd.SavePath))
                        File.Delete(sd.SavePath);
                    LOGGER.PrintLine("下载首分片...");
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
                LOGGER.PrintLine("等待下载完成...", LOGGER.Warning);
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
                                if (isVTT == false && sd.FileUrl.Trim('\"').EndsWith(".vtt"))
                                isVTT = true;
                            sd.Method = info["method"].Value<string>();
                            if (sd.Method != "NONE")
                            {
                                sd.Key = info["key"].Value<string>();
                                sd.Iv = info["iv"].Value<string>();
                            }
                            try
                            {
                                sd.ExpectByte = info["expectByte"].Value<long>();
                            }
                            catch (Exception) { }
                            try
                            {
                                sd.StartByte = info["startByte"].Value<long>();
                            }
                            catch (Exception) { }
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

            //监控文件夹大小变化的收尾工作
            timer.Enabled = false;
            timer.Close();
            // cleanup COM
            //System.Runtime.InteropServices.Marshal.ReleaseComObject(folder);
            //System.Runtime.InteropServices.Marshal.ReleaseComObject(fso);

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
                LOGGER.PrintLine("完成数量 " + tsCount + " / " + segCount);
                LOGGER.WriteLine("Downloaded " + tsCount + " of " + segCount);
                if (Count <= RetryCount)
                {
                    Count++;
                    LOGGER.WriteLine("Retry Count " + Count + " / " + RetryCount);
                    LOGGER.PrintLine("重试次数 " + Count + " / " + RetryCount, LOGGER.Warning);
                    Thread.Sleep(3000);
                    GC.Collect(); //垃圾回收
                    DoDownload();
                }
            }
            else  //开始合并
            {
                LOGGER.PrintLine("已下载完毕" + (DisableIntegrityCheck ? "(已关闭完整性检查)" : ""));
                Console.WriteLine();
                if (NoMerge == false)
                {
                    string exePath = Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName);
                    string driverName = exePath.Remove(exePath.IndexOf(':'));
                    Console.Title = "Done.";
                    LOGGER.WriteLine("Start Merging");
                    LOGGER.PrintLine("开始合并分片...", LOGGER.Warning);
                    //VTT字幕
                    if (isVTT == true)
                        MuxFormat = "vtt";
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
                            LOGGER.PrintLine("二进制合并...请耐心等待");
                            MuxFormat = "ts";
                            if (Global.AUDIO_TYPE != "")
                                MuxFormat = Global.AUDIO_TYPE;
                            Global.CombineMultipleFilesIntoSingleFile(Global.GetFiles(DownDir + "\\Part_0", ".ts"), FFmpeg.OutPutPath + $".{MuxFormat}");
                        }
                        else
                        {
                            if (Global.VIDEO_TYPE != "DV") //不是杜比视界
                            {
                                LOGGER.FFmpegCorsorIndex = LOGGER.CursorIndex;
                                //检测是否为MPEG-TS封装，不是的话就转换为TS封装
                                foreach (string s in Global.GetFiles(DownDir + "\\Part_0", ".ts"))
                                {
                                    //跳过有MAP的情况
                                    if (!File.Exists(DownDir + "\\Part_0\\!MAP.ts") && !FFmpeg.CheckMPEGTS(s))
                                    {
                                        //转换
                                        LOGGER.PrintLine("将文件转换到 MPEG-TS 封装：" + Path.GetFileName(s));
                                        LOGGER.WriteLine("Re-Mux file to MPEG-TS：" + Path.GetFileName(s));
                                        FFmpeg.ConvertToMPEGTS(s);
                                    }
                                }

                                //分片过多的情况
                                if (tsCount >= 1800)
                                {
                                    LOGGER.WriteLine("Too Many Segs, Partial Merging");
                                    LOGGER.PrintLine("分片大于1800个，执行分部合并中...", LOGGER.Warning);
                                    Global.PartialCombineMultipleFiles(Global.GetFiles(DownDir + "\\Part_0", ".ts"));
                                }

                                if (Global.AUDIO_TYPE != "")
                                    MuxFormat = Global.AUDIO_TYPE;

                                LOGGER.PrintLine("使用ffmpeg合并...请耐心等待");
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
                                LOGGER.PrintLine("杜比视界内容，使用二进制合并...请耐心等待");
                                Global.CombineMultipleFilesIntoSingleFile(Global.GetFiles(DownDir + "\\Part_0", ".ts"), FFmpeg.OutPutPath + ".mp4");
                            }
                        }

                        LOGGER.WriteLine("Task Done"
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
                            LOGGER.PrintLine("开始下载外挂音频...", LOGGER.Warning);
                            Parser parser = new Parser();
                            parser.Headers = Headers; //继承Header
                            parser.BaseUrl = "";
                            parser.M3u8Url = externalAudioUrl;
                            parser.DownName = DownName + "(Audio)";
                            parser.DownDir = Path.Combine(Path.GetDirectoryName(DownDir), parser.DownName);
                            LOGGER.WriteLine("Start Parsing " + externalAudioUrl);
                            LOGGER.WriteLine("Downloading External Audio Track");
                            DownName = DownName + "(Audio)";
                            fflogName = "_ffreport(Audio).log";
                            DownDir = parser.DownDir;
                            parser.Parse();  //开始解析
                            Thread.Sleep(1000);
                            LOGGER.CursorIndex = 5;
                            DoDownload();
                        }
                        if (externalSub)  //下载独立字幕
                        {
                            externalSub = false;
                            DownloadedSize = 0;
                            Global.WriteInit();
                            LOGGER.PrintLine("开始下载外挂字幕...", LOGGER.Warning);
                            Parser parser = new Parser();
                            parser.Headers = Headers; //继承Header
                            parser.BaseUrl = "";
                            parser.M3u8Url = externalSubUrl;
                            parser.DownName = DownName + "(Subtitle)";
                            parser.DownDir = Path.Combine(Path.GetDirectoryName(DownDir), parser.DownName);
                            LOGGER.WriteLine("Start Parsing " + externalSubUrl);
                            LOGGER.WriteLine("Downloading External Subtitle Track");
                            DownName = DownName + "(Subtitle)";
                            fflogName = "_ffreport(Subtitle).log";
                            DownDir = parser.DownDir;
                            parser.Parse();  //开始解析
                            Thread.Sleep(1000);
                            LOGGER.CursorIndex = 5;
                            DoDownload();
                        }
                        LOGGER.PrintLine("任务结束", LOGGER.Warning);
                        Console.CursorVisible = true;
                        Environment.Exit(0);  //正常退出程序
                        Console.Clear();
                        return;
                    }

                    FFmpeg.OutPutPath = Path.Combine(Directory.GetParent(DownDir).FullName, DownName);
                    FFmpeg.ReportFile = driverName + "\\:" + exePath.Remove(0, exePath.IndexOf(':') + 1).Replace("\\", "/") + "/Logs/" + Path.GetFileNameWithoutExtension(LOGGER.LOGFILE) + fflogName;
                    
                    //合并分段
                    LOGGER.PrintLine("合并分段中...");
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
                        LOGGER.PrintLine("二进制合并...请耐心等待");
                        MuxFormat = "ts";
                        Global.CombineMultipleFilesIntoSingleFile(Global.GetFiles(DownDir, ".ts"), FFmpeg.OutPutPath + $".{MuxFormat}");
                    }
                    else
                    {
                        if (Global.VIDEO_TYPE != "DV")  //不是爱奇艺杜比视界
                        {
                            LOGGER.FFmpegCorsorIndex = LOGGER.CursorIndex;
                            //检测是否为MPEG-TS封装，不是的话就转换为TS封装
                            foreach (string s in Global.GetFiles(DownDir, ".ts"))
                            {
                                //跳过有MAP的情况
                                if (!File.Exists(DownDir + "\\!MAP.ts") && !FFmpeg.CheckMPEGTS(s))
                                {
                                    //转换
                                    LOGGER.PrintLine("将文件转换到 MPEG-TS 封装：" + Path.GetFileName(s));
                                    LOGGER.WriteLine("Re-Mux file to MPEG-TS：" + Path.GetFileName(s));
                                    FFmpeg.ConvertToMPEGTS(s);
                                }
                            }

                            if (Global.AUDIO_TYPE != "")
                                MuxFormat = Global.AUDIO_TYPE;

                            LOGGER.PrintLine("使用ffmpeg合并...请耐心等待");
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
                            LOGGER.PrintLine("杜比视界内容，使用二进制合并...请耐心等待");
                            Global.CombineMultipleFilesIntoSingleFile(Global.GetFiles(DownDir, ".ts"), FFmpeg.OutPutPath + ".mp4");
                        }
                    }

                    LOGGER.WriteLine("Task Done"
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
                        LOGGER.PrintLine("开始下载外挂音频...", LOGGER.Warning);
                        Parser parser = new Parser();
                        parser.Headers = Headers; //继承Header
                        parser.BaseUrl = "";
                        parser.M3u8Url = externalAudioUrl;
                        parser.DownName = DownName + "(Audio)";
                        parser.DownDir = Path.Combine(Path.GetDirectoryName(DownDir), parser.DownName);
                        LOGGER.WriteLine("Start Parsing " + externalAudioUrl);
                        LOGGER.WriteLine("Downloading External Audio Track");
                        DownName = DownName + "(Audio)";
                        fflogName = "_ffreport(Audio).log";
                        DownDir = parser.DownDir;
                        parser.Parse();  //开始解析
                        Thread.Sleep(1000);
                        LOGGER.CursorIndex = 5;
                        DoDownload();
                    }
                    if (externalSub)  //下载独立字幕
                    {
                        externalSub = false;
                        DownloadedSize = 0;
                        Global.WriteInit();
                        LOGGER.PrintLine("开始下载外挂字幕...", LOGGER.Warning);
                        Parser parser = new Parser();
                        parser.Headers = Headers; //继承Header
                        parser.BaseUrl = "";
                        parser.M3u8Url = externalSubUrl;
                        parser.DownName = DownName + "(Subtitle)";
                        parser.DownDir = Path.Combine(Path.GetDirectoryName(DownDir), parser.DownName);
                        LOGGER.WriteLine("Start Parsing " + externalSubUrl);
                        LOGGER.WriteLine("Downloading External Subtitle Track");
                        DownName = DownName + "(Subtitle)";
                        fflogName = "_ffreport(Subtitle).log";
                        DownDir = parser.DownDir;
                        parser.Parse();  //开始解析
                        Thread.Sleep(1000);
                        LOGGER.CursorIndex = 5;
                        DoDownload();
                    }
                    LOGGER.PrintLine("任务结束", LOGGER.Warning);
                    Console.CursorVisible = true;
                    Environment.Exit(0);  //正常退出程序

                    Console.Clear();
                }
                else
                {
                    Console.Title = "Done.";
                    LOGGER.PrintLine("任务结束", LOGGER.Warning);
                    LOGGER.WriteLine("Task Done"
                        + "\r\n\r\nTask End: " + DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss"));
                    Environment.Exit(0);  //正常退出程序
                }
            }
        }
    }
}
