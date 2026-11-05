#if !CAMDISPLAY_UMM
using System.IO;
using MelonLoader;
using MelonLoader.Utils;

[assembly: MelonInfo(typeof(CamDisplay.LoaderMl), "CamDisplay", "0.1.0", "PrismMods", "https://github.com/PrismMods/CamDisplay")]
[assembly: MelonGame("7th Beat Games", "A Dance of Fire and Ice")]

namespace CamDisplay
{
    // MelonLoader entry: Mods/CamDisplay.dll, data in UserData/CamDisplay, PrismLib.UI in UserLibs.
    public sealed class LoaderMl : MelonMod
    {
        public override void OnInitializeMelon()
        {
            string data = Path.Combine(MelonEnvironment.UserDataDirectory, "CamDisplay");
            Log.Sink = m => LoggerInstance.Msg(m);
            Main.Setup(MelonEnvironment.ModsDirectory, data, MelonEnvironment.ModsDirectory,
                       Path.Combine(data, "PrismLib.bundle"), Info.Version, "ML");
        }

        public override void OnUpdate() => Main.Update();

        public override void OnDeinitializeMelon() => Main.Shutdown();
    }
}
#endif
