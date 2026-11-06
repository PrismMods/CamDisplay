namespace CamDisplay
{
    internal enum CamContextKind { Gameplay, Menu, LevelSelect, Editor }

    // Where the player is, and whether the cam shows there. BCL only (offline-tested).
    internal static class CamContext
    {
        /* The game plays custom levels INSIDE scnEditor, in play mode (seen in-game 2026-11-06),
           so the editor scene is "editor" only while editing; in play mode it is a level like any
           other (an editor playtest included). A paused level counts as a menu; the level selects
           are the world map and the custom level browser. Everything else is a menu. */
        internal static CamContextKind Classify(string scene, bool gameworld, bool paused, bool editorPlaying)
        {
            if (scene == "scnEditor" && !editorPlaying) return CamContextKind.Editor;
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
