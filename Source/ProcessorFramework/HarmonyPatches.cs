using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using RimWorld;
using Verse;
using UnityEngine;

namespace ProcessorFramework
{
    [StaticConstructorOnStartup]
    public static class HarmonyPatches
    {
        static HarmonyPatches()
        {
            var harmony = new Harmony("Syrchalis.Rimworld.UniversalFermenter");
            harmony.PatchAll(Assembly.GetExecutingAssembly());
        }
    }
    [HarmonyPatch(typeof(Building_FermentingBarrel), nameof(Building_FermentingBarrel.GetInspectString))]
    public class OldBarrel_GetInspectStringPatch
    {
        [HarmonyPrefix]
        public static bool OldBarrel_GetInspectString_Postfix(ref string __result)
        {
            __result = "PF_OldBarrelInspectString".Translate();
            return false;
        }
    }

    [HarmonyPatch(typeof(MainTabWindow_Inspect), nameof(MainTabWindow_Inspect.CurTabs), MethodType.Getter)]
    public class CurTabsPatch
    {
        [HarmonyPostfix]
        public static void CurTabs_Postfix(ref IEnumerable<InspectTabBase> __result)
        {
            List<object> objects = Find.Selector.SelectedObjects;
            if (!objects.NullOrEmpty())
            {
                Thing firstThing = objects.First() as Thing;
                if (objects.All(x => x is Thing thing && thing.Faction == Faction.OfPlayerSilentFail && thing.TryGetComp<CompProcessor>() != null
            && thing.def == firstThing.def))
                {
                    Thing thing = Find.Selector.SelectedObjects.First() as Thing;
                    __result = thing.GetInspectTabs();
                }
            }
        }
    }
}
