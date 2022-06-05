using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using RimWorld;
using Verse;
using Verse.Sound;

namespace ProcessorFramework
{
    
    public class MapComponent_Processors : MapComponent
    {
        [Unsaved(false)]
        public List<ThingWithComps> thingsWithProcessorComp = new List<ThingWithComps>();
        public List<Thing> cachedMapIngredients = new List<Thing>();
        public int lastTick = 0;

        public MapComponent_Processors(Map map) : base(map)
        {
        }

        public List<Thing> PotentialIngredients
        {
            get
            {
                if (Find.TickManager.TicksGame > lastTick + 300)
                {
                    cachedMapIngredients.Clear();
                    foreach (ThingDef ingredientDef in ProcessorFramework_Utility.ingredientIcons.Keys)
                    {
                        cachedMapIngredients.AddRange(map.listerThings.ThingsOfDef(ingredientDef));
                    }
                    lastTick = Find.TickManager.TicksGame;
                }
                return cachedMapIngredients;
            }
        }

        public void Register(ThingWithComps thing)
        {
            thingsWithProcessorComp.Add(thing);
        }
        public void Deregister(ThingWithComps thing)
        {
            thingsWithProcessorComp.Remove(thing);
        }
    }

    [StaticConstructorOnStartup]
    public static class ProcessorFramework_Utility
    {
        public static List<ProcessDef> allProcessDefs = new List<ProcessDef>();

        public static Dictionary<ProcessDef, Texture2D> productIcons = new Dictionary<ProcessDef, Texture2D>();
        public static Dictionary<ThingDef, Texture2D> ingredientIcons = new Dictionary<ThingDef, Texture2D>();
        public static Dictionary<QualityCategory, Command_Action> qualityGizmos = new Dictionary<QualityCategory, Command_Action>();

        public static Dictionary<ProcessDef, Material> processMaterials = new Dictionary<ProcessDef, Material>();
        public static Dictionary<QualityCategory, Material> qualityMaterials = new Dictionary<QualityCategory, Material>();
        public static Command_Action emptyNowGizmo;
        public static Texture2D emptyNowIcon = ContentFinder<Texture2D>.Get("UI/EmptyNow");
        public static Texture2D emptyNowDesignation = ContentFinder<Texture2D>.Get("UI/EmptyNowDesignation");
        public static Command_Action dontEmptyGizmo;
        public static Texture2D dontEmptyIcon = ContentFinder<Texture2D>.Get("UI/Designators/Cancel");

        static ProcessorFramework_Utility()
        {
            CheckForErrors();
            CacheAllProcesses();
            RecacheAll();
        }

        public static void CheckForErrors()
        {
            List<string> warnings = new List<string>();
            foreach (ThingDef thingDef in DefDatabase<ThingDef>.AllDefs.Where(x => x.HasComp(typeof(CompProcessor)))) //we grab every thingDef that has the PF comp
            {
                if (thingDef.comps.Find(c => c.compClass == typeof(CompProcessor)) is CompProperties_Processor compPF)
                {
                    if (compPF.processes.Any(p => p.thingDef == null || p.ingredientFilter.AllowedThingDefs.EnumerableNullOrEmpty()))
                    {
                        warnings.Add(thingDef.modContentPack.Name + ": ThingDef '" + thingDef.defName + "' has processes with no product or no ingredient filter. These fields are required.");
                        compPF.processes.RemoveAll(p => p.thingDef == null || p.ingredientFilter.AllowedThingDefs.EnumerableNullOrEmpty());
                    }
                }
                if (thingDef.drawerType != DrawerType.MapMeshAndRealTime)
                {
                    warnings.Add(thingDef.modContentPack.Name + ": ThingDef '" + thingDef.defName + "' has DrawerType '" + thingDef.drawerType.ToString() + "', but MapMeshAndRealTime is required to display product icons and a progress bar.");
                }
                if (thingDef.tickerType == TickerType.Never)
                {
                    warnings.Add(thingDef.modContentPack.Name + ": ThingDef '" + thingDef.defName + "' has TickerType '" + thingDef.tickerType.ToString() + "', but processors need to tick to work.");
                }
            }
            if (warnings.Count != 0)
            {
                Log.Warning("<-- Processor Framework Warnings -->");
                foreach (string warning in warnings)
                {
                    Log.Warning(warning);
                }
            }
        }

        public static void RecacheAll() //Gets called in constructor and in writeSettings
        {
            RecacheProcessIcons();
            RecacheProcessMaterials();
            RecacheQualityGizmos();
        }

        private static void CacheAllProcesses()
        {
            List<ProcessDef> tempProcessList = new List<ProcessDef>();
            foreach (ThingDef thingDef in DefDatabase<ThingDef>.AllDefs.Where(x => x.HasComp(typeof(CompProcessor))))
            {
                if (thingDef.comps.Find(c => c.compClass == typeof(CompProcessor)) is CompProperties_Processor compPF)
                {
                    tempProcessList.AddRange(compPF.processes);
                }
            }
            for (int i = 0; i < tempProcessList.Count; i++)
            {
                tempProcessList[i].uniqueID = i;
                allProcessDefs.Add(tempProcessList[i]);
            }
        }

        public static void RecacheProcessIcons()
        {
            productIcons.Clear();
            ingredientIcons.Clear();
            foreach (ThingDef thingDef in DefDatabase<ThingDef>.AllDefs.Where(x => x.HasComp(typeof(CompProcessor)))) //we grab every thingDef that has the PF comp
            {
                if (thingDef.comps.Find(c => c.compClass == typeof(CompProcessor)) is CompProperties_Processor compPF)
                {
                    foreach (ProcessDef processDef in compPF.processes) //we loop again to make an icon for each process
                    {
                        if (!productIcons.ContainsKey(processDef))
                        {
                            productIcons.Add(processDef, GetIcon(processDef.thingDef, PF_Settings.singleItemIcon));
                        }
                        foreach (ThingDef ingredientDef in processDef.ingredientFilter.AllowedThingDefs)
                        {
                            if (!ingredientIcons.ContainsKey(ingredientDef))
                            {
                                ingredientIcons.Add(ingredientDef, GetIcon(ingredientDef, PF_Settings.singleItemIcon));
                            }
                        }
                    }
                }
            }
        }

        public static void RecacheProcessMaterials()
        {
            processMaterials.Clear();
            foreach (ProcessDef process in allProcessDefs)
            {
                Texture2D icon = GetIcon(process.thingDef, PF_Settings.singleItemIcon);
                Material mat = MaterialPool.MatFrom(icon);
                if (!processMaterials.ContainsKey(process))
                {
                    processMaterials.Add(process, mat);
                }
            }
            qualityMaterials.Clear();
            foreach (QualityCategory quality in Enum.GetValues(typeof(QualityCategory)))
            {
                Texture2D icon = ContentFinder<Texture2D>.Get("UI/QualityIcons/" + quality.ToString());
                Material mat = MaterialPool.MatFrom(icon);
                qualityMaterials.Add(quality, mat);
            }
        }

        public static void RecacheQualityGizmos()
        {
            qualityGizmos.Clear();
            foreach (QualityCategory quality in Enum.GetValues(typeof(QualityCategory)))
            {
                Command_Quality command_Quality = new Command_Quality
                {
                    defaultLabel = quality.GetLabel().CapitalizeFirst(),
                    defaultDesc = "PF_SetQualityDesc".Translate(),
                    //activateSound = SoundDefOf.Tick_Tiny,
                    icon = (Texture2D)qualityMaterials[quality].mainTexture,
                    qualityToTarget = quality
                };
                command_Quality.action = () =>
                {
                    FloatMenu floatMenu = new FloatMenu(command_Quality.RightClickFloatMenuOptions.ToList())
                    {
                        vanishIfMouseDistant = true,
                    };
                    Find.WindowStack.Add(floatMenu);
                };
                qualityGizmos.Add(quality, command_Quality);
            }
            emptyNowGizmo = CacheEmptyNowGizmo(true);
            dontEmptyGizmo = CacheEmptyNowGizmo(false);
        }
        public static Command_Action CacheEmptyNowGizmo(bool empty)
        {
            if (empty)
            {
                return new Command_Action
                {
                    defaultLabel = "PF_emptyNow".Translate(),
                    defaultDesc = "PF_emptyNowDescription".Translate(),
                    icon = emptyNowIcon,
                    action = () => { SetEmptyNow(true); },
                    activateSound = SoundDefOf.TabOpen
                };
            }
            else 
            {
                return new Command_Action
                {
                    defaultLabel = "PF_dontEmpty".Translate(),
                    defaultDesc = "PF_dontEmptyDescription".Translate(),
                    icon = dontEmptyIcon,
                    action = () => { SetEmptyNow(false); },
                    activateSound = SoundDefOf.TabClose
                };
            }

        }
        internal static void SetEmptyNow(bool empty)
        {
            foreach (Thing thing in Find.Selector.SelectedObjects.OfType<Thing>())
            {
                CompProcessor comp = thing.TryGetComp<CompProcessor>();
                if (comp != null && comp.activeProcesses.Any(x => x.processDef.usesQuality))
                {
                    comp.emptyNow = empty;
                }
            }
        }


        private static int gooseAngle = Rand.Range(0, 360);
        public static Command_Action DebugGizmo()
        {
            Command_Action gizmo = new Command_Action
            {
                defaultLabel = "Debug: Options",
                defaultDesc = "Opens a float menu with debug options.",
                icon = ContentFinder<Texture2D>.Get("UI/DebugGoose"),
                iconAngle = gooseAngle,
                iconDrawScale = 1.25f
            };
            gizmo.action = () =>
            {
                FloatMenu floatMenu = new FloatMenu(DebugOptions())
                {
                    vanishIfMouseDistant = true,
                };
                Find.WindowStack.Add(floatMenu);
            };
            return gizmo;
        }

        public static List<FloatMenuOption> DebugOptions()
        {
            List<FloatMenuOption> floatMenuOptions = new List<FloatMenuOption>();
            IEnumerable<ThingWithComps> things = Find.Selector.SelectedObjects.OfType<ThingWithComps>().Where(t => t.GetComp<CompProcessor>() != null);
            IEnumerable<CompProcessor> comps = things.Select(t => t.TryGetComp<CompProcessor>());

            if (comps.Any(c => !c.Empty))
            {
                floatMenuOptions.Add(new FloatMenuOption("Finish process", () => FinishProcess(comps)));
                floatMenuOptions.Add(new FloatMenuOption("Progress one day", () => ProgressOneDay(comps)));
                floatMenuOptions.Add(new FloatMenuOption("Progress half quadrum", () => ProgressHalfQuadrum(comps)));
            }

            if (comps.Any(c => c.AnyComplete))
            {
                floatMenuOptions.Add(new FloatMenuOption("Empty object", () => EmptyObject(comps)));
            }

            if (comps.Any(c => c.Empty))
            {
                floatMenuOptions.Add(new FloatMenuOption("Fill object", () => FillObject(comps)));
            }
            //floatMenuOptions.Add(new FloatMenuOption("Fix Error", () => FixNREingredientThings(comps)));
            return floatMenuOptions;
        }

        internal static void FinishProcess(IEnumerable<CompProcessor> comps)
        {
            foreach (CompProcessor comp in comps) 
            {
                foreach (ActiveProcess activeProcess in comp.activeProcesses)
                {
                    if (activeProcess.processDef.usesQuality)
                    {
                        activeProcess.activeProcessTicks = Mathf.RoundToInt(activeProcess.DaysToReachTargetQuality * GenDate.TicksPerDay);
                    }
                    else
                    {
                        activeProcess.activeProcessTicks = Mathf.RoundToInt(activeProcess.processDef.processDays * GenDate.TicksPerDay);
                    }
                }
            }
            gooseAngle = Rand.Range(0, 360);
            SoundStarter.PlayOneShotOnCamera(DefOf.PF_Honk);
        }

        internal static void ProgressOneDay(IEnumerable<CompProcessor> comps)
        {
            foreach (CompProcessor comp in comps) 
            {
                foreach (ActiveProcess activeProcess in comp.activeProcesses)
                {
                    activeProcess.activeProcessTicks += GenDate.TicksPerDay;
                }
            }
            gooseAngle = Rand.Range(0, 360);
            SoundStarter.PlayOneShotOnCamera(DefOf.PF_Honk);
        }

        internal static void ProgressHalfQuadrum(IEnumerable<CompProcessor> comps)
        {
            foreach (CompProcessor comp in comps) 
            {
                foreach (ActiveProcess activeProcess in comp.activeProcesses)
                {
                    activeProcess.activeProcessTicks += GenDate.TicksPerQuadrum / 2;
                }
            }
            gooseAngle = Rand.Range(0, 360);
            SoundStarter.PlayOneShotOnCamera(DefOf.PF_Honk);
        }

        internal static void EmptyObject(IEnumerable<CompProcessor> comps)
        {
            
            foreach (CompProcessor comp in comps) 
            {
                List<ActiveProcess> finishedProcesses = new List<ActiveProcess>();
                foreach (ActiveProcess activeProcess in comp.activeProcesses)
                {
                    if (activeProcess.Complete)
                    {
                        finishedProcesses.Add(activeProcess);
                    }
                }
                foreach (ActiveProcess finishedProcess in finishedProcesses)
                {
                    Thing product = comp.TakeOutProduct(finishedProcess);
                    GenPlace.TryPlaceThing(product, comp.parent.Position, comp.parent.Map, ThingPlaceMode.Near);
                }
            }
            gooseAngle = Rand.Range(0, 360);
            SoundStarter.PlayOneShotOnCamera(DefOf.PF_Honk);
        }

        internal static void FillObject(IEnumerable<CompProcessor> comps)
        {
            {
                foreach (CompProcessor comp in comps) 
                {
                    if (comp.Empty && !comp.enabledProcesses.EnumerableNullOrEmpty())
                    {
                        ProcessDef processDef = comp.enabledProcesses.Keys.First();
                        Thing ingredient = ThingMaker.MakeThing(processDef.ingredientFilter.AnyAllowedDef);
                        ingredient.stackCount = comp.SpaceLeftFor(processDef);
                        comp.AddIngredient(ingredient, processDef);
                    }
                }
                gooseAngle = Rand.Range(0, 360);
                SoundStarter.PlayOneShotOnCamera(DefOf.PF_Honk);
            }
        }

        internal static void FixNREingredientThings(IEnumerable<CompProcessor> comps)
        {
            foreach (CompProcessor comp in comps)
            {
                foreach (ActiveProcess activeProcess in comp.activeProcesses)
                {
                    activeProcess.ingredientThings.RemoveAll(x => x == null);
                }
            }
        }

        public static string IngredientFilterSummary(ThingFilter thingFilter)
        {
            return thingFilter.Summary;
        }

        public static string ToStringPercentColored(this float val, List<Pair<float, Color>> colors = null)
        {
            colors ??= ITab_ProcessorContents.GreenToYellowToRed;
            return val.ToStringPercent().Colorize(GenUI.LerpColor(colors, val));
        }

        // Try to get a texture of a thingDef; If not found, use LaunchReport icon
        public static Texture2D GetIcon(ThingDef thingDef, bool singleStack = true)
        {
            Texture2D icon = null;
            if (thingDef?.graphicData?.texPath == null)
            {
                icon = ContentFinder<Texture2D>.GetAllInFolder(thingDef.race.AnyPawnKind.lifeStages.First().bodyGraphicData.texPath).FirstOrDefault();
            }
            if (icon == null) icon = ContentFinder<Texture2D>.Get(thingDef.graphicData.texPath, false);
            if (icon == null)
            {
                // Use the first texture in the folder
                icon = singleStack ? ContentFinder<Texture2D>.GetAllInFolder(thingDef.graphicData.texPath).FirstOrDefault() : ContentFinder<Texture2D>.GetAllInFolder(thingDef.graphicData.texPath).LastOrDefault();
                if (icon == null)
                {
                    icon = ContentFinder<Texture2D>.Get("UI/Commands/LaunchReport", true);
                    Log.Warning("Universal Fermenter:: No texture at " + thingDef.graphicData.texPath + ".");
                }
            }
            return icon;
        }
    }
}
