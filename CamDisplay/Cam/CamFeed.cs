using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace CamDisplay
{
    /* The webcam. Play() stalls the main thread for 0.7-1.3 s (measured on macOS), so the camera
       runs whenever CamDisplay is enabled, hidden or not, and only restarts when the camera,
       resolution or Enabled changes. Unity itself stops a WebCamTexture on every scene change
       (seen on macOS: AVCaptureSession stopRunning from UnityPlayer), so one Play() follows each
       scene change; any other stop is a lost device and is reported once, never retried in a loop. */
    internal sealed class CamFeed
    {
        private WebCamTexture _tex;
        private AsyncOperation _auth;
        private CamSettings _s;
        private bool _want, _wasPlaying, _reportedStop, _problem;
        private float _openedAt;
        private string _okStatus = "";             // what the status says when frames are flowing
        private const float NoFramesAfter = 5f;
        private int _sceneChangeFrame = -1;
        private const int SceneStopWindow = 300;   // frames after a scene change in which a stop is Unity's

        /// Set by the macOS patch code: the status to show when access is denied.
        internal static Func<string> DeniedText = () => "Camera access denied";

        public string Status { get; private set; } = "";
        /// A WebCamTexture is open (playing or starting). False means the camera is released.
        internal bool HasCamera => _tex != null;
        /// Scene-change restarts so far, and how many frames after the scene change the last one ran.
        internal int Restarts { get; private set; }
        internal int LastRestartLag { get; private set; } = -1;
        public string Device { get; private set; }
        public event Action StatusChanged;

        /// The live texture, or null until frames arrive (Unity reports 16x16 before that).
        public Texture Texture => _tex != null && _tex.isPlaying && _tex.width > 16 ? _tex : null;

        public CamFeed()
        {
            SceneManager.activeSceneChanged += (a, b) => _sceneChangeFrame = Time.frameCount;
        }

        public string[] Devices()
        {
            try
            {
                var d = WebCamTexture.devices;
                var names = new string[d.Length];
                for (int i = 0; i < d.Length; i++) names[i] = d[i].name;
                return names;
            }
            catch { return new string[0]; }
        }

        public void Restart(CamSettings s)
        {
            _s = s;
            Stop();
            _want = s != null && s.Enabled;
            if (!_want) { SetStatus("Off"); return; }
            if (!Application.HasUserAuthorization(UserAuthorization.WebCam))
            {
                _auth = Application.RequestUserAuthorization(UserAuthorization.WebCam);
                SetStatus("Waiting for camera permission");
                return;
            }
            Open();
        }

        public void Stop()
        {
            _auth = null;
            if (_tex != null)
            {
                try { _tex.Stop(); } catch { }
                Object.Destroy(_tex);
                _tex = null;
            }
            _wasPlaying = false;
            _reportedStop = false;
            _problem = false;
        }

        public void Tick()
        {
            if (!_want) return;
            if (_auth != null)
            {
                if (!_auth.isDone) return;
                _auth = null;
                if (!Application.HasUserAuthorization(UserAuthorization.WebCam)) { SetStatus(DeniedText()); return; }
                Open();
                return;
            }
            if (_tex == null) return;
            if (_tex.isPlaying && _tex.width > 16)
            {
                _wasPlaying = true;
                _reportedStop = false;
                if (_problem) { _problem = false; SetStatus(_okStatus); }   // frames again: clear the warning
                return;
            }
            // Started (or restarted) but no frames yet: a camera the OS blocks or another app holds
            // can stay like this forever, so say so after a few seconds instead of reading "OK".
            if (!_wasPlaying || _tex.isPlaying)
            {
                if (!_problem && Time.realtimeSinceStartup - _openedAt > NoFramesAfter)
                {
                    _problem = true;
                    SetStatus("Camera not delivering frames (in use by another app, or blocked by the OS?)");
                }
                return;
            }
            if (_sceneChangeFrame >= 0 && Time.frameCount - _sceneChangeFrame <= SceneStopWindow)
            {
                LastRestartLag = Time.frameCount - _sceneChangeFrame;
                _sceneChangeFrame = -1;
                _wasPlaying = false;
                Restarts++;
                float t0 = Time.realtimeSinceStartup;
                _tex.Play();
                _openedAt = Time.realtimeSinceStartup;
                Log.Info("camera stopped by a scene change; restarted " + LastRestartLag + " frame(s) after it (Play took "
                         + ((_openedAt - t0) * 1000f).ToString("0") + " ms)");
                return;
            }
            if (!_reportedStop)
            {
                _reportedStop = true;
                _wasPlaying = false;
                _problem = true;
                SetStatus("Camera stopped (" + Device + " disconnected?)");
            }
        }

#if DEBUG
        /// Self-test only: open a named device as Open would, bypassing PickDevice.
        internal void OpenForTest(string name)
        {
            Stop();
            _want = true;
            _tex = new WebCamTexture(name, _s.Width, _s.Height, _s.Fps);
            Device = name;
            _tex.Play();
            _openedAt = Time.realtimeSinceStartup;
            _okStatus = "";
            SetStatus("");
        }
#endif

        private void Open()
        {
            bool fellBack;
            string name = CamContext.PickDevice(_s.Camera, Devices(), out fellBack);
            if (name == null) { SetStatus(Application.HasUserAuthorization(UserAuthorization.WebCam) ? "No camera found" : DeniedText()); return; }
            Device = name;
            try
            {
                _tex = new WebCamTexture(name, _s.Width, _s.Height, _s.Fps);
                float t0 = Time.realtimeSinceStartup;
                _tex.Play();
                _openedAt = Time.realtimeSinceStartup;
                Log.Info("camera '" + name + "' started (" + _s.Width + "x" + _s.Height + "@" + _s.Fps
                         + "), Play took " + ((Time.realtimeSinceStartup - t0) * 1000f).ToString("0") + " ms");
                _okStatus = fellBack ? "Using " + name + " (saved camera not found)" : "";
                _problem = false;
                SetStatus(_okStatus);
            }
            catch (Exception e) { Log.Error("camera start failed", e); SetStatus("Camera failed to start"); _tex = null; }
        }

        private void SetStatus(string s)
        {
            if (s == Status) return;
            Status = s;
            if (s.Length > 0) Log.Info("status: " + s);
            try { StatusChanged?.Invoke(); } catch { }
        }
    }
}
