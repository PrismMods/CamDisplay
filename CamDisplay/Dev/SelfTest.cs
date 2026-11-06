#if DEBUG
using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.UIElements;

namespace CamDisplay
{
    /* DEBUG builds only: an in-game check of the whole mod, run when a "selftest" file sits in the
       data dir. PASS/FAIL lines go to CamDisplayLog.txt, then the game quits. Settings are put back
       as they were. Release builds (release.sh) do not contain this file's code. */
    internal sealed class SelfTest : MonoBehaviour
    {
        int _pass, _fail;

        internal static void MaybeStart()
        {
            if (!File.Exists(Path.Combine(Main.DataDir, "selftest"))) return;
            // Unattended runs happen while the Mac is locked and the game is never focused; without
            // this Unity pauses and the test never advances.
            Application.runInBackground = true;
            var go = new GameObject("CamDisplaySelfTest");
            DontDestroyOnLoad(go);
            go.AddComponent<SelfTest>();
        }

        void Check(bool ok, string what, string detail = "")
        {
            if (ok) _pass++; else _fail++;
            Log.Info((ok ? "PASS " : "FAIL ") + what + (ok || detail == "" ? "" : "  [" + detail + "]"));
        }

        static RectTransform FeedRect() => GameObject.Find("CamDisplayFeed")?.GetComponent<RectTransform>();
        static bool Shown() { var r = FeedRect(); return r != null && r.gameObject.activeInHierarchy; }

        IEnumerator WaitFor(Func<bool> cond, float seconds)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (!cond() && Time.realtimeSinceStartup < end) yield return null;
        }

        IEnumerator Start()
        {
            string json = JsonUtility.ToJson(Main.Settings);   // restored at the end
            Log.Info("SELFTEST start, scene " + SceneWatch.Scene + ", screen " + Screen.width + "x" + Screen.height);
            Check(Main.Loader != null, "the mod loaded via " + Main.Loader);
            Check(PrismBridge.Available, "PrismLib is available and wired");
            for (int i = 0; i < 5; i++) yield return null;
            var getKey = typeof(Input).GetMethod("GetKey", new[] { typeof(KeyCode) });
            var info = HarmonyLib.Harmony.GetPatchInfo(getKey);
            Log.Info("  Input.GetKey patched by: " + (info == null ? "nobody" : string.Join(",", info.Owners)));
            Check(info != null && info.Postfixes.Count > 0, "the input gate is installed (by CamDisplay when standalone)",
                  info == null ? "no patches" : string.Join(",", info.Owners));

            // Feed
            yield return WaitFor(() => Main.Feed?.Texture != null, 15f);
            var t = Main.Feed?.Texture;
            Check(t != null && t.width > 16, "the camera feed is live", "status='" + Main.Feed?.Status + "'");
            Check(string.IsNullOrEmpty(Main.Feed?.Status), "no status message while the feed is fine", Main.Feed?.Status);

            // Context: wait for a real scene, then the cam follows the per-context toggles
            yield return WaitFor(() => SceneWatch.Scene == "scnLevelSelect" || SceneWatch.Scene == "scnCLS", 20f);
            for (int i = 0; i < 30; i++) yield return null;
            var ctx = SceneWatch.Current();
            Log.Info("  " + SceneWatch.Describe() + " -> " + ctx);
            Check(ctx == CamContextKind.LevelSelect, "the level select is classified as level select", SceneWatch.Describe());
            Main.Settings.ShowInLevelSelect = false; Main.Settings.ShowInMenus = false; Main.SaveSoon();
            for (int i = 0; i < 10; i++) yield return null;
            Check(!Shown(), "hidden in " + ctx + " by default");
            Main.Settings.ShowInLevelSelect = true; Main.Settings.ShowInMenus = true; Main.SaveSoon();
            for (int i = 0; i < 10; i++) yield return null;
            Check(Shown(), "shown once that context is switched on");

            // Placement and flips reach the RawImage
            var box = CamGeometry.Box(Main.Settings, Screen.width, Screen.height, t.width, t.height);
            var rt = FeedRect();
            Check(rt != null && Mathf.Abs(rt.anchoredPosition.x - box[0]) < 1f && Mathf.Abs(rt.sizeDelta.y - box[3]) < 1f,
                  "the cam sits where the settings say", rt == null ? "no rect" : rt.anchoredPosition + " " + rt.sizeDelta);
            var img = rt.GetComponent<RawImage>();
            Check(img.uvRect.width < 0f && img.uvRect.height < 0f, "both flips are on by default (negative uv extents)", img.uvRect.ToString());

            // Layout target drives the settings and the view
            var target = PrismBridge.CamTarget();
            target.MoveTo(Screen.width / 2f, Screen.height / 2f);
            target.SetCrop(new[] { 0.1f, 0f, 0.2f, 0f });
            for (int i = 0; i < 5; i++) yield return null;
            Check(Mathf.Abs(rt.anchoredPosition.x - Screen.width / 2f) < 1f && Mathf.Abs(rt.anchoredPosition.y - Screen.height / 2f) < 1f,
                  "moving the layout target moves the cam", rt.anchoredPosition.ToString());
            Check(Mathf.Abs(img.uvRect.x - 0.9f) < 0.01f && Mathf.Abs(img.uvRect.width + 0.7f) < 0.01f, "crop reaches the uv rect, in displayed space under the flip", img.uvRect.ToString());
            yield return new WaitForSecondsRealtime(1.5f);
            string saved = File.ReadAllText(Path.Combine(Main.DataDir, "CamDisplay.json"));
            Check(saved.Contains("\"CropR\": 0.2"), "the change is written to CamDisplay.json within a second", saved.Length + " bytes");

            // Shared layout editor and settings window open without errors
            PrismBridge.OpenLayoutEditor();
            for (int i = 0; i < 10; i++) yield return null;
            Check(PrismBridge.EditorOpen, "the layout editor opens");
            PrismBridge.ToggleSettings();
            for (int i = 0; i < 10; i++) yield return null;
            Check(PrismBridge.SettingsOpen, "the settings window opens");
            int rows = 0; foreach (var r in SettingsRows.Build()) rows++;
            Check(rows >= 10, "the settings window has its rows", rows + " rows");
            // Nothing in the settings window runs past its right edge, and the scroll view clips
            var doc = FindObjectsByType<UnityEngine.UIElements.UIDocument>(FindObjectsSortMode.None)
                .FirstOrDefault(d => d.gameObject.name == "CamDisplayPanel");
            var win = doc != null ? doc.rootVisualElement.Children().FirstOrDefault() : null;
            if (win != null)
            {
                // A text element's box can stay narrow while its glyphs run on (a long value in a
                // fixed-width field), so measure the text itself against the room it has.
                var cut = win.Query<UnityEngine.UIElements.TextElement>().ToList()
                    .Where(t =>
                    {
                        if (t.resolvedStyle.display != UnityEngine.UIElements.DisplayStyle.Flex || string.IsNullOrEmpty(t.text)) return false;
                        // Sticking out of the field (or row) that holds it, or squeezed below its own width.
                        var host = (UnityEngine.UIElements.VisualElement)t.GetFirstAncestorOfType<UnityEngine.UIElements.TextField>() ?? t.parent;
                        bool outside = host != null && host != t && t.worldBound.xMax > host.worldBound.xMax + 1f;
                        // A text field's text never wraps, whatever whiteSpace says.
                        bool singleLine = t.GetFirstAncestorOfType<UnityEngine.UIElements.TextField>() != null
                            || t.resolvedStyle.whiteSpace != UnityEngine.UIElements.WhiteSpace.Normal;
                        bool squeezed = singleLine
                            && t.MeasureTextSize(t.text, 0f, UnityEngine.UIElements.VisualElement.MeasureMode.Undefined, 0f, UnityEngine.UIElements.VisualElement.MeasureMode.Undefined).x > t.contentRect.width + 2f;
                        return outside || squeezed;
                    })
                    .Select(t => "'" + (t.text.Length > 24 ? t.text.Substring(0, 24) + "…" : t.text) + "'").ToList();
                Check(cut.Count == 0, "no text in the settings window is cut off", string.Join(", ", cut));
                var sv = win.Q<UnityEngine.UIElements.ScrollView>();
                Check(sv != null && sv.style.overflow.value == UnityEngine.UIElements.Overflow.Hidden, "the settings scroll view clips its rows",
                      sv == null ? "no scroll view" : sv.style.overflow.value.ToString());
            }
            else Check(false, "the settings window is found for the layout checks");

            // Escape closes the layout editor, and it opens again afterwards
            PrismLib.UI.Toolkit.Ui.CloseTop();   // what Escape does: the editor is the top window
            for (int i = 0; i < 3; i++) yield return null;
            Check(!PrismBridge.EditorOpen, "Escape closes the layout editor");
            PrismBridge.OpenLayoutEditor();
            for (int i = 0; i < 5; i++) yield return null;
            Check(PrismBridge.EditorOpen, "the layout editor opens again after Escape");

            PrismBridge.ToggleSettings();
            PrismBridge.CloseLayoutEditor();
            for (int i = 0; i < 5; i++) yield return null;

            // A scene change (every level retry is one) must not stop the feed: restarting costs a
            // main-thread Play() right at the countdown.
            float t0 = Time.realtimeSinceStartup;
            int restarts = Main.Feed.Restarts;
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
            yield return new WaitForSecondsRealtime(1f);
            yield return WaitFor(() => Main.Feed?.Texture != null, 10f);
            Check(Main.Feed?.Texture != null, "the feed is live after a scene change",
                  "after " + (Time.realtimeSinceStartup - t0).ToString("0.0") + " s, status '" + Main.Feed?.Status + "'");
            // Unity stops the camera on every scene change (DontUnloadUnusedAsset did not prevent it),
            // so the restart has to land in the load transition, a frame or two after the change.
            Check(Main.Feed.Restarts == restarts + 1 && Main.Feed.LastRestartLag >= 0 && Main.Feed.LastRestartLag <= 2,
                  "one restart, within 2 frames of the scene change", (Main.Feed.Restarts - restarts) + " restart(s), lag " + Main.Feed.LastRestartLag + " frames");

            // A camera that never delivers frames says so
            Main.Feed.OpenForTest("No Such Camera");
            yield return WaitFor(() => (Main.Feed.Status ?? "").Contains("not delivering"), 9f);
            Check((Main.Feed.Status ?? "").Contains("not delivering"), "a camera that sends no frames shows a status", "'" + Main.Feed.Status + "'");
            Main.Feed.Restart(Main.Settings);
            yield return WaitFor(() => Main.Feed?.Texture != null, 10f);
            Check(Main.Feed.Texture != null && string.IsNullOrEmpty(Main.Feed.Status), "the real camera comes back with a clear status", "'" + Main.Feed.Status + "'");

            // Frame time with the cam on vs off, here in a menu (the dense-level check is manual)
            Main.Settings.ShowInLevelSelect = Main.Settings.ShowInMenus = false; Main.SaveSoon();
            float off = 0; yield return Measure(v => off = v);
            Main.Settings.ShowInLevelSelect = Main.Settings.ShowInMenus = true; Main.SaveSoon();
            float on = 0; yield return Measure(v => on = v);
            Log.Info("  p99 frame time: cam off " + off.ToString("0.00") + " ms, on " + on.ToString("0.00") + " ms");
            Check(on - off <= 0.5f, "showing the cam costs at most 0.5 ms of p99 frame time here", "off " + off + " on " + on);

            // The editor scene: hidden by default there
            Main.Settings.ShowInEditor = false; Main.SaveSoon();
            SceneManager.LoadScene("scnEditor");
            yield return WaitFor(() => SceneWatch.Scene == "scnEditor", 15f);
            for (int i = 0; i < 60; i++) yield return null;
            Log.Info("  " + SceneWatch.Describe() + " -> " + SceneWatch.Current());
            Check(SceneWatch.Current() == CamContextKind.Editor, "the level editor is classified as the editor", SceneWatch.Describe());
            Check(!Shown(), "and the cam is hidden there by default");

#if CAMDISPLAY_UMM
            // Off in UMM means off: no camera, no cam, no windows, nothing left registered
            yield return WaitFor(() => Main.Feed?.Texture != null, 10f);
            PrismBridge.OpenLayoutEditor();
            for (int i = 0; i < 5; i++) yield return null;
            LoaderUmm.Entry.Active = false;
            for (int i = 0; i < 10; i++) yield return null;
            Check(!Main.Feed.HasCamera, "turning CamDisplay off in UMM releases the camera");
            Check(!Shown(), "and hides the cam");
            Check(!PrismBridge.EditorOpen && !PrismBridge.SettingsOpen, "and closes its windows");
            Check(!LayoutHasCam(), "and takes the cam out of the shared layout editor");
            yield return new WaitForSecondsRealtime(3f);
            Check(!Main.Feed.HasCamera, "the camera stays off while CamDisplay is off");
            LoaderUmm.Entry.Active = true;
            yield return WaitFor(() => Main.Feed?.Texture != null, 10f);
            Check(Main.Feed.Texture != null && LayoutHasCam(), "turning it back on brings the camera and the layout target back");
#endif

            if (MacPatch.IsMac)
            {
                Check(MacPatch.Status() == "patched", "the running game reads as camera-patched", MacPatch.Status());
                Check(Launcher.Install() == "" && File.Exists(Launcher.Path), "the launcher is installed in the game folder");
                Check(Launcher.Line() == MacPatchPlan.LaunchLine(MacPatch.GameDir, "setup_helper.sh"), "the launch line wraps this install's setup_helper.sh", Launcher.Line());
                PrismLib.UI.Toolkit.SettingRow copy = null; int mac = 0;
                foreach (var r in SettingsRows.Build()) if (r.Group == "macOS camera") { mac++; if (r.Label == "Copy launch options") copy = r; }
                Check(mac == 5, "the settings window has the macOS camera group", mac + " rows");
                if (copy != null) { copy.Set(null); }
                Check(GUIUtility.systemCopyBuffer == Launcher.Line(), "Copy puts the launch line on the clipboard");
                Check(MacPatch.ScheduleAfterExit("undo", "apply") == "", "undo + apply are queued for game exit (checked from outside after quit)");
            }

            JsonUtility.FromJsonOverwrite(json, Main.Settings);
            Main.SaveSoon();
            yield return new WaitForSecondsRealtime(1.5f);
            Log.Info("SELFTEST DONE pass=" + _pass + " fail=" + _fail);
            Application.Quit();
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        static bool LayoutHasCam()
        {
            if (!PrismBridge.Available) return false;
            foreach (var kv in PrismLib.Layout.All()) if (kv.Key == "CamDisplay") return true;
            return false;
        }

        IEnumerator Measure(Action<float> result)
        {
            for (int i = 0; i < 30; i++) yield return null;
            var ms = new float[600];
            for (int i = 0; i < ms.Length; i++) { yield return null; ms[i] = Time.unscaledDeltaTime * 1000f; }
            Array.Sort(ms);
            result(ms[(int)(ms.Length * 0.99f)]);
        }
    }
}
#endif
