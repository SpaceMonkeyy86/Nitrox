using System.Reflection;
using NitroxClient.GameLogic;

namespace NitroxPatcher.Patches.Dynamic;

public sealed partial class Fire_Douse_Patch : NitroxPatch, IDynamicPatch
{
    public static readonly MethodInfo TARGET_METHOD = Reflect.Method((Fire t) => t.Douse(default(float)));

    public static void Postfix(Fire __instance, float amount)
    {
        if (__instance.livemixin.health <= 0f || __instance.IsExtinguished())
        {
            Resolve<Fires>().OnExtinguish(__instance);
        }
        else if (amount >= 20f)
        {
            // Called by fire suppression system
            Resolve<Fires>().OnDouseOnce(__instance);
        }
    }
}
