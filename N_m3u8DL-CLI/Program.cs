using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace N_m3u8DL_CLI.NetCore
{
    class Program
    {
        public delegate bool ControlCtrlDelegate(int CtrlType);
        [DllImport("kernel32.dll")]
        private static extern bool SetConsoleCtrlHandler(ControlCtrlDelegate HandlerRoutine, bool Add);
        private static ControlCtrlDelegate cancelHandler = new ControlCtrlDelegate(HandlerRoutine);
        public static bool HandlerRoutine(int CtrlType)
        {
            switch (CtrlType)
            {
                case 0:
                    LOGGER.WriteLine(strings.ExitedCtrlC
                    + "\r\n\r\nTask End: " + DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss")); //Ctrl+C关闭
                    break;
                case 2:
                    LOGGER.WriteLine(strings.ExitedForce
                    + "\r\n\r\nTask End: " + DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss")); //按控制台关闭按钮关闭
                    break;
            }
            return false;
        }

        private static bool ValidateServerCertificate(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            return true;
        }


        static void Main(string[] args)
        {
            SetConsoleCtrlHandler(cancelHandler, true);
            ServicePointManager.ServerCertificateValidationCallback = ValidateServerCertificate;
            string loc = "en-US";
            string currLoc = Thread.CurrentThread.CurrentUICulture.Name;
            if (currLoc == "zh-TW" || currLoc == "zh-HK" || currLoc == "zh-MO") loc = "zh-TW";
            else if (currLoc == "zh-CN" || currLoc == "zh-SG") loc = "zh-CN";
            //设置语言
            CultureInfo.DefaultThreadCurrentCulture = new CultureInfo(loc);
            Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo(loc);

            try
            {
                //goto httplitsen;
                //当前程序路径（末尾有\）
                string CURRENT_PATH = Directory.GetCurrentDirectory();
                string fileName = "";

                //寻找ffmpeg.exe
                if (File.Exists("ffmpeg.exe"))
                {
                    FFmpeg.FFMPEG_PATH = Path.Combine(Environment.CurrentDirectory, "ffmpeg.exe");
                }
                else if (File.Exists(Path.Combine(Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName), "ffmpeg.exe")))
                {
                    FFmpeg.FFMPEG_PATH = Path.Combine(Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName), "ffmpeg.exe");
                }
                else
                {
                    try
                    {
                        string[] EnvironmentPath = Environment.GetEnvironmentVariable("Path").Split(';');
                        foreach (var de in EnvironmentPath)
                        {
                            if (File.Exists(Path.Combine(de.Trim('\"').Trim(), "ffmpeg.exe")))
                            {
                                FFmpeg.FFMPEG_PATH = Path.Combine(de.Trim('\"').Trim(), "ffmpeg.exe");
                                goto HasFFmpeg;
                            }
                        }
                    }
                    catch (Exception)
                    {
                        ;
                    }

                    Console.BackgroundColor = ConsoleColor.Red; //设置背景色
                    Console.ForegroundColor = ConsoleColor.White; //设置前景色，即字体颜色
                    Console.WriteLine(strings.ffmpegLost);
                    Console.ResetColor(); //将控制台的前景色和背景色设为默认值
                    Console.WriteLine(strings.ffmpegTip);
                    Console.WriteLine();
                    Console.WriteLine("http://ffmpeg.org/download.html#build-windows");
                    Console.WriteLine();
                    Console.WriteLine(strings.pressAnyKeyExit);
                    Console.ReadKey();
                    Environment.Exit(-1);
                }

            HasFFmpeg:
                Global.WriteInit();
                if (!File.Exists(Path.Combine(Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName), "NO_UPDATE"))) 
                {
                    Thread checkUpdate = new Thread(() =>
                    {
                        Global.CheckUpdate();
                    });
                    checkUpdate.IsBackground = true;
                    checkUpdate.Start();
                }

                int maxThreads = Environment.ProcessorCount;
                int minThreads = 16;
                int retryCount = 15;
                int timeOut = 10; //默认10秒
                string baseUrl = "";
                string reqHeaders = "";
                string keyFile = "";
                string keyBase64 = "";
                string keyIV = "";
                string muxSetJson = "MUXSETS.json";
                string workDir = CURRENT_PATH + "\\Downloads";
                bool muxFastStart = false;
                bool delAfterDone = false;
                bool parseOnly = false;
                bool noMerge = false;

                /******************************************************/
                ServicePointManager.DefaultConnectionLimit = 1024;
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Ssl3
                                       | SecurityProtocolType.Tls
                                       | (SecurityProtocolType)0x300 //Tls11  
                                       | (SecurityProtocolType)0xC00; //Tls12  
                /******************************************************/

                if (File.Exists(Path.Combine(Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName), "headers.txt")))
                    reqHeaders = File.ReadAllText(Path.Combine(Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName), "headers.txt"));

                //分析命令行参数
                parseArgs:
                var arguments = CommandLineArgumentParser.Parse(args);
                if (args.Length == 1 && args[0] == "--help") 
                {
                    Console.WriteLine(strings.helpInfo);
                    return;
                }
                if (arguments.Has("--enableDelAfterDone"))
                {
                    delAfterDone = true;
                }
                if (arguments.Has("--enableParseOnly"))
                {
                    parseOnly = true;
                }
                if (arguments.Has("--enableBinaryMerge"))
                {
                    DownloadManager.BinaryMerge = true;
                }
                if (arguments.Has("--disableDateInfo"))
                {
                    FFmpeg.WriteDate = false;
                }
                if (arguments.Has("--noMerge"))
                {
                    noMerge = true;
                }
                if (arguments.Has("--noProxy"))
                {
                    Global.NoProxy = true;
                }
                if (arguments.Has("--proxyAddress"))
                {
                    var proxy = arguments.Get("--proxyAddress").Next.ToString();
                    if (proxy.StartsWith("http://"))
                        Global.UseProxyAddress = proxy;
                }
                if (arguments.Has("--headers"))
                {
                    reqHeaders = arguments.Get("--headers").Next;
                }
                if (arguments.Has("--enableMuxFastStart"))
                {
                    muxFastStart = true;
                }
                if (arguments.Has("--disableIntegrityCheck"))
                {
                    DownloadManager.DisableIntegrityCheck = true;
                }
                if (arguments.Has("--enableAudioOnly"))
                {
                    Global.VIDEO_TYPE = "IGNORE";
                }
                if (arguments.Has("--muxSetJson"))
                {
                    muxSetJson = arguments.Get("--muxSetJson").Next;
                }
                if (arguments.Has("--workDir"))
                {
                    workDir = arguments.Get("--workDir").Next;
                    DownloadManager.HasSetDir = true;
                }
                if (arguments.Has("--saveName"))
                {
                    fileName = Global.GetValidFileName(arguments.Get("--saveName").Next);
                }
                if (arguments.Has("--useKeyFile"))
                {
                    if (File.Exists(arguments.Get("--useKeyFile").Next))
                        keyFile = arguments.Get("--useKeyFile").Next;
                }
                if (arguments.Has("--useKeyBase64"))
                {
                    keyBase64 = arguments.Get("--useKeyBase64").Next;
                }
                if (arguments.Has("--useKeyIV"))
                {
                    keyIV = arguments.Get("--useKeyIV").Next;
                }
                if (arguments.Has("--stopSpeed"))
                {
                    Global.STOP_SPEED = Convert.ToInt64(arguments.Get("--stopSpeed").Next);
                }
                if (arguments.Has("--maxSpeed"))
                {
                    Global.MAX_SPEED = Convert.ToInt64(arguments.Get("--maxSpeed").Next);
                }
                if (arguments.Has("--baseUrl"))
                {
                    baseUrl = arguments.Get("--baseUrl").Next;
                }
                if (arguments.Has("--maxThreads"))
                {
                    maxThreads = Convert.ToInt32(arguments.Get("--maxThreads").Next);
                }
                if (arguments.Has("--minThreads"))
                {
                    minThreads = Convert.ToInt32(arguments.Get("--minThreads").Next);
                }
                if (arguments.Has("--retryCount"))
                {
                    retryCount = Convert.ToInt32(arguments.Get("--retryCount").Next);
                }
                if (arguments.Has("--timeOut"))
                {
                    timeOut = Convert.ToInt32(arguments.Get("--timeOut").Next);
                }
                if (arguments.Has("--liveRecDur"))
                {
                    //时间码
                    Regex reg2 = new Regex(@"(\d+):(\d+):(\d+)");
                    var t = arguments.Get("--liveRecDur").Next;
                    if (reg2.IsMatch(t))
                    {
                        int HH = Convert.ToInt32(reg2.Match(t).Groups[1].Value);
                        int MM = Convert.ToInt32(reg2.Match(t).Groups[2].Value);
                        int SS = Convert.ToInt32(reg2.Match(t).Groups[3].Value);
                        HLSLiveDownloader.REC_DUR_LIMIT = SS + MM * 60 + HH * 60 * 60;
                    }
                }
                if (arguments.Has("--downloadRange"))
                {
                    string p = arguments.Get("--downloadRange").Next;

                    if (p.Contains(":"))
                    {
                        //时间码
                        Regex reg2 = new Regex(@"((\d+):(\d+):(\d+))?-((\d+):(\d+):(\d+))?");
                        if (reg2.IsMatch(p))
                        {
                            Parser.DurStart = reg2.Match(p).Groups[1].Value;
                            Parser.DurEnd = reg2.Match(p).Groups[5].Value;
                            if (Parser.DurEnd == "00:00:00") Parser.DurEnd = "";
                            Parser.DelAd = false;
                        }
                    }
                    else
                    {
                        //数字
                        Regex reg = new Regex(@"(\d*)-(\d*)");
                        if (reg.IsMatch(p))
                        {
                            if (!string.IsNullOrEmpty(reg.Match(p).Groups[1].Value))
                            {
                                Parser.RangeStart = Convert.ToInt32(reg.Match(p).Groups[1].Value);
                                Parser.DelAd = false;
                            }
                            if (!string.IsNullOrEmpty(reg.Match(p).Groups[2].Value))
                            {
                                Parser.RangeEnd = Convert.ToInt32(reg.Match(p).Groups[2].Value);
                                Parser.DelAd = false;
                            }
                        }
                    }
                }

                //如果只有URL，没有附加参数，则尝试解析配置文件
                if (args.Length == 1 || (args.Length == 3 && args[1].ToLower() == "--savename"))
                {
                    if (File.Exists(Path.Combine(Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName), "N_m3u8DL-CLI.args.txt")))
                    {
                        if (args.Length == 3)
                        {
                            args = Global.ParseArguments($"\"{args[0]}\" {args[1]} {args[2]} " + File.ReadAllText(Path.Combine(Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName), "N_m3u8DL-CLI.args.txt"))).ToArray();  //解析命令行
                        }
                        else
                        {
                            args = Global.ParseArguments($"\"{args[0]}\" " + File.ReadAllText(Path.Combine(Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName), "N_m3u8DL-CLI.args.txt"))).ToArray();  //解析命令行
                        }
                        goto parseArgs;
                    }
                }

                //ReadLine字数上限
                Stream steam = Console.OpenStandardInput();
                Console.SetIn(new StreamReader(steam, Encoding.Default, false, 5000));
                int inputRetryCount = 20;
            input:
                string testurl = "";


                //重试太多次，退出
                if (inputRetryCount == 0)
                    Environment.Exit(-1);

                if (args.Length > 0)
                    testurl = args[0];
                else
                {
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.Write("N_m3u8DL-CLI");
                    Console.ResetColor();
                    Console.Write(" > ");

                    args = Global.ParseArguments(Console.ReadLine()).ToArray();  //解析命令行
                    Console.Clear();
                    Global.WriteInit();
                    goto parseArgs;
                }

                if (fileName == "")
                    fileName = Global.GetUrlFileName(testurl) + "_" + DateTime.Now.ToString("yyyyMMddHHmmss");


                if (testurl.Contains("twitcasting") && testurl.Contains("/fmp4/"))
                {
                    DownloadManager.BinaryMerge = true;
                }

                string m3u8Content = string.Empty;
                bool isVOD = true;

                //避免文件路径过长
                if (workDir.Length >= 200)
                {
                    //目录不能随便改 直接抛出异常
                    throw new Exception("保存目录过长!");
                }
                else if (workDir.Length + fileName.Length >= 200)
                {
                    //尝试缩短文件名
                    while (workDir.Length + fileName.Length >= 200)
                    {
                        fileName = fileName.Substring(0, fileName.Length - 1);
                    }
                }

                //开始解析

                LOGGER.PrintLine($"{strings.fileName}{fileName}");
                LOGGER.PrintLine($"{strings.savePath}{Path.GetDirectoryName(Path.Combine(workDir, fileName))}");

                Parser parser = new Parser();
                parser.DownName = fileName;
                parser.DownDir = Path.Combine(workDir, parser.DownName);
                parser.M3u8Url = testurl;
                parser.KeyBase64 = keyBase64;
                parser.KeyIV = keyIV;
                parser.KeyFile = keyFile;
                if (baseUrl != "")
                    parser.BaseUrl = baseUrl;
                parser.Headers = reqHeaders;
                string exePath = Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName);
                LOGGER.LOGFILE = Path.Combine(exePath, "Logs", DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss-fff") + ".log");
                LOGGER.InitLog();
                LOGGER.WriteLine(strings.startParsing + testurl);
                LOGGER.PrintLine(strings.startParsing + " " + testurl, LOGGER.Warning);
                if (testurl.EndsWith(".json") && File.Exists(testurl))  //可直接跳过解析
                {
                    if (!Directory.Exists(Path.Combine(workDir, fileName)))//若文件夹不存在则新建文件夹   
                        Directory.CreateDirectory(Path.Combine(workDir, fileName)); //新建文件夹  
                    File.Copy(testurl, Path.Combine(Path.Combine(workDir, fileName), "meta.json"), true);
                }
                else
                {
                    parser.Parse();  //开始解析
                }

                //仅解析模式
                if (parseOnly)
                {
                    LOGGER.PrintLine(strings.parseExit);
                    Environment.Exit(0);
                }

                if (File.Exists(Path.Combine(Path.Combine(workDir, fileName), "meta.json")))
                {
                    JObject initJson = JObject.Parse(File.ReadAllText(Path.Combine(Path.Combine(workDir, fileName), "meta.json")));
                    isVOD = Convert.ToBoolean(initJson["m3u8Info"]["vod"].ToString());
                    //传给Watcher总时长
                    Watcher.TotalDuration = initJson["m3u8Info"]["totalDuration"].Value<double>();
                    LOGGER.PrintLine($"{strings.fileDuration}{Global.FormatTime((int)Watcher.TotalDuration)}");
                    LOGGER.PrintLine(strings.segCount + initJson["m3u8Info"]["originalCount"].Value<int>()
                        + $", {strings.selectedCount}" + initJson["m3u8Info"]["count"].Value<int>());
                }
                else
                {
                    DirectoryInfo directoryInfo = new DirectoryInfo(Path.Combine(workDir, fileName));
                    directoryInfo.Delete(true);
                    LOGGER.PrintLine(strings.InvalidUri, LOGGER.Error);
                    inputRetryCount--;
                    goto input;
                }

                //点播
                if (isVOD == true)
                {
                    ServicePointManager.DefaultConnectionLimit = 10000;
                    DownloadManager md = new DownloadManager();
                    md.DownDir = parser.DownDir;
                    md.Headers = reqHeaders;
                    md.Threads = Environment.ProcessorCount;
                    if (md.Threads > maxThreads)
                        md.Threads = maxThreads;
                    if (md.Threads < minThreads)
                        md.Threads = minThreads;
                    if (File.Exists("minT.txt"))
                    {
                        int t = Convert.ToInt32(File.ReadAllText("minT.txt"));
                        if (md.Threads <= t)
                            md.Threads = t;
                    }
                    md.TimeOut = timeOut * 1000;
                    md.NoMerge = noMerge;
                    md.DownName = fileName;
                    md.DelAfterDone = delAfterDone;
                    md.MuxFormat = "mp4";
                    md.RetryCount = retryCount;
                    md.MuxSetJson = muxSetJson;
                    md.MuxFastStart = muxFastStart;
                    md.DoDownload();
                }
                //直播
                if (isVOD == false)
                {
                    LOGGER.WriteLine(strings.liveStreamFoundAndRecoding);
                    LOGGER.PrintLine(strings.liveStreamFoundAndRecoding);
                    //LOGGER.STOPLOG = true;  //停止记录日志
                    //开辟文件流，且不关闭。（便于播放器不断读取文件）
                    string LivePath = Path.Combine(Directory.GetParent(parser.DownDir).FullName
                        , DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss") + "_" + fileName + ".ts");
                    FileStream outputStream = new FileStream(LivePath, FileMode.Append);

                    HLSLiveDownloader live = new HLSLiveDownloader();
                    live.DownDir = parser.DownDir;
                    live.Headers = reqHeaders;
                    live.LiveStream = outputStream;
                    live.LiveFile = LivePath;
                    live.TimerStart();  //开始录制
                    Console.ReadKey();
                }

                //监听测试
                /*httplitsen:
                HTTPListener.StartListening();*/
                LOGGER.WriteLineError(strings.downloadFailed);
                LOGGER.PrintLine(strings.downloadFailed, LOGGER.Error);
                Thread.Sleep(3000);
                Environment.Exit(-1);
                //Console.Write("按任意键继续..."); Console.ReadKey(); return;
            }
            catch (Exception ex)
            {
                LOGGER.PrintLine(ex.Message, LOGGER.Error);
            }
        }
    }
}
