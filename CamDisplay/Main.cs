using System;
using System.IO;

namespace CamDisplay
{
    // Loader-agnostic core: LoaderMl and LoaderUmm only translate their loader's calls into these.
    internal static class Main
    {
        internal static string ModDir, DataDir, ModsRoot, BundlePath, Version, Loader;

        internal static void Setup(string modDir, string dataDir, string modsRoot, string bundlePath, string version, string loader)
        {
            ModDir = modDir; DataDir = dataDir; ModsRoot = modsRoot; BundlePath = bundlePath;
            Version = version; Loader = loader;
            try { Directory.CreateDirectory(dataDir); } catch { }
            Log.Path = System.IO.Path.Combine(dataDir, "CamDisplayLog.txt");
            Log.Info("CamDisplay " + version + " loaded via " + loader);
        }

        internal static void Update() { }

        internal static void OnGui() { }

        internal static void Shutdown() { Log.Info("CamDisplay shut down"); }
    }
}
