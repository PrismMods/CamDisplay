using System;
using CamDisplay;

// Offline check for CamDisplay's pure core (settings, context rules, geometry, macOS patch
// decisions). The game cannot be scripted from here, so this is where that logic fails loudly.
//   bash tools/test.sh
static class CamDisplayTest
{
    static int _fail;

    static void Check(bool ok, string what)
    {
        Console.WriteLine((ok ? "  ok   " : "  FAIL ") + what);
        if (!ok) _fail++;
    }

    static bool Near(float a, float b) => Math.Abs(a - b) < 0.01f;

    static int Main()
    {
        Console.WriteLine("settings");
        var d = new CamSettings();
        Check(d.Enabled && d.FlipH && d.FlipV && !d.ShowInMenus && !d.ShowInLevelSelect && !d.ShowInEditor, "defaults: on, both flips, contexts off");
        Check(d.Width == 1280 && d.Height == 720 && d.Fps == 30 && d.Anchor == 8 && d.AnchorClosest, "defaults: 720p30, bottom-right, closest");
        bool bad;
        var s = CamSettings.FromJson("{not json", j => { throw new Exception(); }, out bad);
        Check(bad && s.Enabled && s.FlipH, "corrupt file loads defaults and reports bad");
        s = CamSettings.FromJson(null, j => null, out bad);
        Check(!bad && s.Enabled, "missing file loads defaults quietly");
        s = CamSettings.FromJson("{}", j => null, out bad);
        Check(bad && s.Enabled, "a parser that returns nothing counts as bad");
        s = CamSettings.FromJson("{...}", j => new CamSettings { Scale = 0f, Anchor = 12, CropL = 0.9f, CropR = 0.9f, BaseHeight = float.NaN, Width = 0 }, out bad);
        Check(!bad && s.Scale == 0.1f && s.Anchor == 8 && s.CropL + s.CropR <= 0.9501f && s.BaseHeight == 270f && s.Width == 1280,
              "out-of-range values from a file are clamped or reset so the cam stays visible");

        Console.WriteLine("context");
        // The game plays custom levels inside scnEditor (seen in-game 2026-11-06): play mode is playing.
        Check(CamContext.Classify("scnEditor", true, true, false) == CamContextKind.Editor, "editing in the editor is the editor");
        Check(CamContext.Classify("scnEditor", true, false, true) == CamContextKind.Gameplay, "playing a custom level (editor scene, play mode) is gameplay");
        Check(CamContext.Classify("scnEditor", true, true, true) == CamContextKind.Menu, "pausing a custom level is a menu");
        Check(CamContext.Classify("1-X", true, false, false) == CamContextKind.Gameplay, "playing an official level is gameplay");
        Check(CamContext.Classify("scnGame", true, true, false) == CamContextKind.Menu, "pause is a menu");
        Check(CamContext.Classify("scnCLS", false, false, false) == CamContextKind.LevelSelect, "custom level select");
        Check(CamContext.Classify("scnSplash", false, false, false) == CamContextKind.Menu, "anything else is a menu");
        Check(CamContext.ShouldShow(CamContextKind.Gameplay, d) && !CamContext.ShouldShow(CamContextKind.Editor, d), "gameplay always, editor opt-in");
        d.Enabled = false; Check(!CamContext.ShouldShow(CamContextKind.Gameplay, d), "disabled hides everywhere"); d.Enabled = true;
        bool fb;
        Check(CamContext.PickDevice("Logi C920", new[] { "FaceTime HD Camera", "Logi C920" }, out fb) == "Logi C920" && !fb, "keeps the saved camera");
        Check(CamContext.PickDevice("Gone", new[] { "FaceTime HD Camera" }, out fb) == "FaceTime HD Camera" && fb, "falls back to the first device");
        Check(CamContext.PickDevice("", new[] { "FaceTime HD Camera" }, out fb) == "FaceTime HD Camera" && !fb, "no saved camera picks the first without calling it a fallback");
        Check(CamContext.PickDevice("x", new string[0], out fb) == null, "no devices gives null");

        Console.WriteLine("geometry");
        var b = CamGeometry.Box(d, 1920, 1080, 1280, 720);
        Check(Near(b[3], 270) && Near(b[2], 480) && Near(b[0], 1660) && Near(b[1], 160), "default box: 480x270, 20px from the right, 25px up");
        b = CamGeometry.Box(d, 3840, 2160, 1280, 720); Check(Near(b[3], 540) && Near(b[0], 3320), "scales with screen height");
        b = CamGeometry.Box(d, 1920, 1080, 0, 0); Check(Near(b[2], 480), "unknown source size falls back to 16:9");
        b = CamGeometry.Box(d, 1920, 1080, 16, 16); Check(Near(b[2], 480), "Unity's 16x16 placeholder also falls back to 16:9");
        d.CropL = 0.25f; d.CropR = 0.25f; b = CamGeometry.Box(d, 1920, 1080, 1280, 720); Check(Near(b[2], 240) && Near(b[3], 270), "crop narrows the box, height kept");
        var uv = CamGeometry.Uv(d); Check(Near(uv[0], 0.75f) && Near(uv[2], -0.5f) && Near(uv[1], 1f) && Near(uv[3], -1f), "uv: crop then both flips");
        d.FlipH = d.FlipV = false; uv = CamGeometry.Uv(d); Check(Near(uv[0], 0.25f) && Near(uv[2], 0.5f) && Near(uv[1], 0f) && Near(uv[3], 1f), "uv without flips is the crop rect");
        d.FlipH = d.FlipV = true; d.CropL = 0.2f; d.CropR = 0f; d.CropB = 0.1f;
        uv = CamGeometry.Uv(d);
        Check(Near(uv[0], 0.8f) && Near(uv[2], -0.8f), "crop is in displayed space: under FlipH, cropping the left trims the displayed left");
        Check(Near(uv[1], 0.9f) && Near(uv[3], -0.9f), "and under FlipV, cropping the bottom trims the displayed bottom");
        d.CropL = d.CropB = 0f;
        d.CropT = 0.2f; b = CamGeometry.Box(d, 1920, 1080, 1280, 720);
        Check(Near(b[3], 216f) && Near(b[2], 480f), "a vertical crop trims the height and keeps the width (no zoom)");
        d.CropT = 0f;
        d.FlipH = d.FlipV = true; d.CropL = d.CropR = 0;
        CamGeometry.MoveTo(d, 960, 540, 1920, 1080); b = CamGeometry.Box(d, 1920, 1080, 1280, 720); Check(Near(b[0], 960) && Near(b[1], 540), "MoveTo places the centre");
        CamGeometry.MoveTo(d, 1920, 1080, 3840, 2160); b = CamGeometry.Box(d, 3840, 2160, 1280, 720); Check(Near(b[0], 1920) && Near(b[1], 1080), "MoveTo works in screen px at any resolution");
        CamGeometry.Reanchor(d, 0, 1920, 1080); b = CamGeometry.Box(d, 1920, 1080, 1280, 720); Check(d.Anchor == 0 && Near(b[0], 960) && Near(b[1], 540), "Reanchor keeps the centre");
        var ap = CamGeometry.AnchorPoint(2, 1920, 1080); Check(Near(ap[0], 1920) && Near(ap[1], 1080), "anchor 2 is the top-right corner");

        Console.WriteLine("macOS patch");
        Check(MacPatchPlan.NeedsPatch("<key>com.apple.security.cs.disable-library-validation</key>", "<key>NSCameraUsageDescription</key>"), "no camera entitlement needs the patch");
        Check(!MacPatchPlan.NeedsPatch("<key>com.apple.security.device.camera</key><true/>", "<key>NSCameraUsageDescription</key>"), "entitlement + usage key is patched");
        Check(MacPatchPlan.NeedsPatch("<key>com.apple.security.device.camera</key><true/>", "<dict></dict>"), "missing usage key needs the patch");
        Check(MacPatchPlan.LaunchLine("/G a", "setup_helper.sh") == "\"/G a/CamDisplayLaunch\" \"/G a/setup_helper.sh\" %command%", "launch line wraps UMMInstall's helper");
        Check(MacPatchPlan.LaunchLine("/G a", "melonloader-launch.sh") == "\"/G a/CamDisplayLaunch\" \"/G a/melonloader-launch.sh\" %command%", "launch line wraps MelonLoader's script");
        Check(MacPatchPlan.LaunchLine("/G a", null) == "\"/G a/CamDisplayLaunch\" %command%", "launch line without a helper");
        Check(MacPatchPlan.PickHelper(f => true) == "setup_helper.sh", "setup_helper.sh wins when both exist");
        Check(MacPatchPlan.PickHelper(f => f == "melonloader-launch.sh") == "melonloader-launch.sh" && MacPatchPlan.PickHelper(f => false) == null, "falls back to MelonLoader's script, then none");

        Console.WriteLine("camera modes");
        // What this Mac's FaceTime HD camera reported (2026-11-07), two pixel formats of 720p among them.
        var m = CamModes.From(new[]
        {
            new double[] { 1920, 1080, 15, 30 }, new double[] { 1280, 720, 15, 30 }, new double[] { 1280, 720, 15, 30 },
            new double[] { 640, 480, 15, 30 }, new double[] { 1080, 1920, 15, 30 },
        });
        Check(m.Count == 8, "each size offers its top rate and the common rates its range covers, duplicates once (" + m.Count + ")");
        Check(CamModes.Name(m[0]) == "640×480 @ 15" && CamModes.Name(m[1]) == "640×480 @ 30", "smallest picture first, then by frame rate");
        Check(CamModes.IndexOf(m, 1280, 720, 60) < 0 && CamModes.IndexOf(m, 1280, 720, 30) >= 0, "a rate above the camera's range is not offered");
        Check(CamModes.IndexOf(m, 1080, 1920, 30) < CamModes.IndexOf(m, 1920, 1080, 15), "sizes of equal area sort by width");
        m = CamModes.From(new[] { new double[] { 1920, 1080, 60, 60 }, new double[] { 1280, 720, 29.97, 29.97 }, new double[] { 0, 0, 1, 30 } });
        Check(m.Count == 2 && CamModes.Name(m[0]) == "1280×720 @ 30" && CamModes.Name(m[1]) == "1920×1080 @ 60",
              "a fixed-rate format offers only its rate, rounded; an empty format is skipped");
        Check(CamModes.From(new double[0][]).Count == 0, "a camera that reports nothing gives no modes (the fixed list stands in)");

        Console.WriteLine(_fail == 0 ? "ALL PASS" : _fail + " FAILED");
        return _fail == 0 ? 0 : 1;
    }
}
