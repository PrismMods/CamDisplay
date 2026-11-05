using UnityEngine;
using UnityEngine.UI;

namespace CamDisplay
{
    /* The cam on screen: one RawImage on its own overlay canvas. All GPU: crop and flips are the
       uvRect, rotation is the transform; no pixel is ever read or written on the CPU. Layout is
       recomputed only when an input changes (settings, screen or texture size), so a steady frame
       allocates nothing. */
    internal sealed class CamView
    {
        private readonly GameObject _go;
        private readonly RawImage _img;
        private readonly RectTransform _rt;
        private bool _shown = true;
        private int _ver = -1, _sw, _sh, _tw, _th;
        private float[] _box = new float[4];

        public CamView()
        {
            var root = new GameObject("CamDisplayCanvas");
            Object.DontDestroyOnLoad(root);
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 90;   // over the game's HUD, under Topaz's key viewer (100) and every menu
            _go = new GameObject("CamDisplayFeed");
            _go.transform.SetParent(root.transform, false);
            _img = _go.AddComponent<RawImage>();
            _img.raycastTarget = false;
            _rt = _go.GetComponent<RectTransform>();
            _rt.anchorMin = _rt.anchorMax = Vector2.zero;   // bottom-left: anchoredPosition is screen px
            _rt.pivot = new Vector2(0.5f, 0.5f);
            Show(false);
        }

        public void Apply(CamSettings s, Texture tex, bool visible, int settingsVersion)
        {
            bool on = visible && tex != null;
            Show(on);
            if (!on) return;
            if (_img.texture != tex) { _img.texture = tex; _ver = -1; }
            if (settingsVersion == _ver && Screen.width == _sw && Screen.height == _sh && tex.width == _tw && tex.height == _th) return;
            _ver = settingsVersion; _sw = Screen.width; _sh = Screen.height; _tw = tex.width; _th = tex.height;
            _box = CamGeometry.Box(s, _sw, _sh, _tw, _th);
            _rt.anchoredPosition = new Vector2(_box[0], _box[1]);
            _rt.sizeDelta = new Vector2(_box[2], _box[3]);
            _rt.localEulerAngles = new Vector3(0f, 0f, s.Rotation);
            var uv = CamGeometry.Uv(s);
            _img.uvRect = new Rect(uv[0], uv[1], uv[2], uv[3]);
        }

        /// The cam's current screen box for the layout editor, even while it is hidden.
        public float[] Box(CamSettings s, Texture tex) =>
            CamGeometry.Box(s, Screen.width, Screen.height, tex != null ? tex.width : 0, tex != null ? tex.height : 0);

        private void Show(bool on)
        {
            if (on == _shown) return;
            _shown = on;
            _go.SetActive(on);
        }
    }
}
