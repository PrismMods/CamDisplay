using System;
using System.Reflection;
using UnityEngine.SceneManagement;

namespace CamDisplay
{
    /* Where the player is. Game state is read by name through reflection, not a compile-time
       reference to Assembly-CSharp, so one build runs on ADOFAI 2.9 and 3.x; a member that is not
       there reads as false instead of failing to load. */
    internal static class SceneWatch
    {
        private static string _scene = "";
        private static bool _hooked, _looked;
        private static Func<object> _controller, _editor;
        private static Func<object, bool> _gameworld, _paused, _playMode;

        public static string Scene { get { Hook(); return _scene; } }

        private static int _at = -100;
        private static CamContextKind _last = CamContextKind.Menu;

        /// Re-read every 6 frames: reflection boxes, and a rhythm game should not make garbage per frame.
        public static CamContextKind Current()
        {
            int f = UnityEngine.Time.frameCount;
            if (f - _at < 6 && f >= _at) return _last;
            _at = f;
            return _last = Read();
        }

        /// The raw inputs, for logging and the DEBUG self-test.
        internal static string Describe()
        {
            Hook();
            object c = null;
            try { c = _controller?.Invoke(); } catch { }
            bool w = false, p = false;
            if (c != null) { try { w = _gameworld != null && _gameworld(c); } catch { } try { p = _paused != null && _paused(c); } catch { } }
            return "scene=" + _scene + " controller=" + (c != null) + " gameworld=" + w + " paused=" + p + " editorPlaying=" + EditorPlaying();
        }

        private static CamContextKind Read()
        {
            Hook();
            object c = null;
            try { c = _controller?.Invoke(); } catch { }
            bool world = false, paused = false;
            if (c != null)
            {
                try { world = _gameworld != null && _gameworld(c); } catch { }
                try { paused = _paused != null && _paused(c); } catch { }
            }
            return CamContext.Classify(_scene, world, paused, _scene == "scnEditor" && EditorPlaying());
        }

        // scnEditor.instance.playMode: true while a level plays in the editor scene (a custom level
        // from the browser, or a playtest), false while editing.
        private static bool EditorPlaying()
        {
            object e = null;
            try { e = _editor?.Invoke(); } catch { }
            if (e == null || _playMode == null) return false;
            try { return _playMode(e); } catch { return false; }
        }

        private static void Hook()
        {
            if (!_hooked)
            {
                _hooked = true;
                _scene = SceneManager.GetActiveScene().name ?? "";
                SceneManager.activeSceneChanged += (a, b) => { _scene = b.name ?? ""; _at = -100; };   // a new scene is never answered from the old one's cache
            }
            if (_looked) return;
            _looked = true;
            var t = Type.GetType("scrController, Assembly-CSharp");
            if (t == null) { Log.Info("scrController not found; every scene counts as a menu"); return; }
            _controller = Static(t, "instance");
            _gameworld = Bool(t, "gameworld");
            _paused = Bool(t, "paused");
            var ed = Type.GetType("scnEditor, Assembly-CSharp");
            if (ed != null) { _editor = Static(ed, "instance"); _playMode = Bool(ed, "playMode"); }
            Log.Info("scene watch: instance " + (_controller != null) + ", gameworld " + (_gameworld != null) + ", paused " + (_paused != null)
                     + ", editor.playMode " + (_editor != null && _playMode != null));
        }

        private const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic;

        private static Func<object> Static(Type t, string name)
        {
            var f = t.GetField(name, All | BindingFlags.Static | BindingFlags.FlattenHierarchy);
            if (f != null) return () => f.GetValue(null);
            var p = t.GetProperty(name, All | BindingFlags.Static | BindingFlags.FlattenHierarchy);
            if (p != null) return () => p.GetValue(null, null);
            return null;
        }

        private static Func<object, bool> Bool(Type t, string name)
        {
            var f = t.GetField(name, All | BindingFlags.Instance);
            if (f != null && f.FieldType == typeof(bool)) return o => (bool)f.GetValue(o);
            var p = t.GetProperty(name, All | BindingFlags.Instance);
            if (p != null && p.PropertyType == typeof(bool)) return o => (bool)p.GetValue(o, null);
            return null;
        }
    }
}
