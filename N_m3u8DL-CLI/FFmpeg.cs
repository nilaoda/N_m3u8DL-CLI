using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace N_m3u8DL_CLI
{
    class FFmpeg
    {
        public static string FFMPEG_PATH = "ffmpeg";
        public static string REC_TIME = ""; //录制日期

        public static string OutPutPath { get; set; } = string.Empty;
        public static string ReportFile { get; set; } = string.Empty;
        public static bool UseAACFilter { get; set; } = false;  //是否启用滤镜
        public static bool WriteDate { get; set; } = true;  //是否写入录制日期

        public static void Merge(string[] files, string muxFormat, bool fastStart,
            string poster = "", string audioName = "", string title = "",
            string copyright = "", string comment = "", string encodingTool = "")
        {
            string dateString = string.IsNullOrEmpty(REC_TIME) ? DateTime.Now.ToString("o") : REC_TIME;

            //同名文件已存在的共存策略
            if (File.Exists($"{OutPutPath}.{muxFormat.ToLower()}")) 
            {
                OutPutPath = Path.Combine(Path.GetDirectoryName(OutPutPath),
                    Path.GetFileName(OutPutPath) + "_" + DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss"));
            }

            string command = "-loglevel warning -i concat:\"";
            string data = string.Empty;
            string ddpAudio = string.Empty;
            string addPoster = "-map 1 -c:v:1 copy -disposition:v:1 attached_pic";
            ddpAudio = (File.Exists($"{Path.GetFileNameWithoutExtension(OutPutPath + ".mp4")}.txt") ? File.ReadAllText($"{Path.GetFileNameWithoutExtension(OutPutPath + ".mp4")}.txt") : "") ;
            if (!string.IsNullOrEmpty(ddpAudio)) UseAACFilter = false;


            foreach (string t in files)
            {
                command += Path.GetFileName(t) + "|";
            }

            switch (muxFormat.ToUpper())
            {
                case ("MP4"):
                    command += "\" " + (string.IsNullOrEmpty(poster) ? "" : "-i \"" + poster + "\"");
                    command += " " + (string.IsNullOrEmpty(ddpAudio) ? "" : "-i \"" + ddpAudio + "\"");
                    command +=
                        $" -map 0:v? {(string.IsNullOrEmpty(ddpAudio) ? "-map 0:a?" : $"-map {(string.IsNullOrEmpty(poster) ? "1" : "2")}:a -map 0:a?")} -map 0:s? " + (string.IsNullOrEmpty(poster) ? "" : addPoster)
                        + (WriteDate ? " -metadata date=\"" + dateString + "\"" : "") +
                        " -metadata encoding_tool=\"" + encodingTool + "\" -metadata title=\"" + title +
                        "\" -metadata copyright=\"" + copyright + "\" -metadata comment=\"" + comment +
                        $"\" -metadata:s:a:{(string.IsNullOrEmpty(ddpAudio) ? "0" : "1")} handler_name=\"" + audioName + $"\" -metadata:s:a:{(string.IsNullOrEmpty(ddpAudio) ? "0" : "1")} handler=\"" + audioName + "\" ";
                    command += (string.IsNullOrEmpty(ddpAudio) ? "" : " -metadata:s:a:0 handler_name=\"DD+\" -metadata:s:a:0 handler=\"DD+\" ");
                    if (fastStart)
                        command += "-movflags +faststart";
                    command += "  -c copy -y " + (UseAACFilter ? "-bsf:a aac_adtstoasc" : "") + " \"" + OutPutPath + ".mp4\"";
                    break;
                case ("MKV"):
                    command += "\" -map 0  -c copy -y " + (UseAACFilter ? "-bsf:a aac_adtstoasc" : "") + " \"" + OutPutPath + ".mkv\"";
                    break;
                case ("FLV"):
                    command += "\" -map 0  -c copy -y " + (UseAACFilter ? "-bsf:a aac_adtstoasc" : "") + " \"" + OutPutPath + ".flv\"";
                    break;
                case ("TS"):
                    command += "\" -map 0  -c copy -y -f mpegts -bsf:v h264_mp4toannexb \"" + OutPutPath + ".ts\"";
                    break;
                case ("VTT"):
                    command += "\" -map 0  -y \"" + OutPutPath + ".srt\"";  //Convert To Srt
                    break;
                case ("EAC3"):
                    command += "\" -map 0:a -c copy -y \"" + OutPutPath + ".eac3\"";
                    break;
                case ("AAC"):
                    command += "\" -map 0:a -c copy -y \"" + OutPutPath + ".m4a\"";
                    break;
                case ("AC3"):
                    command += "\" -map 0:a -c copy -y \"" + OutPutPath + ".ac3\"";
                    break;

            }

            Run(FFMPEG_PATH, command, Path.GetDirectoryName(files[0]));
            LOGGER.WriteLine(strings.ffmpegDone);
            //Console.WriteLine(command);
        }

        public static void ConvertToMPEGTS(string file)
        {
            if (Global.VIDEO_TYPE == "H264")
            {
                Run(FFMPEG_PATH,
                    "-loglevel quiet -i \"" + file + "\" -map 0 -c copy -copy_unknown -f mpegts -bsf:v h264_mp4toannexb \""
                    + Path.GetFileNameWithoutExtension(file) + "[MPEGTS].ts\"", 
                    Path.GetDirectoryName(file));
                if (File.Exists(Path.GetDirectoryName(file) + "\\" + Path.GetFileNameWithoutExtension(file) + "[MPEGTS].ts"))
                {
                    File.Delete(file);
                    File.Move(Path.GetDirectoryName(file) + "\\" + Path.GetFileNameWithoutExtension(file) + "[MPEGTS].ts", file);
                }
            }
            else if (Global.VIDEO_TYPE == "H265")
            {
                Run(FFMPEG_PATH,
                    "-loglevel quiet -i \"" + file + "\" -map 0 -c copy -copy_unknown -f mpegts -bsf:v hevc_mp4toannexb \""
                    + Path.GetFileNameWithoutExtension(file) + "[MPEGTS].ts\"",
                    Path.GetDirectoryName(file));
                if (File.Exists(Path.GetDirectoryName(file) + "\\" + Path.GetFileNameWithoutExtension(file) + "[MPEGTS].ts"))
                {
                    File.Delete(file);
                    File.Move(Path.GetDirectoryName(file) + "\\" + Path.GetFileNameWithoutExtension(file) + "[MPEGTS].ts", file);
                }
            }
            else
            {
                LOGGER.WriteLineError("Unkown Video Type");
            }
        }

        public static void Run(string path, string args, string workDir)
        {
            string nowDir = Directory.GetCurrentDirectory();  //当前工作路径
            Directory.SetCurrentDirectory(workDir);
            Process p = new Process();//建立外部调用线程
            p.StartInfo.FileName = path;//要调用外部程序的绝对路径
            Environment.SetEnvironmentVariable("FFREPORT", "file=" + ReportFile + ":level=32"); //兼容XP系统
            //p.StartInfo.Environment.Add("FFREPORT", "file=" + ReportFile + ":level=32");
            p.StartInfo.Arguments = args;//参数(这里就是FFMPEG的参数了)
            p.StartInfo.UseShellExecute = false;//不使用操作系统外壳程序启动线程(一定为FALSE,详细的请看MSDN)
            p.StartInfo.RedirectStandardError = true;//把外部程序错误输出写到StandardError流中(这个一定要注意,FFMPEG的所有输出信息,都为错误输出流,用StandardOutput是捕获不到任何消息的...这是我耗费了2个多月得出来的经验...mencoder就是用standardOutput来捕获的)
            p.StartInfo.CreateNoWindow = false;//不创建进程窗口
            p.ErrorDataReceived += new DataReceivedEventHandler(Output);//外部程序(这里是FFMPEG)输出流时候产生的事件,这里是把流的处理过程转移到下面的方法中,详细请查阅MSDN
            p.StartInfo.StandardErrorEncoding = Encoding.UTF8;
            p.Start();//启动线程
            p.BeginErrorReadLine();//开始异步读取
            p.WaitForExit();//阻塞等待进程结束
            p.Close();//关闭进程
            p.Dispose();//释放资源
            Environment.SetEnvironmentVariable("FFREPORT", null); //兼容XP系统
            Directory.SetCurrentDirectory(nowDir);
        }

        private static void Output(object sendProcess, DataReceivedEventArgs output)
        {
            if (!String.IsNullOrEmpty(output.Data))
            {
                LOGGER.PrintLine(output.Data, LOGGER.Warning);
            }
        }

        public static bool CheckMPEGTS(string file)
        {
            //放行杜比视界或纯音频文件
            if (Global.VIDEO_TYPE == "DV" || Global.AUDIO_TYPE != "")
                return true;
            //如果是多分片，也认为不是MPEGTS
            if (DownloadManager.PartsCount > 1)
                return false;

            using (FileStream fs = new FileStream(file, FileMode.Open, FileAccess.Read)) 
            {
                byte[] firstByte = new byte[1];
                fs.Read(firstByte, 0, 1);
                //第一字节的16进制字符串
                string _1_byte_str = Convert.ToString(firstByte[0], 16);
                //syncword不为47就不处理
                if (_1_byte_str != "47")
                    return false;
            }
            return true;
        }
    }
}
