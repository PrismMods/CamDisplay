using System;

namespace CamDisplay
{
    /* Everything CamDisplay stores, as public fields so JsonUtility can read and write it.
       Positions are in 1080p reference pixels relative to the anchor point, so a layout carries
       across resolutions. BCL only: tools/CamDisplayTest.cs compiles this file directly. */
    [Serializable]
    internal sealed class CamSettings
    {
        public int SchemaVersion = 1;

        public bool Enabled = true;
        public string Camera = "";              // device name; "" = the first one
        public int Width = 1280, Height = 720, Fps = 30;

        public bool FlipH = true, FlipV = true;  // both on: a cam mounted over the keyboard, facing back
        public bool ShowInMenus, ShowInLevelSelect, ShowInEditor;

        public int Anchor = 8;                   // 0-8 from the top-left; 8 = bottom-right
        public bool AnchorClosest = true;
        public float OffsetX = -260f, OffsetY = 160f;   // centre minus anchor point, reference px
        public float BaseHeight = 270f;                  // uncropped cam height at Scale 1, reference px
        public float Scale = 1f;
        public float Rotation;                           // degrees, counter-clockwise
        public float CropL, CropB, CropR, CropT;         // fraction trimmed from each DISPLAYED edge

        /// Defaults for a missing file; defaults plus wasBad for one that would not parse.
        internal static CamSettings FromJson(string json, Func<string, CamSettings> parse, out bool wasBad)
        {
            wasBad = false;
            if (string.IsNullOrEmpty(json)) return new CamSettings();
            CamSettings s = null;
            try { s = parse(json); } catch { }
            if (s == null) { wasBad = true; return new CamSettings(); }
            s.Sanitize();
            return s;
        }

        // A hand-edited or damaged file must not leave the cam invisible or the editor stuck.
        internal void Sanitize()
        {
            var d = new CamSettings();
            if (Width <= 0 || Height <= 0) { Width = d.Width; Height = d.Height; }
            if (Fps <= 0) Fps = d.Fps;
            if (Anchor < 0 || Anchor > 8) Anchor = Anchor < 0 ? 0 : 8;
            if (!Finite(Scale) || Scale < 0.1f) Scale = Finite(Scale) ? 0.1f : d.Scale;
            if (Scale > 4f) Scale = 4f;
            if (!Finite(BaseHeight) || BaseHeight <= 0f) BaseHeight = d.BaseHeight;
            if (!Finite(OffsetX)) OffsetX = d.OffsetX;
            if (!Finite(OffsetY)) OffsetY = d.OffsetY;
            if (!Finite(Rotation)) Rotation = 0f;
            CropL = Clamp(CropL, 0f, 0.95f); CropR = Clamp(CropR, 0f, 0.95f - CropL);
            CropB = Clamp(CropB, 0f, 0.95f); CropT = Clamp(CropT, 0f, 0.95f - CropB);
            if (Camera == null) Camera = "";
        }

        private static bool Finite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);
        private static float Clamp(float v, float lo, float hi) => !Finite(v) || v < lo ? lo : v > hi ? hi : v;
    }
}
