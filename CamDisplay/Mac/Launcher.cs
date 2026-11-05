using System;
using System.Diagnostics;
using System.IO;

namespace CamDisplay
{
    /* CamDisplayLaunch (native/launch): starts the game as its own TCC responsible process instead
       of Steam's child, so the camera entitlement is the game's to use. The mod writes it into the
       game folder itself — a file the game writes is not quarantined, so Gatekeeper never blocks
       it — and ad-hoc signs it, which arm64 needs to run it at all. Safe while the game runs. */
    internal static class Launcher
    {
        internal static string Path => System.IO.Path.Combine(MacPatch.GameDir, MacPatchPlan.Launcher);

        /// "" or an error.
        internal static string Install()
        {
            try
            {
                if (Resource.WriteIfChanged("CamDisplay.CamDisplayLaunch", Path))
                {
                    Run("/bin/chmod", "755 " + MacPatch.Q(Path));
                    Run("/usr/bin/codesign", "--force --sign - " + MacPatch.Q(Path));
                    Log.Info("installed " + Path);
                }
                return "";
            }
            catch (Exception e) { Log.Error("launcher install", e); return e.Message; }
        }

        /// The Steam launch options line for this install.
        internal static string Line()
        {
            string dir = MacPatch.GameDir;
            return MacPatchPlan.LaunchLine(dir, MacPatchPlan.PickHelper(f => File.Exists(System.IO.Path.Combine(dir, f))));
        }

        internal static int Run(string exe, string args)
        {
            var p = Process.Start(new ProcessStartInfo(exe, args) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true });
            p.StandardError.ReadToEnd();
            if (!p.WaitForExit(15000)) { try { p.Kill(); } catch { } throw new TimeoutException(exe + " timed out"); }
            if (p.ExitCode != 0) throw new Exception(System.IO.Path.GetFileName(exe) + " exited " + p.ExitCode);
            return p.ExitCode;
        }
    }
}
