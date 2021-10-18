using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using RimWorld;
using Verse;
using Verse.Sound;
using UnityEngine;

namespace ProcessorFramework
{
    public class ProcessorFramework_Core : Mod
    {
        public static PF_Settings settings;
        public ProcessorFramework_Core(ModContentPack content) : base(content)
        {
            settings = GetSettings<PF_Settings>();
        }
        public override string SettingsCategory() => "PF_SettingsCategory".Translate();

        public override void DoSettingsWindowContents(Rect inRect)
        {
            checked
            {
                Listing_Standard listing_Standard = new Listing_Standard();
                listing_Standard.Begin(inRect);
                listing_Standard.CheckboxLabeled("PF_ShowProcessIcon".Translate(), ref PF_Settings.showProcessIconGlobal, "PF_ShowProcessIconTooltip".Translate());
                listing_Standard.Gap(12);
                listing_Standard.Label("PF_ProcessIconSize".Translate() +  ": " + PF_Settings.processIconSize.ToStringByStyle(ToStringStyle.PercentZero), -1, "PF_ProcessIconSizeTooltip".Translate());
                PF_Settings.processIconSize = listing_Standard.Slider(GenMath.RoundTo(PF_Settings.processIconSize, 0.05f), 0.2f, 1f);
                listing_Standard.CheckboxLabeled("PF_SingleItemIcon".Translate(), ref PF_Settings.singleItemIcon, "PF_SingleItemIconTooltip".Translate());
                listing_Standard.GapLine(30);
                listing_Standard.CheckboxLabeled("PF_ShowCurrentQualityIcon".Translate(), ref PF_Settings.showCurrentQualityIcon, "PF_ShowCurrentQualityIconTooltip".Translate());
                listing_Standard.Gap(12);
                listing_Standard.Label("PF_defaultQuality".Translate() + ": " + ((QualityCategory)PF_Settings.defaultTargetQualityInt).GetLabel() , tooltip: "PF_defaultQualityTooltip".Translate());
                PF_Settings.defaultTargetQualityInt = Mathf.RoundToInt(listing_Standard.Slider(PF_Settings.defaultTargetQualityInt, 0, 6));
                listing_Standard.Gap(12);
                listing_Standard.CheckboxLabeled("PF_replaceDestroyedProcessors".Translate(), ref PF_Settings.replaceDestroyedProcessors, "PF_replaceDestroyedProcessorsTooltip".Translate());
                listing_Standard.GapLine(30);
                Rect rectReplaceBarrels = listing_Standard.GetRect(30f);
                TooltipHandler.TipRegion(rectReplaceBarrels, "PF_ReplaceVanillaBarrelsTooltip".Translate());
                if (Widgets.ButtonText(rectReplaceBarrels, "PF_ReplaceVanillaBarrels".Translate(), true, true, true))
                {
                    SoundDefOf.Click.PlayOneShotOnCamera();
                    ReplaceVanillaBarrels();
                }
                listing_Standard.GapLine(30);
                Rect rectDefaultSettings = listing_Standard.GetRect(30f);
                TooltipHandler.TipRegion(rectDefaultSettings, "PF_DefaultSettingsTooltip".Translate());
                if (Widgets.ButtonText(rectDefaultSettings, "PF_DefaultSettings".Translate(), true, true, true))
                {
                    PF_Settings.showProcessIconGlobal = true;
                    PF_Settings.processIconSize = 0.6f;
                    PF_Settings.singleItemIcon = true;
                    PF_Settings.showCurrentQualityIcon = true;
                }
                listing_Standard.End();
                settings.Write();
            }
        }

        public override void WriteSettings()
        {
            base.WriteSettings();
            ProcessorFramework_Utility.RecacheAll();
        }

        public void ReplaceVanillaBarrels()
        {
            if (Current.ProgramState != ProgramState.Playing)
            {
                return;
            }
            foreach (Map map in Find.Maps)
            {
                foreach (Thing thing in map.listerThings.ThingsOfDef(ThingDefOf.FermentingBarrel).ToList())
                {
                    bool inUse = false;
                    float progress = 0;
                    int fillCount = 0;
                    IntVec3 position = thing.Position;
                    ThingDef stuff;
                    if (thing.Stuff != null)
                    {
                         stuff = thing.Stuff;
                    }
                    else
                    {
                        stuff = ThingDefOf.WoodLog;
                    }
                    if (thing is Building_FermentingBarrel oldBarrel)
                    {
                        inUse = oldBarrel.SpaceLeftForWort < 25;
                        if (inUse)
                        {
                            progress = oldBarrel.Progress;
                            fillCount = 25 - oldBarrel.SpaceLeftForWort;
                        }
                    }
                    Thing newBarrel = ThingMaker.MakeThing(DefOf.BarrelProcessor, stuff);
                    GenSpawn.Spawn(newBarrel, position, map);
                    if (inUse)
                    {
                        CompProcessor compProcessor = newBarrel.TryGetComp<CompProcessor>();
                        Thing wort = ThingMaker.MakeThing(ThingDefOf.Wort, null);
                        wort.stackCount = fillCount;
                        compProcessor.AddIngredient(wort);
                        compProcessor.activeProcesses.Find(x => x.processDef.ingredientFilter.Allows(wort)).activeProcessTicks = Mathf.RoundToInt(6 * GenDate.TicksPerDay * progress);
                    }
                }
                foreach (Thing thing in map.listerThings.ThingsOfDef(ThingDefOf.MinifiedThing).Where(t => t.GetInnerIfMinified().def == ThingDefOf.FermentingBarrel))
                {
                    MinifiedThing minifiedThing = thing as MinifiedThing;
                    ThingDef stuff;
                    if (minifiedThing.InnerThing.Stuff != null)
                    {
                        stuff = minifiedThing.InnerThing.Stuff;
                    }
                    else
                    {
                        stuff = ThingDefOf.WoodLog;
                    }
                    minifiedThing.InnerThing = null;
                    Thing newBarrel = ThingMaker.MakeThing(DefOf.BarrelProcessor, stuff);
                    minifiedThing.InnerThing = newBarrel;
                    cachedGraphic.SetValue(minifiedThing, null);
                }
            }
        }
        public static FieldInfo cachedGraphic = typeof(MinifiedThing).GetField("cachedGraphic", BindingFlags.NonPublic | BindingFlags.Instance);
    }

    public class PF_Settings : ModSettings
    {
        public static bool showProcessIconGlobal = true;
        public static float processIconSize = 0.6f;
        public static bool showCurrentQualityIcon = true;
        public static bool singleItemIcon = true;
        public static int defaultTargetQualityInt = 0;
        public static bool replaceDestroyedProcessors = true;
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look<bool>(ref showProcessIconGlobal, "PF_showProcessIconGlobal", true, true);
            Scribe_Values.Look<float>(ref processIconSize, "PF_processIconSize", 0.6f, true);
            Scribe_Values.Look<bool>(ref showCurrentQualityIcon, "PF_showCurrentQualityIcon", true, true);
            Scribe_Values.Look<bool>(ref singleItemIcon, "PF_singleItemIcon", true, true);
            Scribe_Values.Look<int>(ref defaultTargetQualityInt, "PF_defaultTargetQualityInt", 0, false);
            Scribe_Values.Look<bool>(ref replaceDestroyedProcessors, "PF_replaceDestroyedProcessors", true, true);
        }
    }
}
