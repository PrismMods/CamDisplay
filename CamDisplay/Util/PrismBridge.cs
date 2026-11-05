using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using PrismLib.UI.Toolkit;
using UnityEngine;

namespace CamDisplay
{
    /* CamDisplay's side of PrismLib. The rules from PrismLib/CLAUDE.md, both load-bearing on a
       machine where PrismLib.dll never arrived:
         - No PrismLib.dll type in a FIELD (held as object, cast at use): field types resolve when
           this class loads, before Ensure() can install the library.
         - Every method that names a PrismLib.dll type runs only after Available, and no file
           outside this one names one.
       PrismLib.UI types are fine anywhere: that assembly ships beside the mod. */
    internal static class PrismBridge
    {
        internal static bool Available { get; private set; }

        private static object _me, _editorClaim;
        private static LayoutEditor _editor;
        private static bool _editorAll;   // the editor shows every mod's targets (we host it)

        // Raise to the PrismLib release that first shipped the layout editor when CamDisplay ships.
        private static readonly Version Minimum = new Version(0, 3, 12);

        internal static void Init(string version)
        {
            try
            {
                if (!PrismLib.Bootstrap.PrismBootstrap.Ensure(Log.Info, Minimum, Main.ModsRoot, Main.BundlePath)) return;
                Wire(version);
                Available = true;
            }
            catch (Exception e) { Log.Info("PrismLib unavailable, running without the shared editor registry: " + e.Message); }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void Wire(string version)
        {
            Version v;
            Version.TryParse((version ?? "0.0.0").Split('-')[0], out v);
            var me = PrismLib.Prism.Register("CamDisplay", v ?? new Version(0, 0, 0));
            _me = me;
            me.BindKey("Settings", (int)KeyCode.C, PrismLib.KeyMods.Ctrl | PrismLib.KeyMods.Shift, "Open CamDisplay settings");
            me.DescribeLayout(new[] { ToLayoutTarget(CamTarget()) });
            _editorClaim = me.ClaimState(PrismLib.StateKey.LayoutEditor, "shared layout editor");
            if (_editorClaim != null)
            {
                PrismLib.Layout.OpenRequested += () => OpenEditor(all: true);
                PrismLib.Layout.Changed += () => { if (_editor != null && _editor.Visible) _editor.Reload(); };
            }
            me.AddLog("CamDisplay", () => ReadTail(), Log.Path);
        }

        internal static void Tick()
        {
            try { _editor?.Tick(); } catch (Exception e) { Log.Error("layout editor tick", e); }
        }

        /// Open the layout editor: ours if we host it (or PrismLib is missing), else ask the host.
        internal static void OpenLayoutEditor()
        {
            if (Available && _editorClaim == null) { RequestOpenShared(); return; }
            OpenEditor(all: Available);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void RequestOpenShared() => PrismLib.Layout.RequestOpen();

        private static void OpenEditor(bool all)
        {
            if (_editor == null || _editorAll != all)
            {
                _editor?.Dispose();
                _editorAll = all;
                _editor = all ? new LayoutEditor(AllTargets) : new LayoutEditor(OwnTargets);
                _editor.Closed += () => Log.Info("layout editor closed");
            }
            _editor.Visible = true;
        }

        internal static bool EditorOpen => _editor != null && _editor.Visible;

        private static IEnumerable<EditTarget> OwnTargets() { yield return CamTarget(); }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static IEnumerable<EditTarget> AllTargets()
        {
            var list = new List<EditTarget>();
            foreach (var kv in PrismLib.Layout.All()) list.Add(ToEditTarget(kv.Key, kv.Value));
            return list;
        }

        internal static void Shutdown()
        {
            if (!Available) return;
            try { Unwire(); } catch { }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void Unwire()
        {
            PrismLib.Claims.ReleaseAll("CamDisplay");
            PrismLib.Keys.Unregister("CamDisplay");
            PrismLib.Layout.Unregister("CamDisplay");
            PrismLib.Settings.Unregister("CamDisplay");
            PrismLib.Diagnostics.Unregister("CamDisplay");
        }

        // ── The hand cam as a layout target ─────────────────────────────────

        /// Every editor hook for the cam, written once; the registry copy is made from this.
        internal static EditTarget CamTarget()
        {
            Func<CamSettings> S = () => Main.Settings;
            return new EditTarget
            {
                Owner = "CamDisplay", Id = "cam", Label = "Hand cam",
                GetBox = () =>
                {
                    var t = Main.Feed?.Texture;
                    return CamGeometry.Box(S(), Screen.width, Screen.height, t != null ? t.width : 0, t != null ? t.height : 0);
                },
                MoveTo = (x, y) => { CamGeometry.MoveTo(S(), x, y, Screen.width, Screen.height); Main.SaveSoon(); },
                GetScale = () => S().Scale,
                SetScale = v => { S().Scale = Mathf.Clamp(v, 0.1f, 4f); Main.SaveSoon(); },
                MinScale = 0.1f, MaxScale = 4f,
                GetRotation = () => S().Rotation,
                SetRotation = v => { S().Rotation = v; Main.SaveSoon(); },
                GetAnchor = () => S().Anchor,
                SetAnchor = a => { CamGeometry.Reanchor(S(), a, Screen.width, Screen.height); Main.SaveSoon(); },
                GetAnchorClosest = () => S().AnchorClosest,
                SetAnchorClosest = c => { S().AnchorClosest = c; Main.SaveSoon(); },
                GetCrop = () => new[] { S().CropL, S().CropB, S().CropR, S().CropT },
                SetCrop = c =>
                {
                    var s = S();
                    s.CropL = c[0]; s.CropB = c[1]; s.CropR = c[2]; s.CropT = c[3];
                    s.Sanitize();
                    Main.SaveSoon();
                },
            };
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static PrismLib.LayoutTarget ToLayoutTarget(EditTarget e) => new PrismLib.LayoutTarget
        {
            Id = e.Id, Label = e.Label, GetBox = e.GetBox, MoveTo = e.MoveTo,
            GetScale = e.GetScale, SetScale = e.SetScale, MinScale = e.MinScale, MaxScale = e.MaxScale,
            GetRotation = e.GetRotation, SetRotation = e.SetRotation,
            GetAnchor = e.GetAnchor, SetAnchor = e.SetAnchor,
            GetAnchorClosest = e.GetAnchorClosest, SetAnchorClosest = e.SetAnchorClosest,
            GetCrop = e.GetCrop, SetCrop = e.SetCrop,
        };

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static EditTarget ToEditTarget(string owner, PrismLib.LayoutTarget t) => new EditTarget
        {
            Owner = owner, Id = t.Id, Label = t.Label, GetBox = t.GetBox, MoveTo = t.MoveTo,
            GetScale = t.GetScale, SetScale = t.SetScale, MinScale = t.MinScale, MaxScale = t.MaxScale,
            GetRotation = t.GetRotation, SetRotation = t.SetRotation,
            GetAnchor = t.GetAnchor, SetAnchor = t.SetAnchor,
            GetAnchorClosest = t.GetAnchorClosest, SetAnchorClosest = t.SetAnchorClosest,
            GetCrop = t.GetCrop, SetCrop = t.SetCrop,
        };

        private static IEnumerable<string> ReadTail()
        {
            try { return System.IO.File.ReadAllLines(Log.Path); } catch { return new string[0]; }
        }
    }
}
