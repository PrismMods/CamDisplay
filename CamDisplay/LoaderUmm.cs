#if CAMDISPLAY_UMM
using System.IO;
using UnityModManagerNet;

namespace CamDisplay
{
    // UnityModManager entry: UMMMods/CamDisplay/ holds the dll, Info.json, PrismLib.UI and data.
    public static class LoaderUmm
    {
        internal static UnityModManager.ModEntry Entry;

        public static bool Load(UnityModManager.ModEntry e)
        {
            Entry = e;
            string dir = e.Path.TrimEnd('/', '\\');
            Log.Sink = m => e.Logger.Log(m);
            Main.Setup(dir, dir, Path.GetDirectoryName(dir), Path.Combine(dir, "PrismLib.bundle"),
                       e.Info.Version, "UMM");
            e.OnUpdate = (m, dt) => Main.Update();
            e.OnGUI = m => Main.OnGui();
            e.OnUnload = m => { Main.Shutdown(); return true; };
            // Off in UMM's list means the camera is released, not merely the cam hidden.
            e.OnToggle = (m, on) => { Main.SetActive(on); return true; };
            if (!e.Enabled) Main.SetActive(false);
            return true;
        }
    }
}
#endif
