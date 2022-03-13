using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace N_m3u8DL_CLI
{
    class ProgressReporter
    {
        private static string speed = "";
        private static string progress = "";

        static object lockThis = new object();
        public static void Report(string progress, string speed)
        {
            lock (lockThis)
            {
                int windowWith = 63;
                try
                {
                    windowWith = Console.WindowWidth;
                }
                catch (Exception e)
                {
                    // empty
                }
                if (!string.IsNullOrEmpty(progress)) ProgressReporter.progress = progress;
                if (!string.IsNullOrEmpty(speed)) ProgressReporter.speed = speed;
                string now = DateTime.Now.ToString("HH:mm:ss.000");
                var sub = windowWith - 4 - ProgressReporter.progress.Length - ProgressReporter.speed.Length - now.Length;
                if (sub <= 0) sub = 0;
                string print = now + " " + ProgressReporter.progress + " " + ProgressReporter.speed + new string(' ', sub);
                Console.Write("\r" + print + "\r");
                //Console.Write(print);
            }
        }
    }
}
