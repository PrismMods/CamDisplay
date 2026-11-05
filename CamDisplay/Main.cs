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

        private static string SettingsPath => Path.Combine(DataDir, "CamDisplay.json");
        private static float _dirtySince = -1f;

        internal static void Setup(string modDir, string dataDir, string modsRoot, string bundlePath, string version, string loader)
        {
            ModDir = modDir; DataDir = dataDir; ModsRoot = modsRoot; BundlePath = bundlePath;
            Version = version; Loader = loader;
            try { Directory.CreateDirectory(dataDir); } catch { }
            Log.Path = Path.Combine(dataDir, "CamDisplayLog.txt");
            Log.Info("CamDisplay " + version + " loaded via " + loader);
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
            Feed.StatusChanged += PrismBridge.RefreshSettings;
            Feed.Restart(Settings);
#if DEBUG
            SelfTest.MaybeStart();
#endif
        }

        internal static void Update()
        {
            try
            {
                if (Feed == null) Start();
                Feed.Tick();
                bool show = CamContext.ShouldShow(SceneWatch.Current(), Settings);
                View.Apply(Settings, Feed.Texture, show || PrismBridge.EditorOpen, SettingsVersion);
                PrismBridge.Tick();
                if (SettingsKeyDown()) PrismBridge.ToggleSettings();
                if (_dirtySince >= 0f && Time.unscaledTime - _dirtySince >= 1f) Save();
            }
            catch (Exception e) { Log.Error("update", e); }
        }

        // UMM's own mod page: one button into the real settings window.
        internal static void OnGui()
        {
            if (GUILayout.Button("Open CamDisplay settings (Ctrl+Shift+C)", GUILayout.Width(320f))) PrismBridge.ToggleSettings();
        }

        private static bool SettingsKeyDown() =>
            Input.GetKeyDown(KeyCode.C)
            && (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl))
            && (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift));

        internal static void Shutdown()
        {
            if (_dirtySince >= 0f) Save();
            Feed?.Stop();
            PrismBridge.Shutdown();
            Log.Info("CamDisplay shut down");
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
