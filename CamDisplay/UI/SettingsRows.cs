using System;
using System.Collections.Generic;
using PrismLib.UI.Toolkit;

namespace CamDisplay
{
    /* CamDisplay's own settings window, built from PrismLib.UI rows. Its own SettingsPanel, not the
       shared Alt+Shift+S one: that window is drawn through whichever mod holds the debug-panel
       claim, with that mod's converter, which turns unknown kinds into toggles and drops the
       status line. PrismLib.UI ships beside the mod, so naming its types here is safe. */
    internal static class SettingsRows
    {
        private static readonly int[][] Resolutions =
        {
            new[] { 640, 360, 30 }, new[] { 1280, 720, 30 }, new[] { 1920, 1080, 30 }, new[] { 1280, 720, 60 },
        };
        private static readonly string[] ResolutionNames = { "640×360 @ 30", "1280×720 @ 30", "1920×1080 @ 30", "1280×720 @ 60" };

        internal const string Page = "CamDisplay";

        /// Extra rows (the macOS group) appended by other parts of the mod.
        internal static Func<IEnumerable<SettingRow>> Extra;

        internal static IEnumerable<SettingRow> Build()
        {
            var s = Main.Settings;
            var feed = Main.Feed;
            string[] devices = feed != null ? feed.Devices() : new string[0];
            if (devices.Length == 0) devices = new[] { "(no camera found)" };

            yield return Bool("Camera", "Enabled", () => s.Enabled, v => { s.Enabled = v; Changed(restart: true); });
            yield return new SettingRow
            {
                Owner = Page, Page = Page, Group = "Camera", Label = "Camera", Kind = SettingControl.Choice,
                Options = devices,
                Get = () => Math.Max(0, Array.IndexOf(devices, feed?.Device ?? s.Camera)),
                Set = v => { int i = Convert.ToInt32(v); if (i >= 0 && i < devices.Length && devices[i] != "(no camera found)") { s.Camera = devices[i]; Changed(restart: true); } },
            };
            yield return new SettingRow
            {
                Owner = Page, Page = Page, Group = "Camera", Label = "Resolution", Kind = SettingControl.Choice,
                Options = ResolutionNames,
                Get = () => Math.Max(0, Array.FindIndex(Resolutions, r => r[0] == s.Width && r[1] == s.Height && r[2] == s.Fps)),
                Set = v =>
                {
                    int i = Convert.ToInt32(v);
                    if (i < 0 || i >= Resolutions.Length) return;
                    s.Width = Resolutions[i][0]; s.Height = Resolutions[i][1]; s.Fps = Resolutions[i][2];
                    Changed(restart: true);
                },
            };
            yield return new SettingRow
            {
                Owner = Page, Page = Page, Group = "Camera", Label = "Status", Kind = SettingControl.Text,
                Get = () => string.IsNullOrEmpty(feed?.Status) ? "OK" : feed.Status,
                Set = _ => PrismBridge.RefreshSettings(),   // read-only: an edit just redraws the real value
            };

            yield return Bool("Image", "Flip horizontal", () => s.FlipH, v => { s.FlipH = v; Changed(); });
            yield return Bool("Image", "Flip vertical", () => s.FlipV, v => { s.FlipV = v; Changed(); });

            yield return Bool("Show in", "Menus", () => s.ShowInMenus, v => { s.ShowInMenus = v; Changed(); });
            yield return Bool("Show in", "Level select", () => s.ShowInLevelSelect, v => { s.ShowInLevelSelect = v; Changed(); });
            yield return Bool("Show in", "Editor", () => s.ShowInEditor, v => { s.ShowInEditor = v; Changed(); });

            yield return new SettingRow
            {
                Owner = Page, Page = Page, Group = "Layout", Label = "Position, size, rotation and crop",
                Kind = SettingControl.Action, ActionLabel = "Open layout editor",
                Set = _ => PrismBridge.OpenLayoutEditor(),
            };

            if (Extra != null) foreach (var r in Extra()) yield return r;
        }

        internal static SettingRow Bool(string group, string label, Func<bool> get, Action<bool> set) => new SettingRow
        {
            Owner = Page, Page = Page, Group = group, Label = label, Kind = SettingControl.Bool,
            Get = () => get(), Set = v => set(Convert.ToBoolean(v)),
        };

        private static void Changed(bool restart = false)
        {
            Main.SaveSoon();
            if (restart) Main.Feed?.Restart(Main.Settings);
        }
    }
}
