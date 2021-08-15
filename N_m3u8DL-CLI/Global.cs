using BrotliSharpLib;
using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace N_m3u8DL_CLI
{
    class Global
    {
        private volatile static bool shouldStop = false;
        public static long BYTEDOWN = 0;
        public static long STOP_SPEED = 0; //KB 小于此值自动重试
        public static long MAX_SPEED = 0; //KB 速度上限
        public static string VIDEO_TYPE = "";
        public static string AUDIO_TYPE = "";
        public static bool HadReadInfo = false;
        private static bool noProxy = false;
        private static string useProxyAddress = "";

        public static bool ShouldStop { get => shouldStop; set => shouldStop = value; }
        public static bool NoProxy { get => noProxy; set => noProxy = value; }
        public static string UseProxyAddress { get => useProxyAddress; set => useProxyAddress = value; }


        /*===============================================================================*/
        static Version ver = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
        static string nowVer = $"{ver.Major}.{ver.Minor}.{ver.Build}";
        static string nowDate = "20210325";
        public static void WriteInit()
        {
            Console.WriteLine($"N_m3u8DL-CLI version {nowVer} 2018-2021");
            Console.WriteLine($"  built date: {nowDate}");
            Console.WriteLine();
        }

        public static void CheckUpdate()
        {
            try
            {
                string redirctUrl = Get302("https://github.com/nilaoda/N_m3u8DL-CLI/releases/latest");
                string latestVer = redirctUrl.Replace("https://github.com/nilaoda/N_m3u8DL-CLI/releases/tag/", "");
                if (nowVer != latestVer && !latestVer.StartsWith("https"))
                {
                    Console.Title = string.Format(strings.newerVisionDetected, latestVer);
                    try
                    {
                        //尝试下载新版本
                        string url = $"https://mirror.ghproxy.com/https://github.com/nilaoda/N_m3u8DL-CLI/releases/download/{latestVer}/N_m3u8DL-CLI_v{latestVer}.exe";
                        if (File.Exists(Path.Combine(Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName), $"N_m3u8DL-CLI_v{latestVer}.exe")))
                        {
                            Console.Title = string.Format(strings.newerVerisonDownloaded, latestVer);
                            return;
                        }
                        HttpDownloadFile(url, Path.Combine(Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName), $"N_m3u8DL-CLI_v{latestVer}.exe"));
                        if (File.Exists(Path.Combine(Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName), $"N_m3u8DL-CLI_v{latestVer}.exe")))
                            Console.Title = string.Format(strings.newerVerisonDownloaded, latestVer);
                        else
                            Console.Title = string.Format(strings.newerVerisonDownloadFailed, latestVer);
                    }
                    catch (Exception)
                    {
                        ;
                    }
                }
            }
            catch (Exception)
            {
                ;
            }
        }

        public static string GetValidFileName(string input, string re = ".")
        {
            string title = input;
            foreach (char invalidChar in Path.GetInvalidFileNameChars())
            {
                title = title.Replace(invalidChar.ToString(), re);
            }
            return title;
        }

        // parseInt(s, radix)
        public static int GetNum(string str, int numBase)
        {
            return Convert.ToInt32(Microsoft.JScript.GlobalObject.parseInt(str, numBase)); 
        }

        //获取网页源码
        public static string GetWebSource(String url, string headers = "", int TimeOut = 60000)
        {
            string htmlCode = string.Empty;
            for (int i = 0; i < 5; i++)
            {
                try
                {
                reProcess:
                    HttpWebRequest webRequest = (System.Net.HttpWebRequest)System.Net.WebRequest.Create(url);
                    webRequest.Method = "GET";
                    if (NoProxy)
                    {
                        webRequest.Proxy = null;
                    }
                    else if (UseProxyAddress != "")
                    {
                        WebProxy proxy = new WebProxy(UseProxyAddress);
                        //proxy.Credentials = new NetworkCredential(username, password);
                        webRequest.Proxy = proxy;
                    }
                    webRequest.UserAgent = "Mozilla/5.0 (Windows NT 10.0; WOW64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/78.0.3904.108 Safari/537.36";
                    webRequest.Accept = "*/*";
                    webRequest.Headers.Add("Accept-Encoding", "gzip, deflate, br");
                    webRequest.Timeout = TimeOut;  //设置超时
                    webRequest.KeepAlive = false;
                    webRequest.AllowAutoRedirect = false; //手动处理重定向，否则会丢失Referer
                    if (url.Contains("pcvideo") && url.Contains(".titan.mgtv.com"))
                    {
                        webRequest.UserAgent = "";
                        if (!url.Contains("/internettv/"))
                            webRequest.Referer = "https://www.mgtv.com";
                        webRequest.Headers.Add("Cookie", "MQGUID");
                    }
                    //添加headers
                    if (headers != "")
                    {
                        foreach (string att in headers.Split('|'))
                        {
                            try
                            {
                                if (att.Split(':')[0].ToLower() == "referer")
                                    webRequest.Referer = att.Substring(att.IndexOf(":") + 1);
                                else if (att.Split(':')[0].ToLower() == "user-agent")
                                    webRequest.UserAgent = att.Substring(att.IndexOf(":") + 1);
                                else if (att.Split(':')[0].ToLower() == "range")
                                    webRequest.AddRange(Convert.ToInt32(att.Substring(att.IndexOf(":") + 1).Split('-')[0], Convert.ToInt32(att.Substring(att.IndexOf(":") + 1).Split('-')[1])));
                                else if (att.Split(':')[0].ToLower() == "accept")
                                    webRequest.Accept = att.Substring(att.IndexOf(":") + 1);
                                else
                                    webRequest.Headers.Add(att);
                            }
                            catch (Exception e)
                            {
                                LOGGER.WriteLineError(e.Message);
                            }
                        }
                    }
                    HttpWebResponse webResponse = (HttpWebResponse)webRequest.GetResponse();

                    //302
                    if (webResponse.Headers.Get("Location") != null)
                    {
                        url = webResponse.Headers.Get("Location");
                        webResponse.Close();
                        goto reProcess;
                    }

                    //文件过大则认为不是m3u8
                    if (webResponse.ContentLength != -1 && webResponse.ContentLength > 50 * 1024 * 1024) return "";

                    if (webResponse.ContentEncoding != null
                        && webResponse.ContentEncoding.ToLower() == "gzip") //如果使用了GZip则先解压
                    {
                        using (Stream streamReceive = webResponse.GetResponseStream())
                        {
                            using (var zipStream =
                                new System.IO.Compression.GZipStream(streamReceive, System.IO.Compression.CompressionMode.Decompress))
                            {
                                using (StreamReader sr = new StreamReader(zipStream, Encoding.UTF8))
                                {
                                    htmlCode = sr.ReadToEnd();
                                }
                            }
                        }
                    }
                    else if (webResponse.ContentEncoding != null
                        && webResponse.ContentEncoding.ToLower() == "br") //如果使用了Brotli则先解压
                    {
                        using (Stream streamReceive = webResponse.GetResponseStream())
                        {
                            using (var bs = new BrotliStream(streamReceive, CompressionMode.Decompress))
                            {
                                using (StreamReader sr = new StreamReader(bs, Encoding.UTF8))
                                {
                                    htmlCode = sr.ReadToEnd();
                                }
                            }
                        }
                    }
                    else
                    {
                        using (Stream streamReceive = webResponse.GetResponseStream())
                        {
                            using (StreamReader sr = new StreamReader(streamReceive, Encoding.UTF8))
                            {
                                htmlCode = sr.ReadToEnd();
                            }
                        }
                    }

                    if (webResponse != null)
                    {
                        webResponse.Close();
                    }
                    if (webRequest != null)
                    {
                        webRequest.Abort();
                    }
                    break;
                }
                catch (Exception e)  //捕获所有异常
                {
                    LOGGER.WriteLine(e.Message);
                    LOGGER.WriteLineError(e.Message);
                    Thread.Sleep(1000); //1秒后重试
                    continue;
                }
            }

            return htmlCode;
        }

        [DllImport("shell32.dll", ExactSpelling = true)]
        private static extern void ILFree(IntPtr pidlList);
        [DllImport("shell32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
        private static extern IntPtr ILCreateFromPathW(string pszPath);
        [DllImport("shell32.dll", ExactSpelling = true)]
        private static extern int SHOpenFolderAndSelectItems(IntPtr pidlList, uint cild, IntPtr children, uint dwFlags);

        //参数：
        //  string dir 指定的文件夹
        //  string ext 文件类型的扩展名，如".txt" , “.exe"
        public static int GetFileCount(string dir, string ext)
        {
            if (!Directory.Exists(dir)) 
                return 0;

            int count = 0;
            DirectoryInfo d = new DirectoryInfo(dir);
            foreach (FileInfo fi in d.GetFiles())
            {
                if (fi.Extension.ToUpper() == ext.ToUpper())
                {
                    count++;
                }
            }
            return count;
        }
        
        /// <summary>
        /// 寻找指定目录下指定后缀的文件的详细路径 如".txt"
        /// </summary>
        /// <param name="dir"></param>
        /// <param name="ext"></param>
        /// <returns></returns>
        public static string[] GetFiles(string dir, string ext)
        {
            ArrayList al = new ArrayList();
            StringBuilder sb = new StringBuilder();
            DirectoryInfo d = new DirectoryInfo(dir);
            foreach (FileInfo fi in d.GetFiles())
            {
                if (fi.Extension.ToUpper() == ext.ToUpper())
                {
                    al.Add(fi.FullName);
                }
            }
            string[] res = (string[])al.ToArray(typeof(string));
            Array.Sort(res); //排序
            return res;
        }

        /// <summary>  
        /// 获取url字符串参数，返回参数值字符串  
        /// </summary>  
        /// <param name="name">参数名称</param>  
        /// <param name="url">url字符串</param>  
        /// <returns></returns>  
        public static string GetQueryString(string name, string url)
        {
            Regex re = new Regex(@"(^|&)?(\w+)=([^&]+)(&|$)?", System.Text.RegularExpressions.RegexOptions.Compiled);
            MatchCollection mc = re.Matches(url);
            foreach (Match m in mc)
            {
                if (m.Result("$2").Equals(name))
                {
                    return m.Result("$3");
                }
            }
            return "";
        }

        //大量文件分部分二进制合并
        public static void PartialCombineMultipleFiles(string[] files)
        {
            int div = 0;
            if (files.Length <= 90000)
                div = 100;
            else
                div = 200;

            string outputName = Path.GetDirectoryName(files[0]) + "\\T";
            int index = 0; //序号

            //按照div的容量分割为小数组
            string[][] li = Enumerable.Range(0, files.Count() / div + 1).Select(x => files.Skip(x * div).Take(div).ToArray()).ToArray();
            foreach (var items in li)
            {
                if (items.Count() == 0) 
                    continue;
                CombineMultipleFilesIntoSingleFile(items, outputName + index.ToString("0000") + ".ts");
                //合并后删除这些文件
                foreach (var item in items)
                {
                    File.Delete(item);
                }
                index++;
            }
        }

        /// <summary>
        /// 输入一堆已存在的文件，合并到新文件
        /// </summary>
        /// <param name="files"></param>
        /// <param name="outputFilePath"></param>
        public static void CombineMultipleFilesIntoSingleFile(string[] files, string outputFilePath)
        {
            //同名文件已存在的共存策略
            if (File.Exists(outputFilePath))
            {
                outputFilePath = Path.Combine(Path.GetDirectoryName(outputFilePath),
                    Path.GetFileNameWithoutExtension(outputFilePath) + "_" + DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss") + Path.GetExtension(outputFilePath));
            }
            if (files.Length == 1)
            {
                FileInfo fi = new FileInfo(files[0]);
                fi.MoveTo(outputFilePath);
                return;
            }

            if (!Directory.Exists(Path.GetDirectoryName(outputFilePath)))
                Directory.CreateDirectory(Path.GetDirectoryName(outputFilePath));

            string[] inputFilePaths = files;
            using (var outputStream = File.Create(outputFilePath))
            {
                foreach (var inputFilePath in inputFilePaths)
                {
                    if (inputFilePath == "")
                        continue;
                    using (var inputStream = File.OpenRead(inputFilePath))
                    {
                        // Buffer size can be passed as the second argument.
                        inputStream.CopyTo(outputStream);
                    }
                    //Console.WriteLine("The file {0} has been processed.", inputFilePath);
                }
            }
            //Global.ExplorerFile(outputFilePath);
        }
        


        /// <summary>
        /// 将一个字节流附加至文件流
        /// </summary>
        /// <param name="liveStream"></param>
        /// <param name="file"></param>
        public static void AppendBytesToFileStreamAndDoNotClose(FileStream liveStream, byte[] file)
        {
            FileStream outputStream = liveStream;
            using (var inputStream = new MemoryStream(file))
            {
                inputStream.CopyTo(outputStream);
            }
        }

        //重定向
        public static string Get302(string url, string headers = "", int timeout = 5000)
        {
            try
            {
                string redirectUrl;
                WebRequest myRequest = WebRequest.Create(url);
                myRequest.Timeout = timeout;
                if (NoProxy)
                {
                    myRequest.Proxy = null;
                }
                else if (UseProxyAddress != "")
                {
                    WebProxy proxy = new WebProxy(UseProxyAddress);
                    //proxy.Credentials = new NetworkCredential(username, password);
                    myRequest.Proxy = proxy;
                }
                //添加headers
                if (headers != "")
                {
                    foreach (string att in headers.Split('|'))
                    {
                        try
                        {
                            myRequest.Headers.Add(att);
                        }
                        catch (Exception)
                        {

                        }
                    }
                }
                WebResponse myResponse = myRequest.GetResponse();
                redirectUrl = myResponse.ResponseUri.ToString();
                myResponse.Close();
                return redirectUrl;
            }
            catch (Exception) { return url; }
        }

        /// <summary>
        /// 下载文件为字节流
        /// </summary>
        /// <param name="url"></param>
        /// <param name="timeOut"></param>
        /// <returns></returns>
        public static byte[] HttpDownloadFileToBytes(string url, string headers = "", int timeOut = 60000)
        {
            //本地文件
            if (url.StartsWith("file:"))
            {
                Uri t = new Uri(url);
                url = t.LocalPath;
                if (File.Exists(url))
                {
                    FileStream fs = new FileStream(url, FileMode.Open, FileAccess.Read);
                    byte[] infbytes = new byte[(int)fs.Length];
                    fs.Read(infbytes, 0, infbytes.Length);
                    fs.Close();
                    return infbytes;
                }
            }

        reProcess:
            byte[] arraryByte;
            HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
            req.Method = "GET";
            req.Timeout = timeOut;
            req.ReadWriteTimeout = timeOut; //重要
            req.AllowAutoRedirect = false; //手动处理重定向，否则会丢失Referer
            if (NoProxy)
            {
                req.Proxy = null;
            }
            else if (UseProxyAddress != "")
            {
                WebProxy proxy = new WebProxy(UseProxyAddress);
                //proxy.Credentials = new NetworkCredential(username, password);
                req.Proxy = proxy;
            }
            req.Headers.Add("Accept-Encoding", "gzip, deflate");
            req.Accept = "*/*";
            req.UserAgent = "Mozilla/5.0 (Windows NT 10.0; WOW64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/78.0.3904.108 Safari/537.36";
            //添加headers
            if (headers != "")
            {
                foreach (string att in headers.Split('|'))
                {
                    try
                    {
                        if (att.Split(':')[0].ToLower() == "referer")
                            req.Referer = att.Substring(att.IndexOf(":") + 1);
                        else if (att.Split(':')[0].ToLower() == "user-agent")
                            req.UserAgent = att.Substring(att.IndexOf(":") + 1);
                        else if (att.Split(':')[0].ToLower() == "range")
                            req.AddRange(Convert.ToInt32(att.Substring(att.IndexOf(":") + 1).Split('-')[0], Convert.ToInt32(att.Substring(att.IndexOf(":") + 1).Split('-')[1])));
                        else if (att.Split(':')[0].ToLower() == "accept")
                            req.Accept = att.Substring(att.IndexOf(":") + 1);
                        else
                            req.Headers.Add(att);
                    }
                    catch (Exception e)
                    {
                        LOGGER.WriteLineError(e.Message);
                    }
                }
            }

            using (HttpWebResponse wr = (HttpWebResponse)req.GetResponse())
            {
                //302
                if (wr.Headers.Get("Location") != null)
                {
                    url = wr.Headers.Get("Location");
                    wr.Close();
                    goto reProcess;
                }
                if (wr.ContentEncoding != null && wr.ContentEncoding.ToLower() == "gzip") //如果使用了GZip则先解压
                {
                    using (Stream streamReceive = wr.GetResponseStream())
                    {
                        using (var zipStream =
                            new System.IO.Compression.GZipStream(streamReceive, System.IO.Compression.CompressionMode.Decompress))
                        {
                            //读取到内存
                            MemoryStream stmMemory = new MemoryStream();
                            Stream responseStream = zipStream;

                            byte[] bArr = new byte[1024];
                            int size = responseStream.Read(bArr, 0, (int)bArr.Length);
                            while (size > 0)
                            {
                                stmMemory.Write(bArr, 0, size);
                                size = responseStream.Read(bArr, 0, (int)bArr.Length);
                            }
                            arraryByte = stmMemory.ToArray();
                            responseStream.Close();
                            stmMemory.Close();
                        }
                    }
                }
                else
                {
                    using (Stream streamReceive = wr.GetResponseStream())
                    {
                        //读取到内存
                        MemoryStream stmMemory = new MemoryStream();
                        Stream responseStream = streamReceive;

                        byte[] bArr = new byte[1024];
                        int size = responseStream.Read(bArr, 0, (int)bArr.Length);
                        while (size > 0)
                        {
                            stmMemory.Write(bArr, 0, size);
                            size = responseStream.Read(bArr, 0, (int)bArr.Length);
                        }
                        arraryByte = stmMemory.ToArray();
                        responseStream.Close();
                        stmMemory.Close();
                    }
                }
            }
            return arraryByte;
        }

        /// <summary>
        /// Http下载文件
        /// </summary>
        public static void HttpDownloadFile(string url, string path, int timeOut = 20000, string headers = "", long startByte = 0, long expectByte = -1)
        {
            int retry = 0;
            reDownload:
            try
            {
                if (File.Exists(path))
                    File.Delete(path);
                if (shouldStop)
                    return;

                reProcess:
                HttpWebRequest request = WebRequest.Create(url) as HttpWebRequest;
                request.Timeout = timeOut;
                request.ReadWriteTimeout = timeOut; //重要
                request.AllowAutoRedirect = false; //手动处理重定向，否则会丢失Referer
                request.KeepAlive = false;
                request.Method = "GET";
                if (NoProxy)
                {
                    request.Proxy = null;
                }
                else if (UseProxyAddress != "")
                {
                    WebProxy proxy = new WebProxy(UseProxyAddress);
                    //proxy.Credentials = new NetworkCredential(username, password);
                    request.Proxy = proxy;
                }
                if (url.Contains("data.video.iqiyi.com"))
                    request.UserAgent = "QYPlayer/Android/4.4.5;NetType/3G;QTP/1.1.4.3";
                else if (url.Contains("pcvideo") && url.Contains(".titan.mgtv.com"))
                {
                    request.UserAgent = "";
                    if (!url.Contains("/internettv/"))
                        request.Referer = "https://www.mgtv.com";
                    request.Headers.Add("Cookie", "MQGUID");
                }
                else if (url.Contains(".xboku.com/")) //独播库
                {
                    request.Referer = "https://my.duboku.vip/static/player/videojs.html";
                }
                else
                    request.UserAgent = "Mozilla/5.0 (Windows NT 10.0; WOW64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/78.0.3904.108 Safari/537.36";
                //下载部分字节
                if (expectByte != -1)
                    request.AddRange("bytes", startByte, startByte + expectByte - 1);
                //添加headers
                if (headers != "")
                {
                    foreach (string att in headers.Split('|'))
                    {
                        try
                        {
                            if (att.Split(':')[0].ToLower() == "referer")
                                request.Referer = att.Substring(att.IndexOf(":") + 1);
                            else if (att.Split(':')[0].ToLower() == "user-agent")
                                request.UserAgent = att.Substring(att.IndexOf(":") + 1);
                            else if (att.Split(':')[0].ToLower() == "range")
                                request.AddRange(Convert.ToInt32(att.Substring(att.IndexOf(":") + 1).Split('-')[0], Convert.ToInt32(att.Substring(att.IndexOf(":") + 1).Split('-')[1])));
                            else if (att.Split(':')[0].ToLower() == "accept")
                                request.Accept = att.Substring(att.IndexOf(":") + 1);
                            else
                                request.Headers.Add(att);
                        }
                        catch (Exception e)
                        {
                            LOGGER.WriteLineError(e.Message);
                        }
                    }
                }

                long totalLen = 0;
                long downLen = 0;
                bool pngHeader = false; //PNG HEADER检测
                using (var response = (HttpWebResponse)request.GetResponse())
                {
                    //302
                    if (response.Headers.Get("Location") != null)
                    {
                        url = response.Headers.Get("Location");
                        response.Close();
                        goto reProcess;
                    }
                    using (var responseStream = response.GetResponseStream())
                    {
                        using (var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Write))
                        {
                            //responseStream.CopyTo(stream);
                            totalLen = response.ContentLength;
                            byte[] bArr = new byte[1024];
                            int size = responseStream.Read(bArr, 0, (int)bArr.Length);
                            if (!pngHeader && size > 3 && 137 == bArr[0] && 80 == bArr[1] && 78 == bArr[2] && 71 == bArr[3])
                            {
                                pngHeader = true;
                            }
                            //GIF HEADER检测
                            if (!pngHeader && size > 3 && 0x47 == bArr[0] && 0x49 == bArr[1] && 0x46 == bArr[2] && 0x38 == bArr[3])
                            {
                                bArr = bArr.Skip(42).ToArray();
                                size -= 42;
                                downLen += 42;
                            }
                            while (size > 0)
                            {
                                stream.Write(bArr, 0, size);
                                downLen += size;
                                BYTEDOWN += size; //计算下载速度
                                if (MAX_SPEED != 0)
                                    while (BYTEDOWN >= MAX_SPEED * 1024 * DownloadManager.CalcTime)  //限速
                                    {
                                        Thread.Sleep(1);
                                    }
                                size = responseStream.Read(bArr, 0, (int)bArr.Length);
                                if (shouldStop)
                                {
                                    request.Abort();
                                    break;
                                }
                            }
                        }
                    }
                }
                if (shouldStop)
                    try { File.Delete(path); } catch (Exception) { }
                if (totalLen != -1 && downLen != totalLen)
                    try { File.Delete(path); } catch (Exception) { }
                if (pngHeader)
                    TrySkipPngHeader(path);

            }
            catch (Exception e)
            {
                LOGGER.WriteLineError("DOWN: " + e.Message + " " + url);
                try { File.Delete(path); } catch (Exception) { }
                if (retry++ < 3)
                {
                    Thread.Sleep(1000);
                    LOGGER.WriteLineError($"DOWN: AUTO RETRY {retry}/3 " + url);
                    goto reDownload;
                }
            }
        }

        /// <summary>
        /// 用于处理利用图床上传TS导致前面被插入PNG Header的情况
        /// </summary>
        /// <param name="filePath"></param>
        public static void TrySkipPngHeader(string filePath)
        {
            var u = File.ReadAllBytes(filePath);
            if (0x47 == u[0])
            {
                return;
            }
            else if (u.Length > 120 && 137 == u[0] && 80 == u[1] && 78 == u[2] && 71 == u[3] && 96 == u[118] && 130 == u[119])
            {
                u = u.Skip(120).ToArray();
            }
            else if (u.Length > 6102 && 137 == u[0] && 80 == u[1] && 78 == u[2] && 71 == u[3] && 96 == u[6100] && 130 == u[6101])
            {
                u = u.Skip(6102).ToArray();
            }
            else if (u.Length > 69 && 137 == u[0] && 80 == u[1] && 78 == u[2] && 71 == u[3] && 96 == u[67] && 130 == u[68])
            {
                u = u.Skip(69).ToArray();
            }
            else if (u.Length > 771 && 137 == u[0] && 80 == u[1] && 78 == u[2] && 71 == u[3] && 96 == u[769] && 130 == u[770])
            {
                u = u.Skip(771).ToArray();
            }
            else if (u.Length > 4 && 137 == u[0] && 80 == u[1] && 78 == u[2] && 71 == u[3])
            {
                //确定是PNG但是需要手动查询结尾标记 0x47 出现两次
                int skip = 0;
                for (int i = 4; i < u.Length - 188 * 2 - 4; i++)
                {
                    if (u[i] == 0x47 && u[i + 188] == 0x47 && u[i + 188 + 188] == 0x47)
                    {
                        skip = i;
                        break;
                    }
                }
                u = u.Skip(skip).ToArray();
            }

            File.WriteAllBytes(filePath, u);
        }

        //格式化json字符串
        public static string ConvertJsonString(string str)
        {
            //Console.WriteLine(str);
            JsonSerializer serializer = new JsonSerializer();
            TextReader tr = new StringReader(str);
            JsonTextReader jtr = new JsonTextReader(tr);
            object obj = serializer.Deserialize(jtr);
            if (obj != null)
            {
                StringWriter textWriter = new StringWriter();
                JsonTextWriter jsonWriter = new JsonTextWriter(textWriter)
                {
                    Formatting = Newtonsoft.Json.Formatting.Indented,
                    Indentation = 2,
                    IndentChar = ' '
                };  //Indentation 为缩进量
                serializer.Serialize(jsonWriter, obj);
                return textWriter.ToString();
            }
            else
            {
                return str;
            }
        }

        //获取属性
        public static string GetTagAttribute(string attributeList, string key)
        {
            /*#EXT-X-STREAM-INF:PROGRAM-ID=1,RESOLUTION=1056x594,BANDWIDTH=1963351,CODECS="mp4a.40.5,avc1.4d001f",FRAME-RATE=30.000,AUDIO="aac",AVERAGE-BANDWIDTH=1655131*/
            if (attributeList != "") 
            {
                try
                {
                    string tmp = attributeList.Trim();
                    if (tmp.Contains(key + "=")) 
                    {
                        if (tmp[tmp.IndexOf(key + "=") + key.Length + 1] == '\"')
                        {
                            return tmp.Substring(tmp.IndexOf(key + "=") + key.Length + 2, tmp.Remove(0, tmp.IndexOf(key + "=") + key.Length + 2).IndexOf('\"'));
                        }
                        else
                        {
                            if (tmp.Remove(0, tmp.IndexOf(key + "=") + key.Length + 2).Contains(","))
                                return tmp.Substring(tmp.IndexOf(key + "=") + key.Length + 1, tmp.Remove(0, tmp.IndexOf(key + "=") + key.Length + 1).IndexOf(','));
                            else
                                return tmp.Substring(tmp.IndexOf(key + "=") + key.Length + 1);
                        }
                    }
                }
                catch (Exception)
                {
                    return string.Empty;
                }
            }
            return string.Empty;
        }

        //正则表达式
        public static ArrayList RegexFind(string regex, string src, int group = -1)
        {
            ArrayList array = new ArrayList();
            Regex reg = new Regex(@regex);
            MatchCollection result = reg.Matches(src);
            if (result.Count == 0)
                array.Add("NULL");
            foreach (Match m in result)
            {
                if (group == -1)
                    array.Add(m.Value);
                else
                    array.Add(m.Groups[group].Value);
            }
            return array;
        }

        //调用ffmpeg获取视频信息
        public static ArrayList GetVideoInfo(string file)
        {
            LOGGER.WriteLine(strings.readingFileInfo);
            LOGGER.PrintLine(strings.readingFileInfo, LOGGER.Warning);
            StringBuilder sb = new StringBuilder();
            ArrayList info = new ArrayList();
            string cmd = "-hide_banner -i \"" + file + "\"";
            if (!File.Exists(file))
            {
                info.Add("Error in reading file");
                return info;
            }
            using (Process p = new Process())
            {
                p.StartInfo.FileName = "ffmpeg";
                p.StartInfo.Arguments = cmd;
                p.StartInfo.UseShellExecute = false;        //是否使用操作系统shell启动
                p.StartInfo.RedirectStandardInput = true;   //接受来自调用程序的输入信息
                p.StartInfo.RedirectStandardOutput = true;  //由调用程序获取输出信息
                p.StartInfo.RedirectStandardError = true;   //重定向标准错误输出
                p.StartInfo.CreateNoWindow = true;          //不显示程序窗口
                p.StartInfo.StandardErrorEncoding = Encoding.UTF8;
                p.Start();//启动程序
                p.StandardInput.AutoFlush = true;
                //获取cmd窗口的输出信息
                StreamReader reader = p.StandardError;//截取输出流
                sb.Append(reader.ReadLine() + "\r\n");//每次读取一行
                while (!reader.EndOfStream)
                {
                    sb.Append(reader.ReadLine() + "\r\n");
                }
                p.WaitForExit();//等待程序执行完退出进程
                p.Close();
            }

            string res = string.Empty;
            foreach (string s in (string[])RegexFind("Stream #.*", sb.ToString()).ToArray(typeof(string)))
            {
                res = "PID "
                    + RegexFind(@"\[(0x\d{2,})\]", s, 1)[0].ToString() + ": "
                    + RegexFind(@": (.*)", s, 1)[0].ToString()
                    .Replace(RegexFind(@" \(\[.*?\)", s)[0].ToString(), "")
                    .Replace(": ", " ");

                if (VIDEO_TYPE == "" && res.Contains(": Video")) 
                {
                    if (res.Contains("Video dvvideo"))  //爱奇艺杜比视界
                    {
                        VIDEO_TYPE = "DV";
                    }
                    else if (res.Contains("Video none (dvhe"))  //腾讯视频杜比视界
                    {
                        VIDEO_TYPE = "DV";
                    }
                    else if (res.Contains("Video hevc (dvhe"))  //腾讯视频杜比视界
                    {
                        VIDEO_TYPE = "DV";
                    }
                    else if (res.Contains("Video hevc (Main 10) (DOVI"))  //优酷视频杜比视界
                    {
                        VIDEO_TYPE = "DV";
                    }
                    else if (res.Contains("Video hevc (Main 10) (dvh1"))  //优酷视频杜比视界
                    {
                        VIDEO_TYPE = "DV";
                    }
                    else if (res.Contains("Video hevc (dvh1"))  //优酷视频杜比视界
                    {
                        VIDEO_TYPE = "DV";
                    }
                    else if (res.Contains("Video h264"))
                    {
                        VIDEO_TYPE = "H264";
                    }
                    else if (res.Contains("Video hevc"))
                    {
                        VIDEO_TYPE = "H265";
                    }
                    else
                    {
                        VIDEO_TYPE = "UNKOWN";
                    }
                }

                if (res.Contains("Audio aac"))
                {
                    FFmpeg.UseAACFilter = true;
                }

                //有非AAC音轨则关闭UseAACFilter
                if (res.Contains("Audio") && !res.Contains("Audio aac"))
                {
                    FFmpeg.UseAACFilter = false;
                }

                if ((VIDEO_TYPE == "" || VIDEO_TYPE == "IGNORE") && res.Contains("Audio eac3")) 
                {
                    AUDIO_TYPE = "eac3";
                }
                else if((VIDEO_TYPE == "" || VIDEO_TYPE == "IGNORE") && res.Contains("Audio aac"))
                {
                    AUDIO_TYPE = "aac";
                }
                else if ((VIDEO_TYPE == "" || VIDEO_TYPE == "IGNORE") && res.Contains("Audio ac3"))
                {
                    AUDIO_TYPE = "ac3";
                }

                info.Add(res);
            }

            if (VIDEO_TYPE != "" && VIDEO_TYPE != "IGNORE")
                AUDIO_TYPE = "";

            return info;
        }

        //所给路径中所对应的文件大小
        public static long FileSize(string filePath)
        {
            //定义一个FileInfo对象，是指与filePath所指向的文件相关联，以获取其大小
            FileInfo fileInfo = new FileInfo(filePath);
            return fileInfo.Length;
        }

        //获取文件夹大小
        public static long GetDirectoryLength(string path)
        {
            if (!Directory.Exists(path))
            {
                return 0;
            }
            long size = 0;
            //遍历指定路径下的所有文件
            DirectoryInfo di = new DirectoryInfo(path);
            foreach (FileInfo fi in di.GetFiles())
            {
                size += fi.Length;
            }
            //遍历指定路径下的所有文件夹
            DirectoryInfo[] dis = di.GetDirectories();
            if (dis.Length > 0)
            {
                for (int i = 0; i < dis.Length; i++)
                {
                    size += GetDirectoryLength(dis[i].FullName);
                }
            }
            return size;
        }

        //此函数用于格式化输出时长  
        public static String FormatTime(Int32 time)
        {
            TimeSpan ts = new TimeSpan(0, 0, time);
            string str = "";
            str = (ts.Hours.ToString("00") == "00" ? "" : ts.Hours.ToString("00") + "h") + ts.Minutes.ToString("00") + "m" + ts.Seconds.ToString("00") + "s";
            return str;
        }

        //此函数用于格式化输出文件大小
        public static String FormatFileSize(Double fileSize)
        {
            if (fileSize < 0)
            {
                return "Error";
            }
            else if (fileSize >= 1024 * 1024 * 1024)
            {
                return string.Format("{0:########0.00} GB", ((Double)fileSize) / (1024 * 1024 * 1024));
            }
            else if (fileSize >= 1024 * 1024)
            {
                return string.Format("{0:####0.00} MB", ((Double)fileSize) / (1024 * 1024));
            }
            else if (fileSize >= 1024)
            {
                return string.Format("{0:####0.00} KB", ((Double)fileSize) / 1024);
            }
            else
            {
                return string.Format("{0} bytes", fileSize);
            }
        }

        /// <summary>  
        /// 获取当前时间戳  
        /// </summary>  
        /// <param name="bflag">为真时获取10位时间戳,为假时获取13位时间戳.bool bflag = true</param>  
        /// <returns></returns>  
        public static string GetTimeStamp(bool bflag)
        {
            TimeSpan ts = DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, 0);
            string ret = string.Empty;
            if (bflag)
                ret = Convert.ToInt64(ts.TotalSeconds).ToString();
            else
                ret = Convert.ToInt64(ts.TotalMilliseconds).ToString();

            return ret;
        }

        /// <summary>
        /// 获取有效文件名
        /// </summary>
        /// <param name="text"></param>
        /// <param name="replacement"></param>
        /// <returns></returns>
        public static string MakeValidFileName(string text, string replacement = "_")
        {
            StringBuilder str = new StringBuilder();
            var invalidFileNameChars = Path.GetInvalidFileNameChars();
            foreach (var c in text)
            {
                if (invalidFileNameChars.Contains(c))
                {
                    str.Append(replacement ?? "");
                }
                else
                {
                    str.Append(c);
                }
            }
            return str.ToString();
        }

        /// <summary>
        /// 从URL获取文件名
        /// </summary>
        /// <param name="url"></param>
        /// <returns></returns>
        public static string GetUrlFileName(string url)
        {
            if (File.Exists(url))
            {
                return Path.GetFileNameWithoutExtension(url);
            }
            if (string.IsNullOrEmpty(url))
            {
                return "None";
            }
            try
            {
                string[] strs1 = url.Split(new char[] { '/' });
                return MakeValidFileName(System.Web.HttpUtility.UrlDecode(strs1[strs1.Length - 1].Split(new char[] { '?' })[0].Replace(".m3u8", "")));
            }
            catch (Exception)
            {
                return DateTime.Now.ToString("yyyy.MM.dd-HH.mm.ss");
            }
        }

        //检测GZip并解压
        public static void GzipHandler(string file)
        {
            try
            {
                using (FileStream fr = File.OpenRead(file))
                {
                    using (GZipStream gz = new GZipStream(fr, CompressionMode.Decompress))
                    {
                        using (FileStream fw = File.OpenWrite(Path.Combine(Path.GetDirectoryName(file), Path.GetFileNameWithoutExtension(file) + "[t].ts")))
                        {
                            byte[] by = new byte[1024];
                            int r = gz.Read(by, 0, by.Length);
                            while (r > 0)
                            {
                                fw.Write(by, 0, r);
                                r = gz.Read(by, 0, r);
                            }
                        }
                    }
                    File.Delete(file);
                    File.Move(Path.Combine(Path.GetDirectoryName(file), Path.GetFileNameWithoutExtension(file) + "[t].ts"), file);
                }
            }
            catch (Exception)
            {
                if (File.Exists(Path.Combine(Path.GetDirectoryName(file), Path.GetFileNameWithoutExtension(file) + "[t].ts")))
                    File.Delete(Path.Combine(Path.GetDirectoryName(file), Path.GetFileNameWithoutExtension(file) + "[t].ts"));
                return;
            }
        }

        [DllImport("shell32.dll", SetLastError = true)]
        static extern IntPtr CommandLineToArgvW([MarshalAs(UnmanagedType.LPWStr)] string lpCmdLine, out int pNumArgs);
        //使用Win32 API解析字符串为命令行参数
        public static IEnumerable<string> ParseArguments(string commandLine)
        {
            int argc;
            var argv = CommandLineToArgvW(commandLine, out argc);
            if (argv == IntPtr.Zero)
                throw new System.ComponentModel.Win32Exception();
            try
            {
                var args = new string[argc];
                for (var i = 0; i < args.Length; i++)
                {
                    var p = Marshal.ReadIntPtr(argv, i * IntPtr.Size);
                    args[i] = Marshal.PtrToStringUni(p);
                }

                return args;
            }
            finally
            {
                Marshal.FreeHGlobal(argv);
            }
        }

        //重载
        public class WebClientEx : WebClient
        {
            private readonly long from;
            private readonly long to;
            private readonly int timeout;
            private readonly bool setTimeout;
            private readonly bool setRange;

            public WebClientEx()
            {
                
            }

            public WebClientEx(long from, long to)
            {
                this.from = from;
                this.to = to;
                setRange = true;
            }

            public WebClientEx(int timeout)
            {
                this.timeout = timeout;
                setTimeout = true;
            }

            public WebClientEx(int timeout, long from, long to)
            {
                this.timeout = timeout;
                setTimeout = true;
                this.from = from;
                this.to = to;
                setRange = true;
            }

            protected override WebRequest GetWebRequest(Uri address)
            {
                var wr = (HttpWebRequest)base.GetWebRequest(address);
                if (NoProxy)
                {
                    wr.Proxy = null;
                }
                else if (UseProxyAddress != "")
                {
                    WebProxy proxy = new WebProxy(UseProxyAddress);
                    //proxy.Credentials = new NetworkCredential(username, password);
                    wr.Proxy = proxy;
                }
                if (setRange)
                    wr.AddRange(this.from, this.to);
                if (setTimeout)
                    wr.Timeout = timeout; // timeout in milliseconds (ms)
                return wr;
            }
        }

        /**
         * 通过X-TIMESTAMP-MAP 调整VTT字幕的时间轴
         */
        public static void ReAdjustVtt(string[] vtts)
        {
            string MsToTime(int ms)
            {
                TimeSpan ts = new TimeSpan(0, 0, 0, 0, ms);
                string str = "";
                str = (ts.Hours.ToString("00") + ":") + ts.Minutes.ToString("00") + ":" + ts.Seconds.ToString("00") + "." + ts.Milliseconds.ToString("000");
                return str;
            }

            int TimeToMs(string line)
            {
                int hh = Convert.ToInt32(line.Split(':')[0]);
                int mm = Convert.ToInt32(line.Split(':')[1]);
                int ss = Convert.ToInt32(line.Split(':')[2].Split('.')[0]);
                int ms = Convert.ToInt32(line.Split(':')[2].Split('.')[1]);
                return hh * 60 * 60 * 1000 + mm * 60 * 1000 + ss * 1000 + ms;
            }

            int addTime = 0;
            int baseTime = 0;
            for (int i = 0; i < vtts.Length; i++)
            {
                string tmp = File.ReadAllText(vtts[i], Encoding.UTF8);
                if (!Regex.IsMatch(tmp, "X-TIMESTAMP-MAP.*MPEGTS:(\\d+)"))
                    break;
                if (i > 0)
                {
                    int newTime = Convert.ToInt32(Regex.Match(tmp, "X-TIMESTAMP-MAP.*MPEGTS:(\\d+)").Groups[1].Value);
                    if (newTime == 900000)
                        continue;
                    //计算偏移量
                    //LOGGER.PrintLine((newTime - baseTime).ToString());
                    addTime = addTime + ((newTime - baseTime) / 100);
                    if ((newTime - baseTime) == 6300000)
                        addTime -= 3000;
                    //将新的作为基准时间
                    baseTime = newTime;
                    foreach (Match m in Regex.Matches(tmp, @"(\d{2}:\d{2}:\d{2}\.\d{3}) --> (\d{2}:\d{2}:\d{2}\.\d{3})"))
                    {
                        string start = m.Groups[1].Value;
                        string end = m.Groups[2].Value;
                        tmp = tmp.Replace(m.Value, MsToTime(TimeToMs(start) + addTime) + " --> " + MsToTime(TimeToMs(end) + addTime));
                    }
                }
                File.WriteAllText(vtts[i], Regex.Replace(tmp, "X-TIMESTAMP-MAP=.*", ""), Encoding.UTF8);
            }
            //Console.ReadLine();
        }
    }
}
