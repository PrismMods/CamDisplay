using System;
using System.IO;

namespace CamDisplay
{
    // One log for both loaders: CamDisplayLog.txt in the data dir, mirrored to the loader's logger.
    internal static class Log
    {
        internal static string Path;
        internal static Action<string> Sink;

        internal static void Info(string m) => Write(m);

        internal static void Error(string m, Exception e = null) => Write("ERROR " + m + (e == null ? "" : ": " + e));

        private static void Write(string m)
        {
            try { if (Path != null) File.AppendAllText(Path, DateTime.Now.ToString("HH:mm:ss.fff") + " " + m + Environment.NewLine); }
            catch { }
            try { Sink?.Invoke(m); } catch { }
        }
    }
}
