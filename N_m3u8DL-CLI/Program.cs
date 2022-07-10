using CommandLine;
using CommandLine.Text;
using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using Microsoft.Win32;

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
            /******************************************************/
            SetConsoleCtrlHandler(cancelHandler, true);
            ServicePointManager.ServerCertificateValidationCallback = ValidateServerCertificate;
            ServicePointManager.DefaultConnectionLimit = 1024;
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Ssl3
                                   | SecurityProtocolType.Tls
                                   | (SecurityProtocolType)0x300 //Tls11  
                                   | (SecurityProtocolType)0xC00; //Tls12  
            /******************************************************/

            try
            {
                string loc = "en-US";
                string currLoc = Thread.CurrentThread.CurrentUICulture.Name;
                if (currLoc == "zh-TW" || currLoc == "zh-HK" || currLoc == "zh-MO") loc = "zh-TW";
                else if (currLoc == "zh-CN" || currLoc == "zh-SG") loc = "zh-CN";
                //设置语言
                CultureInfo.DefaultThreadCurrentCulture = new CultureInfo(loc);
                Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo(loc);
            }
            catch (Exception) {; }

            // 处理m3u8dl URL协议
            if (args.Length == 1)
            {
                if (args[0].ToLower().StartsWith("m3u8dl:"))
                {
                    var base64 = args[0].Replace("m3u8dl://", "").Replace("m3u8dl:", "");
                    var cmd = "";
                    try { cmd = Encoding.UTF8.GetString(Convert.FromBase64String(base64)); }
                    catch (FormatException) { cmd = Encoding.UTF8.GetString(Convert.FromBase64String(base64.TrimEnd('/'))); }
                    //修正参数转义符
                    cmd = cmd.Replace("\\\"", "\"");
                    //修正工作目录
                    Environment.CurrentDirectory = Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName);
                    args = Global.ParseArguments(cmd).ToArray();  //解析命令行
                }
                else if (args[0] == "--registerUrlProtocol")
                {
                    RequireElevated(string.Join(" ", args));
                    bool result = RegisterUriScheme("m3u8dl", Assembly.GetExecutingAssembly().Location);
                    Console.WriteLine(result ? strings.registerUrlProtocolSuccessful : strings.registerUrlProtocolFailed);
                    Environment.Exit(0);
                }
                else if (args[0] == "--unregisterUrlProtocol")
                {
                    RequireElevated(string.Join(" ", args));
                    bool result = UnregisterUriScheme("m3u8dl");
                    Console.WriteLine(result ? strings.unregisterUrlProtocolSuccessful : strings.unregisterUrlProtocolFailed);
                    Environment.Exit(0);
                }
            }

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
            if (!File.Exists(Path.Combine(Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName), "NO_UPDATE")))
            {
                Thread checkUpdate = new Thread(() =>
                {
                    Global.CheckUpdate();
                });
                checkUpdate.IsBackground = true;
                checkUpdate.Start();
            }

            //ReadLine字数上限
            Stream steam = Console.OpenStandardInput();
            Console.SetIn(new StreamReader(steam, Encoding.Default, false, 5000));

            if (args.Length == 0)
            {
                Global.WriteInit();
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.Write("N_m3u8DL-CLI");
                Console.ResetColor();
                Console.Write(" > ");

                var cmd = Console.ReadLine();
                if (string.IsNullOrEmpty(cmd)) Environment.Exit(0);
                args = Global.ParseArguments(cmd).ToArray();  //解析命令行
                Console.Clear();
            }
            //如果只有URL，没有附加参数，则尝试解析配置文件
            else if (args.Length == 1 || (args.Length == 3 && args[1].ToLower() == "--savename"))
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
                }
            }

            var cmdParser = new CommandLine.Parser(with => with.HelpWriter = null);
            var parserResult = cmdParser.ParseArguments<MyOptions>(args);

            //解析命令行
            parserResult
              .WithParsed(o => DoWork(o))
              .WithNotParsed(errs => DisplayHelp(parserResult, errs));
        }

        private static void DoWork(MyOptions o)
        {
            try
            {
                Global.WriteInit();
                //当前程序路径（末尾有\）
                string CURRENT_PATH = Directory.GetCurrentDirectory();
                string fileName = Global.GetValidFileName(o.SaveName);
                string reqHeaders = o.Headers;
                string muxSetJson = o.MuxSetJson ?? "MUXSETS.json";
                string workDir = CURRENT_PATH + "\\Downloads";
                string keyFile = "";
                string keyBase64 = "";
                string keyIV = "";
                string baseUrl = "";
                Global.STOP_SPEED = o.StopSpeed;
                Global.MAX_SPEED = o.MaxSpeed;
                if (!string.IsNullOrEmpty(o.UseKeyBase64)) keyBase64 = o.UseKeyBase64;
                if (!string.IsNullOrEmpty(o.UseKeyIV)) keyIV = o.UseKeyIV;
                if (!string.IsNullOrEmpty(o.BaseUrl)) baseUrl = o.BaseUrl;
                if (o.EnableBinaryMerge) DownloadManager.BinaryMerge = true;
                if (o.DisableDateInfo) FFmpeg.WriteDate = false;
                if (o.NoProxy) Global.NoProxy = true;
                if (o.DisableIntegrityCheck) DownloadManager.DisableIntegrityCheck = true;
                if (o.EnableAudioOnly) Global.VIDEO_TYPE = "IGNORE";
                if (!string.IsNullOrEmpty(o.WorkDir))
                {
                    workDir = Environment.ExpandEnvironmentVariables(o.WorkDir);
                    DownloadManager.HasSetDir = true;
                }
                //CHACHA20
                if (o.EnableChaCha20 && !string.IsNullOrEmpty(o.ChaCha20KeyBase64) && !string.IsNullOrEmpty(o.ChaCha20NonceBase64))
                {
                    Downloader.EnableChaCha20 = true;
                    Downloader.ChaCha20KeyBase64 = o.ChaCha20KeyBase64;
                    Downloader.ChaCha20NonceBase64 = o.ChaCha20NonceBase64;
                }

                //Proxy
                if (!string.IsNullOrEmpty(o.ProxyAddress))
                {
                    var proxy = o.ProxyAddress;
                    if (proxy.StartsWith("http://"))
                        Global.UseProxyAddress = proxy;
                    if (proxy.StartsWith("socks5://"))
                        Global.UseProxyAddress = proxy;
                }
                //Key
                if (!string.IsNullOrEmpty(o.UseKeyFile))
                {
                    if (File.Exists(o.UseKeyFile))
                        keyFile = o.UseKeyFile;
                }

                if (File.Exists(Path.Combine(Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName), "headers.txt")))
                    reqHeaders = File.ReadAllText(Path.Combine(Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName), "headers.txt"));

                if (!string.IsNullOrEmpty(o.LiveRecDur))
                {
                    //时间码
                    Regex reg2 = new Regex(@"(\d+):(\d+):(\d+)");
                    var t = o.LiveRecDur;
                    if (reg2.IsMatch(t))
                    {
                        int HH = Convert.ToInt32(reg2.Match(t).Groups[1].Value);
                        int MM = Convert.ToInt32(reg2.Match(t).Groups[2].Value);
                        int SS = Convert.ToInt32(reg2.Match(t).Groups[3].Value);
                        HLSLiveDownloader.REC_DUR_LIMIT = SS + MM * 60 + HH * 60 * 60;
                    }
                }
                if (!string.IsNullOrEmpty(o.DownloadRange))
                {
                    string p = o.DownloadRange;

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

                int inputRetryCount = 20;
            input:
                string testurl = o.Input;

                //重试太多次，退出
                if (inputRetryCount == 0)
                    Environment.Exit(-1);

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
                if (o.EnableParseOnly)
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
                    if (md.Threads > o.MaxThreads)
                        md.Threads = (int)o.MaxThreads;
                    if (md.Threads < o.MinThreads)
                        md.Threads = (int)o.MinThreads;
                    if (File.Exists("minT.txt"))
                    {
                        int t = Convert.ToInt32(File.ReadAllText("minT.txt"));
                        if (md.Threads <= t)
                            md.Threads = t;
                    }
                    md.TimeOut = (int)(o.TimeOut * 1000);
                    md.NoMerge = o.NoMerge;
                    md.DownName = fileName;
                    md.DelAfterDone = o.EnableDelAfterDone;
                    md.MuxFormat = "mp4";
                    md.RetryCount = (int)o.RetryCount;
                    md.MuxSetJson = muxSetJson;
                    md.MuxFastStart = o.EnableMuxFastStart;
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

        public static bool RegisterUriScheme(string scheme, string applicationPath)
        {
            try
            {
                using (var schemeKey = Registry.ClassesRoot.CreateSubKey(scheme, writable: true))
                {
                    schemeKey.SetValue("", "URL:m3u8DL Protocol");
                    schemeKey.SetValue("URL Protocol", "");
                    using (var defaultIconKey = schemeKey.CreateSubKey("DefaultIcon"))
                    {
                        defaultIconKey.SetValue("", $"\"{applicationPath}\",1");
                    }
                    using (var shellKey = schemeKey.CreateSubKey("shell"))
                    using (var openKey = shellKey.CreateSubKey("open"))
                    using (var commandKey = openKey.CreateSubKey("command"))
                    {
                        commandKey.SetValue("", $"\"{applicationPath}\" \"%1\"");
                        return true;
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }

            return false;
        }

        public static bool UnregisterUriScheme(string scheme)
        {
            try
            {
                Registry.ClassesRoot.DeleteSubKeyTree(scheme);
                return true;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }

            return false;
        }

        public static void RequireElevated(string cmd)
        {
            if (!UACHelper.UACHelper.IsElevated)
            {
                string[] arguments = Environment.GetCommandLineArgs();
                UACHelper.UACHelper.StartElevated(
                    new ProcessStartInfo(Assembly.GetExecutingAssembly().Location, cmd)
                );
                Environment.Exit(0);
            }
        }

        private static void DisplayHelp(ParserResult<MyOptions> result, IEnumerable<Error> errs)
        {
            var helpText = HelpText.AutoBuild(result, h =>
            {
                h.AdditionalNewLineAfterOption = false;
                h.Copyright = "\r\nUSAGE:\r\n\r\n  N_m3u8DL-CLI <URL|JSON|FILE> [OPTIONS]\r\n\r\nOPTIONS:";
                return HelpText.DefaultParsingErrorsHandler(result, h);
            }, e => e);
            Console.WriteLine(helpText);
        }
    }
}
