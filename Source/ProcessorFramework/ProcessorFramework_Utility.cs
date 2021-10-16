﻿using System;
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

        public MapComponent_Processors(Map map) : base(map)
        {
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

        public static Dictionary<ProcessDef, Command_Action> processGizmos = new Dictionary<ProcessDef, Command_Action>();
        public static Dictionary<QualityCategory, Command_Action> qualityGizmos = new Dictionary<QualityCategory, Command_Action>();

        public static Dictionary<ProcessDef, Material> processMaterials = new Dictionary<ProcessDef, Material>();
        public static Dictionary<QualityCategory, Material> qualityMaterials = new Dictionary<QualityCategory, Material>();

        static ProcessorFramework_Utility()
        {
            CheckForErrors();
            CacheAllProcesses();
            RecacheAll();
        }

        public static void CheckForErrors()
        {
            bool sendWarning = false;
            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.AppendLine("<-- Processor Framework Errors -->");
            foreach (ThingDef thingDef in DefDatabase<ThingDef>.AllDefs.Where(x => x.HasComp(typeof(CompProcessor)))) //we grab every thingDef that has the PF comp
            {
                if (thingDef.comps.Find(c => c.compClass == typeof(CompProcessor)) is CompProperties_Processor compPF)
                {
                    if (compPF.processes.Any(p => p.thingDef == null || p.ingredientFilter.AllowedThingDefs.EnumerableNullOrEmpty()))
                    {
                        stringBuilder.AppendLine("ThingDef '" + thingDef.defName + "' has processes with no product or no filter. These fields are required.");
                        compPF.processes.RemoveAll(p => p.thingDef == null || p.ingredientFilter.AllowedThingDefs.EnumerableNullOrEmpty());
                        sendWarning = true;
                    }
                }
            }
            if (sendWarning)
            {
                Log.Warning(stringBuilder.ToString().TrimEndNewlines());
            }
        }

        public static void RecacheAll() //Gets called in constructor and in writeSettings
        {
            //RecacheProcessGizmos();
            RecacheProcessMaterials();
            //RecacheQualityGizmos();
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

        /*public static void RecacheProcessGizmos()
        {
            processGizmos.Clear();
            foreach (ThingDef thingDef in DefDatabase<ThingDef>.AllDefs.Where(x => x.HasComp(typeof(CompProcessor)))) //we grab every thingDef that has the PF comp
            {
                if (thingDef.comps.Find(c => c.compClass == typeof(CompProcessor)) is CompProperties_Processor compPF)
                {
                    foreach (ProcessDef process in compPF.processes) //we loop again to make a gizmo for each process, now that we have a complete FloatMenuOption list
                    {
                        Command_Process command_Process = new Command_Process
                        {
                            defaultLabel = process.customLabel != "" ? process.customLabel : process.thingDef.label,
                            defaultDesc = "PF_NextDesc".Translate(process.thingDef.label, IngredientFilterSummary(process.ingredientFilter)),
                            //activateSound = SoundDefOf.Tick_Tiny,
                            icon = GetIcon(process.thingDef, PF_Settings.singleItemIcon),
                            processToTarget = process,
                            processOptions = compPF.processes
                            
                        };
                        command_Process.action = () =>
                        {
                            FloatMenu floatMenu = new FloatMenu(command_Process.RightClickFloatMenuOptions.ToList())
                            {
                                vanishIfMouseDistant = true,
                            };
                            Find.WindowStack.Add(floatMenu);
                        };
                        processGizmos.Add(process, command_Process);
                    }
                }
            }
        }*/

        public static void RecacheProcessMaterials()
        {
            processMaterials.Clear();
            foreach (ProcessDef process in allProcessDefs)
            {
                Texture2D icon = GetIcon(process.thingDef, PF_Settings.singleItemIcon);
                Material mat = MaterialPool.MatFrom(icon);
                processMaterials.Add(process, mat);
            }
            qualityMaterials.Clear();
            foreach (QualityCategory quality in Enum.GetValues(typeof(QualityCategory)))
            {
                Texture2D icon = ContentFinder<Texture2D>.Get("UI/QualityIcons/" + quality.ToString());
                Material mat = MaterialPool.MatFrom(icon);
                qualityMaterials.Add(quality, mat);
            }
        }

        /*public static void RecacheQualityGizmos()
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
        }*/

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
                foreach (ActiveProcess activeProcess in comp.activeProcesses)
                {
                    if (activeProcess.Complete)
                    {
                        Thing product = comp.TakeOutProduct(activeProcess);
                        GenPlace.TryPlaceThing(product, comp.parent.Position, comp.parent.Map, ThingPlaceMode.Near);
                    }
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
                    if (comp.Empty)
                    {
                        Thing ingredient = ThingMaker.MakeThing(comp.Props.processes.First().ingredientFilter.AnyAllowedDef);
                        ingredient.stackCount = comp.SpaceLeft;
                        comp.AddIngredient(ingredient);
                    }
                }
                gooseAngle = Rand.Range(0, 360);
                SoundStarter.PlayOneShotOnCamera(DefOf.PF_Honk);
            }
        }

        public static string IngredientFilterSummary(ThingFilter thingFilter)
        {
            return thingFilter.Summary;
        }

        public static string VowelTrim(string str, int limit)
        {
            int vowelsToRemove = str.Length - limit;
            for (int i = str.Length - 1; i > 0; i--)
            {
                if (vowelsToRemove <= 0)
                    break;

                if (IsVowel(str[i]))
                {
                    if (str[i - 1] == ' ')
                    {
                        continue;
                    }
                    else
                    {
                        str = str.Remove(i, 1);
                        vowelsToRemove--;
                    }
                }
            }

            if (str.Length > limit)
            {
                str = str.Remove(limit - 2) + "..";
            }

            return str;
        }

        public static bool IsVowel(char c)
        {
            var vowels = new HashSet<char> { 'a', 'e', 'i', 'o', 'u' };
            return vowels.Contains(c);
        }

        // Try to get a texture of a thingDef; If not found, use LaunchReport icon
        public static Texture2D GetIcon(ThingDef thingDef, bool singleStack = true)
        {
            Texture2D icon = ContentFinder<Texture2D>.Get(thingDef.graphicData.texPath, false);
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
