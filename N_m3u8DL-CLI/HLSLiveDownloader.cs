using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Timers;

namespace N_m3u8DL_CLI
{
    class HLSLiveDownloader
    {
        public static int REC_DUR_LIMIT = -1; //默认不限制录制时长
        public static double REC_DUR = 0; //已录制时长
        private string liveFile = string.Empty;
        private string jsonFile = string.Empty;
        private string headers = string.Empty;
        private string downDir = string.Empty;
        private FileStream liveStream = null;
        private double targetduration = 10;
        private bool isFirstJson = true;

        public double TotalDuration { get; set; }
        public string Headers { get => headers; set => headers = value; }
        public string DownDir { get => downDir; set => downDir = value; }
        public FileStream LiveStream { get => liveStream; set => liveStream = value; }
        public string LiveFile { get => liveFile; set => liveFile = value; }

        ArrayList toDownList = new ArrayList();  //所有待下载的列表
        System.Timers.Timer timer = new System.Timers.Timer();
        Downloader sd = new Downloader();  //只有一个实例

        public void TimerStart()
        {
            timer.Enabled = true;
            //timer.Interval = (targetduration - 2) * 1000; //执行间隔时间,单位为毫秒
            timer.Start();
            timer.Elapsed += new ElapsedEventHandler(UpdateList);
            UpdateList(timer, new EventArgs());  //立即执行一次
            Record();
        }

        public void TimerStop()
        {
            timer.Stop();
        }

        //更新列表
        private void UpdateList(object source, EventArgs e)
        {
            jsonFile = Path.Combine(DownDir, "meta.json");
            if (!File.Exists(jsonFile)) 
            {
                TimerStop();
                return;
            }
            string jsonContent = File.ReadAllText(jsonFile);
            JObject initJson = JObject.Parse(jsonContent);
            string m3u8Url = initJson["m3u8"].Value<string>();
            targetduration = initJson["m3u8Info"]["targetDuration"].Value<double>();
            TotalDuration = initJson["m3u8Info"]["totalDuration"].Value<double>();
            timer.Interval = (TotalDuration - targetduration) * 1000;//设置定时器运行间隔
            JArray lastSegments = JArray.Parse(initJson["m3u8Info"]["segments"][0].ToString().Trim());  //上次的分段，用于比对新分段
            ArrayList tempList = new ArrayList();  //所有待下载的列表
            tempList.Clear();
            foreach (JObject seg in lastSegments)
            {
                tempList.Add(seg.ToString());
            }

            if(isFirstJson)
            {
                toDownList = tempList;
                isFirstJson = false;
                return;
            }

            Parser parser = new Parser();
            parser.DownDir = Path.GetDirectoryName(jsonFile);
            parser.M3u8Url = m3u8Url;
            parser.LiveStream = true;
            parser.Parse();  //产生新的json文件

            jsonContent = File.ReadAllText(jsonFile);
            initJson = JObject.Parse(jsonContent);
            JArray segments = JArray.Parse(initJson["m3u8Info"]["segments"][0].ToString());  //大分组
            foreach (JObject seg in segments)
            {
                if (!tempList.Contains(seg.ToString()))
                {
                    toDownList.Add(seg.ToString());  //加入真正的待下载队列
                    //Console.WriteLine(seg.ToString());
                }
            }
            if (toDownList.Count > 0)
                Record();
        }

        //public void TryDownload()
        //{
        //    Thread t = new Thread(Download);
        //    while (toDownList.Count != 0)
        //    {
        //        t = new Thread(Download);
        //        t.Start();
        //        t.Join();
        //        while (sd.IsDone != true) ;  //忙等待
        //        if (toDownList.Count > 0)
        //            toDownList.RemoveAt(0);  //下完删除一项
        //    }
        //    Console.WriteLine("Waiting...");
        //}

        private void Record()
        {
            while (toDownList.Count > 0 && (sd.FileUrl != "" ? sd.IsDone : true)) 
            {
                JObject info = JObject.Parse(toDownList[0].ToString());
                int index = info["index"].Value<int>();
                sd.FileUrl = info["segUri"].Value<string>();
                sd.Method = info["method"].Value<string>();
                if (sd.Method != "NONE")
                {
                    sd.Key = info["key"].Value<string>();
                    sd.Iv = info["iv"].Value<string>();
                }
                sd.TimeOut = (int)timer.Interval - 1000;//超时时间不超过下次执行时间
                sd.SegIndex = index;
                sd.Headers = Headers;
                sd.SegDur = info["duration"].Value<double>();
                sd.IsLive = true;  //标记为直播
                sd.LiveFile = LiveFile;
                sd.LiveStream = LiveStream;
                sd.Down();  //开始下载
                while (sd.IsDone != true) { Thread.Sleep(1); };  //忙等待 Thread.Sleep(1) 可防止cpu 100% 防止电脑风扇狂转
                if (toDownList.Count > 0)
                    toDownList.RemoveAt(0);  //下完删除一项
            }
            LOGGER.PrintLine("Waiting...", LOGGER.Warning);
            LOGGER.WriteLine("Waiting...");
        }

        //检测是否有新分片
        private bool isNewSeg()
        {
            if (toDownList.Count > 0)
                return true;
            return false;
        }
    }
}
