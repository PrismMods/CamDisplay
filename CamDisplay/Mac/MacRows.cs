using System.Collections.Generic;
using PrismLib.UI.Toolkit;
using UnityEngine;

namespace CamDisplay
{
    // The "macOS camera" group of the settings window, and the camera-denied wording on macOS.
    internal static class MacRows
    {
        private const string Group = "macOS camera";
        private static string _pending;

        internal static void Install()
        {
            if (!MacPatch.IsMac) return;
            SettingsRows.Extra = Rows;
            CamFeed.DeniedText = () => MacPatch.NeedsPatch()
                ? "Camera blocked by macOS: CamDisplay settings › macOS camera › Enable camera"
                : "Camera denied: check the Steam launch options (CamDisplay settings › macOS camera) and System Settings › Privacy & Security › Camera";
        }

        private static IEnumerable<SettingRow> Rows()
        {
            yield return new SettingRow
            {
                Owner = SettingsRows.Page, Page = SettingsRows.Page, Group = Group, Label = "Camera patch",
                Kind = SettingControl.Text, Get = () => _pending ?? StatusText(), Set = _ => PrismBridge.RefreshSettings(),
            };
            yield return Action("Enable camera on macOS", "Enable camera", () =>
            {
                string e = Launcher.Install();
                if (e == "") e = MacPatch.ScheduleAfterExit("apply");
                _pending = e == ""
                    ? "Set the launch options below, then quit the game: the patch is applied as it closes. Start it again from Steam."
                    : "Could not prepare the patch: " + e;
            });
            yield return new SettingRow
            {
                Owner = SettingsRows.Page, Page = SettingsRows.Page, Group = Group, Label = "Steam launch options",
                Kind = SettingControl.Text, Get = () => Launcher.Line(), Set = _ => PrismBridge.RefreshSettings(),
            };
            yield return Action("Copy launch options", "Copy", () =>
            {
                GUIUtility.systemCopyBuffer = Launcher.Line();
                _pending = "Launch options copied. In Steam: ADOFAI › Properties › Launch Options, paste, close.";
            });
            yield return Action("Undo camera patch", "Undo", () =>
            {
                string e = MacPatch.ScheduleAfterExit("undo");
                _pending = e == "" ? "The patch is removed as the game closes." : "Could not queue the undo: " + e;
            });
        }

        private static string StatusText()
        {
            switch (MacPatch.Status())
            {
                case "patched": return "Applied. The launch options must start with CamDisplayLaunch.";
                case "stock": return "Not applied: macOS blocks the camera until you enable it (and after every game update).";
                default: return "Unknown (could not read the game's signature)";
            }
        }

        private static SettingRow Action(string label, string button, System.Action run) => new SettingRow
        {
            Owner = SettingsRows.Page, Page = SettingsRows.Page, Group = Group, Label = label,
            Kind = SettingControl.Action, ActionLabel = button,
            Set = _ => { run(); PrismBridge.RefreshSettings(); },
        };
    }
}
