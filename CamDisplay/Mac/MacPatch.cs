using System;
using System.Diagnostics;
using System.IO;
using UnityEngine;

namespace CamDisplay
{
    /* The macOS camera patch, done by the same tools/mac-camera-patch.sh that has its own tests
       (embedded in the mod, written to the data dir). Reading the state is safe while the game
       runs; changing it is not proven safe (re-signing or replacing a running executable can get
       the process killed), so Apply and Undo are queued to run the moment the game exits. */
    internal static class MacPatch
    {
        internal static bool IsMac => Application.platform == RuntimePlatform.OSXPlayer;

        /// …/ADanceOfFireAndIce.app. On macOS dataPath is …/X.app/Contents; walk up to the bundle
        /// rather than counting levels.
        internal static string AppPath
        {
            get
            {
                var d = new DirectoryInfo(Application.dataPath);
                while (d != null && !d.Name.EndsWith(".app", StringComparison.OrdinalIgnoreCase)) d = d.Parent;
                return d?.FullName;
            }
        }
        internal static string GameDir => Path.GetDirectoryName(AppPath);

        private static string ScriptPath => Path.Combine(Main.DataDir, "mac-camera-patch.sh");
        internal static string LogPath => Path.Combine(Main.DataDir, "mac-camera-patch.log");

        private static string _status;
        private static float _statusAt = -100f;

        /// "patched", "stock", or null when it could not be read. Cached for 5 s: codesign is not free.
        internal static string Status()
        {
            if (Time.unscaledTime - _statusAt < 5f) return _status;
            _statusAt = Time.unscaledTime;
            try
            {
                WriteScript();
                var p = Process.Start(new ProcessStartInfo("/bin/bash", Q(ScriptPath) + " status " + Q(AppPath))
                    { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true });
                string o = p.StandardOutput.ReadToEnd().Trim();
                if (!p.WaitForExit(15000)) { try { p.Kill(); } catch { } return _status = null; }
                return _status = o == "patched" || o == "stock" ? o : null;
            }
            catch (Exception e) { Log.Error("camera patch status", e); return _status = null; }
        }

        internal static bool NeedsPatch() => Status() == "stock";

        /// Queue the patch (or its undo) for the moment the game exits. "" or an error.
        internal static string ScheduleAfterExit(params string[] commands)
        {
            try
            {
                WriteScript();
                int pid = Process.GetCurrentProcess().Id;
                string job = Path.Combine(Main.DataDir, "mac-camera-patch-job.sh");
                var body = "#!/bin/bash\n# Written by CamDisplay: waits for the game (pid " + pid + ") to exit, then runs the patch.\n"
                         + "(\n  while kill -0 " + pid + " 2>/dev/null; do sleep 0.5; done\n  echo \"--- $(date) after game exit\"\n";
                foreach (var c in commands) body += "  /bin/bash " + Sh(ScriptPath) + " " + c + " " + Sh(AppPath) + "\n";
                body += ") >> " + Sh(LogPath) + " 2>&1 &\n";
                File.WriteAllText(job, body);
                Process.Start(new ProcessStartInfo("/usr/bin/nohup", "/bin/bash " + Q(job)) { UseShellExecute = false, CreateNoWindow = true });
                _statusAt = -100f;
                Log.Info("camera patch queued for game exit: " + string.Join(", ", commands));
                return "";
            }
            catch (Exception e) { Log.Error("camera patch schedule", e); return e.Message; }
        }

        private static void WriteScript() => Resource.WriteIfChanged("CamDisplay.mac-camera-patch.sh", ScriptPath);

        // Mono splits Arguments on Windows rules: double quotes around a path with spaces.
        internal static string Q(string s) => "\"" + s + "\"";
        // Inside a bash script: single quotes, with any ' escaped.
        private static string Sh(string s) => "'" + s.Replace("'", "'\\''") + "'";
    }

    internal static class Resource
    {
        /// Copy an embedded resource to disk unless the same bytes are already there.
        internal static bool WriteIfChanged(string name, string path)
        {
            using (var s = typeof(Resource).Assembly.GetManifestResourceStream(name))
            {
                if (s == null) throw new InvalidOperationException("missing embedded resource " + name);
                var ms = new MemoryStream();
                s.CopyTo(ms);
                byte[] want = ms.ToArray();
                if (File.Exists(path))
                {
                    byte[] have = File.ReadAllBytes(path);
                    if (have.Length == want.Length && Convert.ToBase64String(have) == Convert.ToBase64String(want)) return false;
                }
                File.WriteAllBytes(path, want);
                return true;
            }
        }
    }
}
