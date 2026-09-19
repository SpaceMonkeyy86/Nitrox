using System.Reflection;
using NitroxClient.GameLogic;

namespace NitroxPatcher.Patches.Dynamic;

public sealed partial class FireExtinguisher_Update_Patch : NitroxPatch, IDynamicPatch
{
    public static readonly MethodInfo TARGET_METHOD = Reflect.Method((FireExtinguisher t) => t.Update());

    public static Fire lastTarget;

    public static void Postfix(FireExtinguisher __instance)
    {
        if (!__instance.isDrawn)
        {
            return;
        }

        Fire target = __instance.usedThisFrame ? __instance.fireTarget : null;

        if (target != lastTarget)
        {
            if (lastTarget)
            {
                Resolve<Fires>().OnDouseChange(lastTarget, 0f);
            }

            if (target)
            {
                Resolve<Fires>().OnDouseChange(target, __instance.fireDousePerSecond);
            }
        }
    }
}
