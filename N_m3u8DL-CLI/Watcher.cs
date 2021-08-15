using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;

namespace N_m3u8DL_CLI
{
    class Watcher
    {
        private string dir = string.Empty;
        private int total = 0;
        private static double totalDuration = 0; //总时长
        private int now = 0;
        private int partsCount = 0;
        FileSystemWatcher watcher = new FileSystemWatcher();

        public int Total { get => total; set => total = value; }
        public int Now { get => now; set => now = value; }
        public int PartsCount { get => partsCount; set => partsCount = value; }
        public static double TotalDuration { get => totalDuration; set => totalDuration = value; }

        public Watcher(string Dir)
        {
            this.dir = Dir;
        }

        public void WatcherStrat()
        {
            for (int i = 0; i < PartsCount; i++)
            {
                Now += Global.GetFileCount(dir + "\\Part_" + i.ToString(DownloadManager.partsPadZero), ".ts");
            }
            watcher.Path = dir;
            watcher.Filter = "*.ts";
            watcher.IncludeSubdirectories = true;  //包括子目录
            watcher.EnableRaisingEvents = true;    //开启提交事件
            watcher.Created += new FileSystemEventHandler(OnCreated);
            watcher.Renamed += new RenamedEventHandler(OnCreated);
            watcher.Deleted += new FileSystemEventHandler(OnDeleted);
        }

        public void WatcherStop()
        {
            watcher.Dispose();
        }

        private void OnCreated(object source, FileSystemEventArgs e)
        {
            if (Path.GetFileNameWithoutExtension(e.FullPath).StartsWith("Part"))
                return;
            Now++;
            if (Now > Total)
            {
                return;
            }
            //Console.Title = Now + "   /   " + Total;
            string downloadedSize = Global.FormatFileSize(DownloadManager.DownloadedSize);
            string estimatedSize = Global.FormatFileSize(DownloadManager.DownloadedSize * total / now);
            int padding = downloadedSize.Length > estimatedSize.Length ? downloadedSize.Length : estimatedSize.Length;
            DownloadManager.ToDoSize = (DownloadManager.DownloadedSize * total / now) - DownloadManager.DownloadedSize;
            string percent = (Convert.ToDouble(now) / Convert.ToDouble(total) * 100).ToString("0.00") + "%";
            var print = "Progress: " + Now + "/" + Total
                + $" ({percent}) -- {downloadedSize.PadLeft(padding)}/{estimatedSize.PadRight(padding)}";
            ProgressReporter.Report(print, "");
        }

        private void OnRenamed(object source, RenamedEventArgs e)
        {
            if (Path.GetFileNameWithoutExtension(e.FullPath).StartsWith("Part"))
                return;
            Now++;
            if (Now > Total)
            {
                return;
            }
            //Console.Title = Now + "   /   " + Total;
            string downloadedSize = Global.FormatFileSize(DownloadManager.DownloadedSize);
            string estimatedSize = Global.FormatFileSize(DownloadManager.DownloadedSize * total / now);
            int padding = downloadedSize.Length > estimatedSize.Length ? downloadedSize.Length : estimatedSize.Length;
            DownloadManager.ToDoSize = (DownloadManager.DownloadedSize * total / now) - DownloadManager.DownloadedSize;
            string percent = (Convert.ToDouble(now) / Convert.ToDouble(total) * 100).ToString("0.00") + "%";
            var print = "Progress: " + Now + "/" + Total
                + $" ({percent}) -- {downloadedSize.PadLeft(padding)}/{estimatedSize.PadRight(padding)}";
            ProgressReporter.Report(print, "");
        }

        private void OnDeleted(object source, FileSystemEventArgs e)
        {
            if (Path.GetFileNameWithoutExtension(e.FullPath).StartsWith("Part"))
                return;
            Now--;
            if (Now > Total)
            {
                return;
            }
            //Console.Title = Now + "   /   " + Total;
            string downloadedSize = Global.FormatFileSize(DownloadManager.DownloadedSize);
            string estimatedSize = Global.FormatFileSize(DownloadManager.DownloadedSize * total / now);
            int padding = downloadedSize.Length > estimatedSize.Length ? downloadedSize.Length : estimatedSize.Length;
            DownloadManager.ToDoSize = (DownloadManager.DownloadedSize * total / now) - DownloadManager.DownloadedSize;
            string percent = (Convert.ToDouble(now) / Convert.ToDouble(total) * 100).ToString("0.00") + "%";
            var print = "Progress: " + Now + "/" + Total
                + $" ({percent}) -- {downloadedSize.PadLeft(padding)}/{estimatedSize.PadRight(padding)}";
            ProgressReporter.Report(print, "");
        }
    }
}
