namespace CamDisplay
{
    internal enum CamContextKind { Gameplay, Menu, LevelSelect, Editor }

    // Where the player is, and whether the cam shows there. BCL only (offline-tested).
    internal static class CamContext
    {
        /* scnEditor is the editor even while playtesting (a clear is recorded from a real level);
           a paused level counts as a menu; the level selects are the world map and the custom
           level browser. Everything else is a menu. */
        internal static CamContextKind Classify(string scene, bool gameworld, bool paused)
        {
            if (scene == "scnEditor") return CamContextKind.Editor;
            if (gameworld && !paused) return CamContextKind.Gameplay;
            if (scene == "scnLevelSelect" || scene == "scnCLS") return CamContextKind.LevelSelect;
            return CamContextKind.Menu;
        }

        internal static bool ShouldShow(CamContextKind k, CamSettings s)
        {
            if (s == null || !s.Enabled) return false;
            switch (k)
            {
                case CamContextKind.Gameplay: return true;
                case CamContextKind.Editor: return s.ShowInEditor;
                case CamContextKind.LevelSelect: return s.ShowInLevelSelect;
                default: return s.ShowInMenus;
            }
        }

        /// The saved camera if it is still there, else the first (fellBack when a saved one was lost).
        internal static string PickDevice(string saved, string[] devices, out bool fellBack)
        {
            fellBack = false;
            if (devices == null || devices.Length == 0) return null;
            if (!string.IsNullOrEmpty(saved))
            {
                foreach (var d in devices) if (d == saved) return d;
                fellBack = true;
            }
            return devices[0];
        }
    }
}
