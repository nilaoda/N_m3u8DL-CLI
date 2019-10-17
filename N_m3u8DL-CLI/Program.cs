using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace N_m3u8DL_CLI.NetCore
{
    /// <summary>
    /// 2018年12月3日
    ///   - 通过监控文件夹的更改来营造下载进度
    ///   - 增加对EXT-X-DISCONTINUITY的处理（分部分处理，分别合并）
    ///   - 增加对腾讯视频HDR的支持（主要是EXT-X-MAP的处理）
    ///   - 增加对Master List的支持（默认最高画质）
    ///   - json文件UpdateTime属性值改为"o" 符合国际标准
    ///   - 按照EXT-X-DISCONTINUITY划分出的视频组采用COPY /B方式合并，最后用ffmpeg concat合并为单一文件
    /// 2018年12月5日
    ///   - 转换为.Net Core项目(放弃)
    /// 2018年12月10日
    ///   - 修改M3u8Do中的多线程下载，改为线程局部变量
    /// 2018年12月11日
    ///   - 修复BUG，处理拼接相对路径中含有冒号的情况
    /// 2018年12月13日
    ///   - 读写锁机制确保LOG正确写入
    ///   - 跳过优酷广告分片
    /// 2018年12月14日
    ///   - 如果Parts不等于1，就强制转换到MPEGTS封装
    ///   - 如果Parts不等于1，启动新线程合并
    ///   - 优化点播直播的判断
    ///   - 修复获取属性的BUG（由','分割字符串，codecs里也有','造成）
    ///   - baseurl增加冒号的拼接逻辑
    /// 2018年12月17日
    ///   - 支持本地m3u8+本地ts文件形式
    /// 2018年12月19日
    ///   - 支持 EXT-X-BYTERANGE 标签（点播）
    /// 2018年12月25日
    ///   - 修改判断直播与点播的逻辑
    ///   - 优酷 默认修改为 drm_type=3&drm_device=10
    ///   - HttpDownloadFileToBytes 支持解压Gzip压缩且不再依赖服务器返回的ContentLength（bug fixed）
    ///   - CombineURL 改用 Uri 类来拼接baseurl和url，普适性更强，无脑截取丢给它也可以拼接出正确的地址（bug fixed）
    /// 2018年12月26日
    ///   - 修复Bug，增加变量startIndex，使用 segIndex-startIndex 计算分段总数
    /// 2019年1月23日 
    ///   - parser规范化，使用Jobject构造Json文件（切记：使用 new 来清空对象，不要用Clear，否则会导致之前加入的对象被同时清空）
    ///   - 使用WebClient下载，并优化m3u8的Range处理
    /// 2019年1月24日
    ///   - 修复：在master列表检测时需重置Baseurl
    /// 2019年2月23日
    ///   - 优化下载
    ///   - 重试次数增加到5
    ///   - 完成后不显示进度
    ///   - 命令行支持自定义MuxFastStart
    /// 2019年3月8日
    ///   - 重写对linetv的key分析，比对重复
    /// 2019年3月11日
    ///   - 自动判断音轨决定是否加入-bsf:a aac_adtstoasc参数
    /// 2019年3月18日
    ///   - 固定几行UI，可显示下载速度以及进度(计算文件夹大小实现)
    ///   - 混流时寻找ddpAudio.txt里的杜比音轨路径，封装杜比音轨
    /// 2019年3月20日
    ///   - 通过Global.ShouldStop变量，完成了速度为零3次的自动杀进程功能(HTTP写入流也强行结束)
    /// 2019年3月25日
    ///   - 优化下载函数
    /// 2019年3月29日
    ///   - 0:a?
    ///   - 修复对#EXT-X-BYTERANGE的支持
    /// 2019年3月30日
    ///   - 删除Remove()函数，改为在Global.HttpDownloadFile()执行该逻辑
    /// 2019年3月31日
    ///   - Global.HttpDownloadFile()采用using包围
    ///   - 找不到ffmpeg报异常
    ///   - Log写入Command Line
    /// 2019年4月11日
    ///   - 支持爱奇艺杜比视界，并判断如果是杜比视界则采用二进制合并
    ///   - 暂时去掉分段检测TS封装
    /// 2019年4月12日
    ///   - 最低16线程 最高32
    ///   - 修复AAC滤镜识别
    ///   - 支持腾讯视频杜比视界
    /// 2019年4月13日
    ///   - 增加downLen和totalLen对比是否下载完全
    /// 2019年4月18日
    ///   - 命令行模式正式化，发布1.0版本
    /// 2019年4月24日
    ///   - 增加enableBinaryMerge选项
    ///   - 修复Bug
    /// 2019年4月30日
    ///   - 增加仅解析功能 --enableParseOnly
    ///   - 支持从已解析的meta.json文件中直接进行下载
    /// 2019年5月3日
    ///   - 可下载纯音频m3u8
    /// 2019年5月6日
    ///   - 修改速度计算方式（增加BYTE）
    ///   - 修复ContentLength引发的BUG
    /// 2019年6月5日
    ///   - 外部ddp逻辑优化
    ///   - 跳过已存在文件时防止被速度监控程序杀死
    ///   - 增加过多分片(>1800)合并逻辑
    /// 2019年6月6日
    ///   - 支持DMM视频网站m3u8下载
    ///   - 增加全局异常捕获
    ///   - ffmpeg合并时去掉-map 0:d，因为mp4容器不支持此类数据
    /// 2019年6月7日
    ///   - 支持删除混流的日期参数
    /// 2019年6月8日
    ///   - 通过request.ReadWriteTimeout解决不能及时重试的问题
    ///   - 下载失败后不会卡在按任意键继续
    ///   - 添加timeout参数
    /// 2019年6月9日
    ///   - 过滤m3u8内容中的空白行 
    ///   - 修复BUG(不该验证Status=200)
    ///   - 增加显示更多信息(百分比/已下载/估计大小/估计时长)
    ///   - 增加对优酷杜比视界的支持
    ///   - 优化判断杜比视界的逻辑
    /// 2019年6月10日
    ///   - 获取文件时排序，防止在网络驱动器中的致命BUG
    ///   - AllowAutoRedirect = true 去掉Get302函数
    ///   - 解决XP系统低版本.net框架的一个URL拼接bug
    ///   - 为兼容XP系统 使用Environment.SetEnvironmentVariable替代了StartInfo.Environment
    ///   - 修复获取属性值的一个bug
    /// 2019年6月12日
    ///   - 自动下载m3u8外挂音轨、字幕等
    /// 2019年6月14日
    ///   - 自动处理芒果TV请求头
    /// 2019年6月16日
    ///   - 为兼容XP做出调整(https安全协议 SecurityProtocol)
    /// 2019年6月17日
    ///   - 修复同名覆盖的BUG
    ///   - LOG写入正确的工作目录
    ///   - 修复下载额外字幕、音频时未能继承ReqHeaders的问题
    /// 2019年6月18日
    ///   - 添加图标
    ///   - 增加程序更新检测
    /// 2019年6月19日
    ///   - 修复升级BUG
    ///   - 自动下载更新
    /// 2019年6月23日
    ///   - LOG写入到程序EXE所在目录
    ///   - 环境变量检测BUG修复
    /// 2019年7月7日
    ///   - 芒果自动加Cookie
    ///   - 支持分段形式伪m3u8的正确合并
    /// 2019年7月8日
    ///   - 修改默认UA为 VLC/2.2.1 LibVLC/2.2.1 
    /// 2019年7月10日
    ///   - 支持气球云m3u8
    /// 2019年7月10日
    ///   - 修复获取属性值的BUG
    /// 2019年7月10日
    ///   - 支持阿里云大学m3u8
    /// 2019年7月23日
    ///   - 在TS格式检测中放行杜比视界视频
    ///   - 自动去除优酷视频的广告(当指定downloadRange时不会启动)
    ///   - 支持手动指定想要下载的内容(downloadRange)
    /// 2019年7月29日
    ///   - 自动修改为爱奇艺UA
    /// 2019年8月21日
    ///   - 增加originalCount属性，修复选取时间段后可能导致的合并顺序错乱问题
    ///   - 增加noMerge命令行参数
    ///   - 增加noProxy命令行参数
    /// 2019年8月22日
    ///   - 增加stopSpeed命令行参数
    ///   - Invalid Url至多提示20次
    /// 2019年8月28日
    ///   - 优化腾讯杜比视界的识别
    /// 2019年9月5日
    ///   - 更改输出信息，输出显示更多下载细节
    ///   - 可以识别单音轨，自动合并为指定格式
    ///   - 支持双击后输入命令
    ///   - 避免重试时再次检测视频
    ///   - 识别MPEG-TS封装时略过纯音频
    ///   - 会首先下载第一个分片用以读取信息
    ///   - 修复302状态码Baseurl错误的问题
    ///   - 修正流匹配的正则表达式
    /// 2019年9月8日
    ///   - 修复视频被识别为音频的BUG
    /// 2019年9月9日
    ///   - 如果Parts大于1，则强制进行MPEG-TS封装
    ///   - 修改Parts大于1时的下载逻辑，提升下载速度
    /// 2019年9月10日
    ///   - 修改读取视频信息的逻辑
    ///   - 优化直播下载的信息输出
    /// 2019年9月16日
    ///   - 修复下载外挂流时显示异常问题
    /// 2019年9月18日
    ///   - 每秒计算一次速度
    ///   - 下载首分片将不触发停速重试
    ///   - 加入全局限速功能
    /// 2019年9月27日
    ///   - 支持www.vlive.tv
    /// 2019年10月5日
    ///   - N_m3u8DL-CLI.args.txt
    ///   - 细节优化
    /// 2019年10月18日
    ///   - 去掉了优酷DRM设备参数更改
    /// </summary>
    /// 

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
                    LOGGER.WriteLine("Exited: Ctrl + C"
                    + "\r\n\r\nTask End: " + DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss")); //Ctrl+C关闭
                    Console.CursorVisible = true;
                    break;
                case 2:
                    LOGGER.WriteLine("Exited: Force"
                    + "\r\n\r\nTask End: " + DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss")); //按控制台关闭按钮关闭
                    Console.CursorVisible = true;
                    break;
            }
            return false;
        }

        static void Main(string[] args)
        {
            try
            {
                //goto httplitsen;
                //当前程序路径（末尾有\）
                string CURRENT_PATH = Directory.GetCurrentDirectory();
                string fileName = "";

                //寻找ffmpeg.exe
                if (!File.Exists("ffmpeg.exe"))
                {
                    try
                    {
                        string[] EnvironmentPath = Environment.GetEnvironmentVariable("Path").Split(';');
                        foreach (var de in EnvironmentPath)
                        {
                            if (File.Exists(Path.Combine(de.Trim('\"').Trim(), "ffmpeg.exe")))
                                goto HasFFmpeg;
                        }
                    }
                    catch (Exception)
                    {
                        ;
                    }

                    Console.BackgroundColor = ConsoleColor.Red; //设置背景色
                    Console.ForegroundColor = ConsoleColor.White; //设置前景色，即字体颜色
                    Console.WriteLine("在PATH和程序路径下找不到 ffmpeg.exe");
                    Console.ResetColor(); //将控制台的前景色和背景色设为默认值
                    Console.WriteLine("请下载ffmpeg.exe并把他放到程序同目录.");
                    Console.WriteLine();
                    Console.WriteLine("x86 https://ffmpeg.zeranoe.com/builds/win32/static/");
                    Console.WriteLine("x64 https://ffmpeg.zeranoe.com/builds/win64/static/");
                    Console.WriteLine();
                    Console.WriteLine("按任意键退出.");
                    Console.ReadKey();
                    Environment.Exit(-1);
                }

            HasFFmpeg:
                Global.WriteInit();
                Thread checkUpdate = new Thread(() =>
                  {
                      Global.CheckUpdate();
                  });
                checkUpdate.IsBackground = true;
                checkUpdate.Start();

                int maxThreads = Environment.ProcessorCount;
                int minThreads = 16;
                int retryCount = 15;
                int timeOut = 10; //默认10秒
                string baseUrl = "";
                string reqHeaders = "";
                string muxSetJson = "MUXSETS.json";
                string workDir = CURRENT_PATH + "\\Downloads";
                bool muxFastStart = false;
                bool binaryMerge = false;
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
                    Console.WriteLine(@"N_m3u8DL-CLI.exe <URL|File|JSON> [OPTIONS]  

    --workDir    Directory      设定程序工作目录
    --saveName   Filename       设定存储文件名(不包括后缀)
    --baseUrl    BaseUrl        设定Baseurl
    --headers    headers        设定请求头，格式 key:value 使用|分割不同的key&value
    --maxThreads Thread         设定程序的最大线程数(默认为32)
    --minThreads Thread         设定程序的最小线程数(默认为16)
    --retryCount Count          设定程序的重试次数(默认为15)
    --timeOut    Sec            设定程序网络请求的超时时间(单位为秒，默认为10秒)
    --muxSetJson File           使用外部json文件定义混流选项
    --downloadRange Range       仅下载视频的一部分分片或长度
    --stopSpeed  Number         当速度低于此值时，重试(单位为KB/s)
    --maxSpeed   Number         设置下载速度上限(单位为KB/s)
    --enableDelAfterDone        开启下载后删除临时文件夹的功能
    --enableMuxFastStart        开启混流mp4的FastStart特性
    --enableBinaryMerge         开启二进制合并分片
    --enableParseOnly           开启仅解析模式(程序只进行到meta.json)
    --enableAudioOnly           合并时仅封装音频轨道
    --disableDateInfo           关闭混流中的日期写入
    --noMerge                   禁用自动合并
    --noProxy                   不自动使用系统代理");
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
                    binaryMerge = true;
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
                if (arguments.Has("--headers"))
                {
                    reqHeaders = arguments.Get("--headers").Next;
                }
                if (arguments.Has("--enableMuxFastStart"))
                {
                    muxFastStart = true;
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
                    fileName = arguments.Get("--saveName").Next;
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
                if (args.Length == 1)
                {
                    if (File.Exists(Path.Combine(Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName), "N_m3u8DL-CLI.args.txt")))
                    {
                        args = Global.ParseArguments($"\"{args[0]}\"" + File.ReadAllText(Path.Combine(Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName), "N_m3u8DL-CLI.args.txt"))).ToArray();  //解析命令行
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
                    Console.CursorVisible = true;
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.Write("N_m3u8DL-CLI");
                    Console.ResetColor();
                    Console.Write(" > ");

                    args = Global.ParseArguments(Console.ReadLine()).ToArray();  //解析命令行
                    Global.WriteInit();
                    Console.CursorVisible = false;
                    goto parseArgs;
                }

                if (fileName == "")
                    fileName = Global.GetUrlFileName(testurl) + "_" + DateTime.Now.ToString("yyyyMMddHHmmss");


                //优酷DRM设备更改
                /*if (testurl.Contains("playlist/m3u8"))
                {
                    string drm_type = Global.GetQueryString("drm_type", testurl);
                    string drm_device = Global.GetQueryString("drm_device", testurl);
                    if (drm_type != "1")
                    {
                        testurl = testurl.Replace("drm_type=" + drm_type, "drm_type=1");
                    }
                    if (drm_device != "11")
                    {
                        testurl = testurl.Replace("drm_device=" + drm_device, "drm_device=11");
                    }
                }*/
                string m3u8Content = string.Empty;
                bool isVOD = true;


                //开始解析

                Console.CursorVisible = false;
                LOGGER.PrintLine($"文件名称：{fileName}");
                LOGGER.PrintLine($"存储路径：{Path.GetDirectoryName(Path.Combine(workDir, fileName))}");

                Parser parser = new Parser();
                parser.DownName = fileName;
                parser.DownDir = Path.Combine(workDir, parser.DownName);
                parser.M3u8Url = testurl;
                if (baseUrl != "")
                    parser.BaseUrl = baseUrl;
                parser.Headers = reqHeaders;
                string exePath = Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName);
                LOGGER.LOGFILE = Path.Combine(exePath, "Logs", DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss") + ".log");
                LOGGER.InitLog();
                LOGGER.WriteLine("Start Parsing " + testurl);
                LOGGER.PrintLine("开始解析地址...", LOGGER.Warning);
                if (testurl.EndsWith(".json") && File.Exists(testurl))  //可直接跳过解析
                {
                    if (!Directory.Exists(Path.Combine(workDir, fileName)))//若文件夹不存在则新建文件夹   
                        Directory.CreateDirectory(Path.Combine(workDir, fileName)); //新建文件夹  
                    File.Copy(testurl, Path.Combine(Path.Combine(workDir, fileName), "meta.json"));
                }
                else
                {
                    parser.Parse();  //开始解析
                }

                //仅解析模式
                if (parseOnly)
                {
                    LOGGER.PrintLine("解析m3u8成功, 程序退出");
                    Environment.Exit(0);
                }

                if (File.Exists(Path.Combine(Path.Combine(workDir, fileName), "meta.json")))
                {
                    JObject initJson = JObject.Parse(File.ReadAllText(Path.Combine(Path.Combine(workDir, fileName), "meta.json")));
                    isVOD = Convert.ToBoolean(initJson["m3u8Info"]["vod"].ToString());
                    //传给Watcher总时长
                    Watcher.TotalDuration = initJson["m3u8Info"]["totalDuration"].Value<double>();
                    LOGGER.PrintLine($"文件时长：{Global.FormatTime((int)Watcher.TotalDuration)}");
                    LOGGER.PrintLine("总分片：" + initJson["m3u8Info"]["originalCount"].Value<int>()
                        + ", 已选择分片：" + initJson["m3u8Info"]["count"].Value<int>());
                }
                else
                {
                    DirectoryInfo directoryInfo = new DirectoryInfo(Path.Combine(workDir, fileName));
                    directoryInfo.Delete(true);
                    LOGGER.PrintLine("地址无效", LOGGER.Error);
                    LOGGER.CursorIndex = 5;
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
                    md.BinaryMerge = binaryMerge;
                    md.MuxFormat = "mp4";
                    md.RetryCount = retryCount;
                    md.MuxSetJson = muxSetJson;
                    md.MuxFastStart = muxFastStart;
                    md.DoDownload();
                }
                //直播
                if (isVOD == false)
                {
                    LOGGER.WriteLine("Living Stream Found");
                    LOGGER.WriteLine("Start Recording");
                    LOGGER.PrintLine("识别为直播流, 开始录制");
                    LOGGER.STOPLOG = true;  //停止记录日志
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
                LOGGER.WriteLineError("Download Failed");
                LOGGER.PrintLine("下载失败, 程序退出", LOGGER.Error);
                Console.CursorVisible = true;
                Environment.Exit(-1);
                //Console.Write("按任意键继续..."); Console.ReadKey(); return;
            }
            catch (Exception ex)
            {
                Console.CursorVisible = true;
                LOGGER.PrintLine(ex.Message, LOGGER.Error);
            }
        }
    }
}
