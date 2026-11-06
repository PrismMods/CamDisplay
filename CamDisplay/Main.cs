using System;
using System.IO;
using UnityEngine;

namespace CamDisplay
{
    // Loader-agnostic core: LoaderMl and LoaderUmm only translate their loader's calls into these.
    internal static class Main
    {
        internal static string ModDir, DataDir, ModsRoot, BundlePath, Version, Loader;
        internal static CamSettings Settings = new CamSettings();
        /// Bumped on every settings change, so the view recomputes only when something moved.
        internal static int SettingsVersion;
        internal static CamFeed Feed;
        internal static CamView View;
        /// False while CamDisplay is switched off (UMM's toggle): then nothing may hold the camera.
        internal static bool Active { get; private set; } = true;

        private static string SettingsPath => Path.Combine(DataDir, "CamDisplay.json");
        private static float _dirtySince = -1f;
        private static CamContextKind _lastCtx = (CamContextKind)(-1);
        private static bool _lastShow;

        internal static void Setup(string modDir, string dataDir, string modsRoot, string bundlePath, string version, string loader)
        {
            ModDir = modDir; DataDir = dataDir; ModsRoot = modsRoot; BundlePath = bundlePath;
            Version = version; Loader = loader;
            try { Directory.CreateDirectory(dataDir); } catch { }
            Log.Path = Path.Combine(dataDir, "CamDisplayLog.txt");
            Log.Info("CamDisplay " + version + " loaded via " + loader + "; settings: " + SettingsChord);
            // Before anything names a PrismLib.UI type (PrismBridge has one in a field): one copy
            // loads per session, so every mod folder's copy is brought up to the newest first.
            PrismLib.Bootstrap.PrismBootstrap.SyncUi();
            Load();
            InitPrism(version);
        }

        // Separate and never inlined: JIT-compiling Setup must not load PrismBridge's field types
        // (PrismLib.UI) before SyncUi above has run.
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private static void InitPrism(string version) => PrismBridge.Init(version);

        // Built on the first frame, not at load: MelonLoader initialises mods before any scene.
        private static void Start()
        {
            View = new CamView();
            Feed = new CamFeed();
            MacRows.Install();
            Feed.StatusChanged += PrismBridge.RefreshSettings;
            Feed.Restart(Settings);
#if DEBUG
            SelfTest.MaybeStart();
#endif
        }

        internal static void Update()
        {
            if (!Active) return;
            try
            {
                if (Feed == null) Start();
                Feed.Tick();
                var ctx = SceneWatch.Current();
                bool show = CamContext.ShouldShow(ctx, Settings);
                if (ctx != _lastCtx || show != _lastShow)
                {
                    // Once per change: the answer to "why is the cam (not) on screen?"
                    _lastCtx = ctx; _lastShow = show;
                    Log.Info("context " + ctx + " (" + SceneWatch.Describe() + "): cam " + (show ? "shown" : "hidden"));
                }
                View.Apply(Settings, Feed.Texture, show || PrismBridge.EditorOpen, SettingsVersion);
                Gate.Tick();
                // Our own keys and the Prism polls read real input even while a window withholds it.
                PrismLib.UI.Toolkit.InputGate.Exempt(Polls);
                if (_dirtySince >= 0f && Time.unscaledTime - _dirtySince >= 1f) Save();
            }
            catch (Exception e) { Log.Error("update", e); }
        }

        // UMM's own mod page: one button into the real settings window.
        internal static void OnGui()
        {
            if (!Active) { GUILayout.Label("CamDisplay is off: the camera is not used."); return; }
            if (GUILayout.Button("Open CamDisplay settings (" + SettingsChord + ")", GUILayout.Width(320f))) PrismBridge.ToggleSettings();
        }

        private static void Polls()
        {
            PrismBridge.Tick();
            if (SettingsKeyDown()) PrismBridge.ToggleSettings();
            // Escape closes the top Prism window (only when one is open); the wheel has to be fed
            // by hand, this game's input module does not forward it to UI Toolkit.
            if (Input.GetKeyDown(KeyCode.Escape) && PrismLib.UI.Toolkit.Ui.AnyWindowOpen) PrismLib.UI.Toolkit.Ui.CloseTop();
            PrismLib.UI.Toolkit.Ui.TickWheel(Input.mousePosition, Input.mouseScrollDelta.y);
        }

        /* Alt+C, in the Prism family (Alt+T Topaz, Alt+P Prism settings, Alt+D debug): the game and
           its editor use Ctrl chords (Ctrl+S saves the level), never Alt, and Topaz lets Alt chords
           through its menu's key block. On macOS Unity drops some letters while Option is held, so
           the keys are read through PrismLib.UI's MacKeys (the window server) when it is there. */
        internal const string SettingsChord = "Alt+C";
        private static bool SettingsKeyDown() =>
            KeyDown(KeyCode.C) && (KeyHeld(KeyCode.LeftAlt) || KeyHeld(KeyCode.RightAlt));

        // MacKeys is newer than some PrismLib.UI copies another mod may have loaded: fall back to Input.
        private static bool _noMacKeys;
        private static bool KeyDown(KeyCode k)
        {
            if (!_noMacKeys) try { return MacKeyDown(k); } catch (Exception) { _noMacKeys = true; }
            return Input.GetKeyDown(k);
        }
        private static bool KeyHeld(KeyCode k)
        {
            if (!_noMacKeys) try { return MacKeyHeld(k); } catch (Exception) { _noMacKeys = true; }
            return Input.GetKey(k);
        }
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private static bool MacKeyDown(KeyCode k) => PrismLib.UI.Toolkit.MacKeys.Down(k);
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private static bool MacKeyHeld(KeyCode k) => PrismLib.UI.Toolkit.MacKeys.Held(k);

        internal static void Shutdown()
        {
            if (_dirtySince >= 0f) Save();
            Feed?.Stop();
            PrismBridge.Shutdown();
            Gate.Shutdown();
            Log.Info("CamDisplay shut down");
        }

        /// Switched off: release the camera and everything CamDisplay holds; on: take it all back.
        internal static void SetActive(bool on)
        {
            if (on == Active) return;
            Active = on;
            try
            {
                if (!on)
                {
                    Feed?.Stop();
                    View?.Apply(Settings, null, false, SettingsVersion);
                    PrismBridge.Deactivate();
                    Gate.Shutdown();
                    if (_dirtySince >= 0f) Save();
                    Log.Info("CamDisplay off: camera released");
                }
                else
                {
                    PrismBridge.Reactivate();
                    Feed?.Restart(Settings);
                    Log.Info("CamDisplay on");
                }
            }
            catch (Exception e) { Log.Error("toggle", e); }
        }

        /// A settings field changed: redraw now, write to disk within a second.
        internal static void SaveSoon()
        {
            SettingsVersion++;
            if (_dirtySince < 0f) _dirtySince = Time.unscaledTime;
        }

        private static void Load()
        {
            string json = null;
            try { if (File.Exists(SettingsPath)) json = File.ReadAllText(SettingsPath); } catch (Exception e) { Log.Error("read settings", e); }
            bool bad;
            // FromJsonOverwrite onto fresh defaults: fields an older file lacks keep their defaults.
            Settings = CamSettings.FromJson(json, j => { var s = new CamSettings(); JsonUtility.FromJsonOverwrite(j, s); return s; }, out bad);
            if (bad)
            {
                string bak = SettingsPath + "." + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".bak";
                try { File.Move(SettingsPath, bak); } catch { }
                Log.Info("settings file could not be read; defaults loaded, old file kept as " + Path.GetFileName(bak));
            }
            SettingsVersion++;
        }

        private static void Save()
        {
            _dirtySince = -1f;
            try
            {
                string tmp = SettingsPath + ".tmp";
                File.WriteAllText(tmp, JsonUtility.ToJson(Settings, true));
                if (File.Exists(SettingsPath)) File.Delete(SettingsPath);
                File.Move(tmp, SettingsPath);
            }
            catch (Exception e) { Log.Error("save settings", e); }
        }
    }
}
