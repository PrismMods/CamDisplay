using System;
using PrismLib.UI;
using TMPro;
using UnityEngine;

namespace CamDisplay
{
    /* The camera patch re-signs the game, and macOS ties Input Monitoring to the signature, so the
       first patch (and every one that fell back to ad hoc) loses it: the keyboard hook throws
       SkyHookException: NOT_PERMITTED and the game falls back to Unity's keyboard, which never
       counts Tab or F1-F12 as hits. The game says nothing about it, so this does. The exception is
       the signal, caught off Unity's log, so it has to be listening before the game's first
       scrController.Start, where the hook starts. */
    internal static class InputCheck
    {
        internal static bool Blocked { get; private set; }

        internal const string Fix = "System Settings › Privacy & Security › Input Monitoring: remove the game, add it again, restart";

        private static Toast _toast;
        private static ToastContent _content;

        internal static void Install()
        {
            if (MacPatch.IsMac) Application.logMessageReceived += OnLog;
        }

        private static void OnLog(string condition, string stack, LogType type)
        {
            if (condition == null || !condition.Contains("SkyHookException") || !condition.Contains("NOT_PERMITTED")) return;
            Application.logMessageReceived -= OnLog;
            Blocked = true;
            Log.Info("macOS denied the game's keyboard hook (Input Monitoring): async input is off, Tab and F1-F12 will not hit. " + Fix);
            _content = new ToastContent
            {
                Key = "input-monitoring",
                Title = "macOS blocked async input: Tab and F-keys won't hit",
                Hint = "Re-add the game under Privacy & Security › Input Monitoring",
                Width = 500f,
                OnClick = PrismBridge.ToggleSettings,
            };
            PrismBridge.RefreshSettings();
        }

        internal static void Tick()
        {
            if (_content == null) return;
            if (_toast == null)
            {
                _toast = new Toast("CamDisplay");
                // TMP's default is the game's display face, unreadable at toast size.
                try { _toast.Theme.Font = TMP_FontAsset.CreateFontAsset(new Font("/System/Library/Fonts/Supplemental/Arial.ttf")); }
                catch (Exception e) { Log.Error("toast font", e); }
            }
            _toast.Set(_content);
            _toast.Tick();
        }

        internal static void Dispose()
        {
            Application.logMessageReceived -= OnLog;
            _toast?.Dispose();
            _toast = null;
            _content = null;
        }
    }
}
