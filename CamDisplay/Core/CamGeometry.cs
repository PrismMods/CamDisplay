namespace CamDisplay
{
    /* Settings ↔ screen. Screen pixels with a bottom-left origin (the layout editor's convention);
       settings hold 1080p reference pixels, so screen px = reference px × screenH / 1080.
       BCL only (offline-tested). */
    internal static class CamGeometry
    {
        private const float RefH = 1080f;

        // Same rule as PrismLib.UI's LayoutMath.AnchorPoint (internal there, so not callable here).
        internal static float[] AnchorPoint(int anchor, float screenW, float screenH)
        {
            int a = anchor < 0 ? 0 : anchor > 8 ? 8 : anchor;
            return new[] { (a % 3) * screenW / 2f, screenH - (a / 3) * screenH / 2f };
        }

        /// {centreX, centreY, width, height} of the visible (cropped) cam, before rotation.
        internal static float[] Box(CamSettings s, float screenW, float screenH, float srcW, float srcH)
        {
            float k = screenH / RefH;
            float visW = 1f - s.CropL - s.CropR, visH = 1f - s.CropB - s.CropT;
            // Unity reports 16x16 until the first frame arrives; assume 16:9 until then.
            float srcAspect = srcW > 16f && srcH > 16f ? srcW / srcH : 16f / 9f;
            // BaseHeight x Scale is the WHOLE source's height: cropping trims the box, never zooms
            // the picture (the layout editor's crop ghost relies on that).
            float full = s.BaseHeight * s.Scale * k;
            float h = full * visH, w = full * srcAspect * visW;
            var a = AnchorPoint(s.Anchor, screenW, screenH);
            return new[] { a[0] + s.OffsetX * k, a[1] + s.OffsetY * k, w, h };
        }

        internal static void MoveTo(CamSettings s, float cx, float cy, float screenW, float screenH)
        {
            float k = screenH / RefH;
            var a = AnchorPoint(s.Anchor, screenW, screenH);
            s.OffsetX = (cx - a[0]) / k;
            s.OffsetY = (cy - a[1]) / k;
        }

        /// Switch anchors without moving the cam on screen.
        internal static void Reanchor(CamSettings s, int anchor, float screenW, float screenH)
        {
            float k = screenH / RefH;
            var a = AnchorPoint(s.Anchor, screenW, screenH);
            float cx = a[0] + s.OffsetX * k, cy = a[1] + s.OffsetY * k;
            s.Anchor = anchor;
            MoveTo(s, cx, cy, screenW, screenH);
        }

        /// RawImage.uvRect {x, y, w, h}. Crop is in DISPLAYED space (what the editor's handles trim):
        /// under a flip the displayed left is the source's right, so the rect starts from the far side.
        internal static float[] Uv(CamSettings s)
        {
            float w = 1f - s.CropL - s.CropR, h = 1f - s.CropB - s.CropT;
            float x = s.FlipH ? 1f - s.CropL : s.CropL;
            float y = s.FlipV ? 1f - s.CropB : s.CropB;
            return new[] { x, y, s.FlipH ? -w : w, s.FlipV ? -h : h };
        }
    }
}
