using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using PrismLib.UI.Toolkit;

namespace CamDisplay
{
    /* PrismLib.UI's InputGate (keys, wheel and the game's mouse reads withheld while a Prism window
       is open) and FontFix only work once some mod applies their patches with its own Harmony.
       Topaz and Sapphire do; standalone, nobody would, and the game would take every key and
       click typed into CamDisplay's windows. One install per process: whoever comes first. */
    internal static class Gate
    {
        private static HarmonyLib.Harmony _h;   // qualified: MelonLoader also has a "Harmony" namespace

        internal static void Tick()
        {
            if (_h == null) _h = new HarmonyLib.Harmony("PrismMods.CamDisplay");
            InputGate.EnsureInstalled("CamDisplay", Patch);
            FontFix.EnsureInstalled("CamDisplay", Patch);
        }

        // The patches stay (they only read PrismLib.UI's shared state); clearing the owner lets
        // another mod's next EnsureInstalled take over.
        internal static void Shutdown()
        {
            InputGate.Uninstalled("CamDisplay");
            FontFix.Uninstalled("CamDisplay");
        }

        // Same shape as Topaz's GatePatch: PrismLib.UI says what to patch and how.
        private static void Patch(MethodBase target, MethodInfo patch, string kind)
        {
            var proc = _h.CreateProcessor(target);
            if (kind == "redirect")
                proc.AddTranspiler(new HarmonyMethod(typeof(Gate).GetMethod(nameof(RedirectMouse), BindingFlags.NonPublic | BindingFlags.Static)));
            else if (kind == "prefix") proc.AddPrefix(new HarmonyMethod(patch));
            else if (kind == "finalizer") proc.AddFinalizer(new HarmonyMethod(patch));
            else proc.AddPostfix(new HarmonyMethod(patch));
            proc.Patch();
        }

        // The game's mouse-button reads are engine calls Harmony cannot patch here, so the gate
        // redirects their callers: each call becomes InputGate's gated equivalent.
        private static IEnumerable<CodeInstruction> RedirectMouse(IEnumerable<CodeInstruction> code)
        {
            foreach (var ins in code)
            {
                if ((ins.opcode == OpCodes.Call || ins.opcode == OpCodes.Callvirt) && ins.operand is MethodBase called)
                {
                    var to = InputGate.RedirectFor(called);
                    if (to != null) { ins.opcode = OpCodes.Call; ins.operand = to; }
                }
                yield return ins;
            }
        }
    }
}
