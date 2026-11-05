using System;

namespace CamDisplay
{
    // The macOS camera patch's decisions, kept apart from the processes that carry them out.
    internal static class MacPatchPlan
    {
        internal const string UsageText = "CamDisplay shows your hand cam in-game.";
        internal const string CameraEntitlement = "com.apple.security.device.camera";
        internal const string Launcher = "CamDisplayLaunch";

        /// The game needs the camera entitlement AND the usage string, or macOS denies the camera.
        internal static bool NeedsPatch(string entitlementsXml, string infoPlistXml) =>
            entitlementsXml == null || !entitlementsXml.Contains(CameraEntitlement)
            || infoPlistXml == null || !infoPlistXml.Contains("NSCameraUsageDescription");

        /// The Steam launch options: the launcher in front of the helper the player already uses.
        internal static string LaunchLine(string gameDir, string helperScript)
        {
            string dir = gameDir.TrimEnd('/');
            string line = "\"" + dir + "/" + Launcher + "\" ";
            if (!string.IsNullOrEmpty(helperScript)) line += "\"" + dir + "/" + helperScript + "\" ";
            return line + "%command%";
        }

        /// UMMInstall's setup_helper.sh first (it is what the player's options call), then MelonLoader's own.
        internal static string PickHelper(Func<string, bool> existsInGameDir)
        {
            if (existsInGameDir("setup_helper.sh")) return "setup_helper.sh";
            if (existsInGameDir("melonloader-launch.sh")) return "melonloader-launch.sh";
            return null;
        }
    }
}
