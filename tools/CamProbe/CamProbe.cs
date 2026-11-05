// Dev tool: can ADOFAI open a webcam in-process? Logs to probe.log and draws the feed top-left.
using System;
using System.IO;
using UnityEngine;
using UnityModManagerNet;

namespace CamProbe {
    public static class Main {
        public static string LogPath;
        public static void Log(string s) {
            try { File.AppendAllText(LogPath, DateTime.Now.ToString("HH:mm:ss.fff") + " " + s + "\n"); } catch { }
        }
        public static bool Load(UnityModManager.ModEntry e) {
            LogPath = Path.Combine(e.Path, "probe.log");
            Log("load: platform=" + Application.platform + " unity=" + Application.unityVersion);
            var go = new GameObject("CamProbe");
            UnityEngine.Object.DontDestroyOnLoad(go);
            go.AddComponent<Runner>();
            return true;
        }
    }

    public class Runner : MonoBehaviour {
        WebCamTexture tex;
        AsyncOperation auth;
        int frame, updates, reports;
        bool started;

        bool wasPlaying;
        int restarts;

        // Instrumentation: what happens around a webcam that stops on its own.
        void OnApplicationFocus(bool f) { Main.Log("focus=" + f + " frame=" + frame); }
        void OnApplicationPause(bool p) { Main.Log("pause=" + p + " frame=" + frame); }

        void Start() {
            UnityEngine.SceneManagement.SceneManager.activeSceneChanged +=
                (a, b) => Main.Log("scene " + a.name + " -> " + b.name + " frame=" + frame);
            try {
                Main.Log("hasAuth(before)=" + Application.HasUserAuthorization(UserAuthorization.WebCam));
                var devs = WebCamTexture.devices;
                Main.Log("devices(before auth)=" + devs.Length);
                foreach (var d in devs) Main.Log("  device: '" + d.name + "' front=" + d.isFrontFacing + " kind=" + d.kind);
                Main.Log("requesting authorization...");
                auth = Application.RequestUserAuthorization(UserAuthorization.WebCam);
            } catch (Exception ex) { Main.Log("start EX: " + ex); }
        }

        void Update() {
            frame++;
            try {
                if (!started && auth != null && auth.isDone) {
                    started = true;
                    Main.Log("auth done; hasAuth=" + Application.HasUserAuthorization(UserAuthorization.WebCam));
                    var devs = WebCamTexture.devices;
                    Main.Log("devices(after auth)=" + devs.Length);
                    if (devs.Length == 0) { Main.Log("no devices, stop"); return; }
                    tex = new WebCamTexture(devs[0].name, 1280, 720, 30);
                    Main.Log("calling Play on '" + devs[0].name + "'");
                    tex.Play();
                    Main.Log("Play returned; isPlaying=" + tex.isPlaying);
                }
                if (tex != null) {
                    if (tex.isPlaying) wasPlaying = true;
                    else if (wasPlaying) {
                        wasPlaying = false;
                        Main.Log("STOPPED frame=" + frame + " updates=" + updates + " scene=" + UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
                        if (restarts < 3) { restarts++; tex.Play(); Main.Log("restart " + restarts + " -> isPlaying=" + tex.isPlaying); }
                    }
                    if (tex.didUpdateThisFrame) updates++;
                    if (frame % 120 == 0 && reports < 12) {
                        reports++;
                        var c = tex.width > 16 ? tex.GetPixel(tex.width / 2, tex.height / 2) : Color.clear;
                        Main.Log("t=" + Time.realtimeSinceStartup.ToString("0.0") + " isPlaying=" + tex.isPlaying + " size=" + tex.width + "x" + tex.height + " updates=" + updates + " center=" + c);
                    }
                }
            } catch (Exception ex) { Main.Log("update EX: " + ex); tex = null; }
        }

        void OnGUI() {
            if (tex != null && tex.width > 16) GUI.DrawTexture(new Rect(20, 20, 480, 270), tex, ScaleMode.ScaleToFit);
        }
    }
}
