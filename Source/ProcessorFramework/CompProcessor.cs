// Notes:
//   * parent.Map is null when the building (parent) is minified (uninstalled).

using System.Collections.Generic;
using System.Text;
using System.Linq;
using UnityEngine;
using RimWorld;
using Verse;
using HarmonyLib;

namespace ProcessorFramework
{
    public class CompProcessor : ThingComp, IThingHolder
    {
        public List<ActiveProcess> activeProcesses = new List<ActiveProcess>();
        public Dictionary<ProcessDef, ProcessFilter> enabledProcesses = new Dictionary<ProcessDef, ProcessFilter>();

        public Dictionary<ProcessDef, QualityCategory> cachedTargetQualities = new Dictionary<ProcessDef, QualityCategory>();
        public bool emptyNow = false;

        public bool graphicChangeQueued = false;

        public CompRefuelable refuelComp;
        public CompPowerTrader powerTradeComp;
        public CompFlickable flickComp;

        public ThingOwner innerContainer = null;

        //----------------------------------------------------------------------------------------------------
        // Properties
        public CompProperties_Processor Props => (CompProperties_Processor)props;

        public bool AnyRuined => activeProcesses.Any(x => x.Ruined);
        public bool Empty => TotalIngredientCount <= 0;
        public bool AnyComplete => activeProcesses.Any(x => x.Complete);
        public int SpaceLeft => Props.capacity - TotalIngredientCount;
        public int TotalIngredientCount => Mathf.CeilToInt(activeProcesses.Sum(x => x.ingredientCount * x.processDef.capacityFactor));
        public HashSet<ThingDef> ValidIngredients
        {
            get
            {
                HashSet<ThingDef> validIngredients = new HashSet<ThingDef>();
                foreach (ProcessFilter processFilter in enabledProcesses.Values)
                {
                    validIngredients.AddRange(processFilter.allowedIngredients);
                }
                return validIngredients;
            }
        }
        public bool TemperatureOk
        {
            get
            {
                float temp = parent.AmbientTemperature;
                foreach (ProcessDef process in enabledProcesses.Keys)
                {
                    if (temp >= process.temperatureSafe.min - 2 || temp <= process.temperatureSafe.max + 2)
                    {
                        return true;
                    }
                }
                return false;
            }
        }
        public float PowerConsumptionRate
        {
            get
            {
                float rate = 0f;
                foreach (ActiveProcess activeProcess in activeProcesses)
                {
                    rate += activeProcess.processDef.powerUseFactor * activeProcess.ingredientCount * activeProcess.processDef.capacityFactor;
                }
                return rate == 0 ? 1 : rate / TotalIngredientCount;
            }
        }
        public float FuelConsumptionRate
        {
            get
            {
                float rate = 0f;
                foreach (ActiveProcess activeProcess in activeProcesses)
                {
                    rate += activeProcess.processDef.fuelUseFactor * activeProcess.ingredientCount * activeProcess.processDef.capacityFactor;
                }
                return rate == 0 ? 1 : rate / TotalIngredientCount;
            }
        }
        public float RoofCoverage  // How much of the building is under a roof
        {
            get
            {
                if (parent.Map == null)
                {
                    return 0f;
                }
                int allTiles = 0;
                int roofedTiles = 0;
                foreach (IntVec3 current in parent.OccupiedRect())
                {
                    allTiles++;
                    if (parent.Map.roofGrid.Roofed(current))
                    {
                        roofedTiles++;
                    }
                }
                return (float)roofedTiles / (float)allTiles;
            }
        }
        public bool Fueled => refuelComp == null || refuelComp.HasFuel;
        public bool Powered => powerTradeComp == null || powerTradeComp.PowerOn;
        public bool FlickedOn => flickComp == null || flickComp.SwitchIsOn;


        //----------------------------------------------------------------------------------------------------
        // Interfaces
        public ThingOwner GetDirectlyHeldThings()
        {
            return innerContainer;
        }
        public void GetChildHolders(List<IThingHolder> outChildren)
        {
            ThingOwnerUtility.AppendThingHoldersFromThings(outChildren, GetDirectlyHeldThings());
        }

        //----------------------------------------------------------------------------------------------------
        // Overrides

        public override void Initialize(CompProperties props)
        {
            base.Initialize(props);
            innerContainer = new ThingOwner<Thing>(this);

            parent.def.inspectorTabsResolved ??= new List<InspectTabBase>();
            if (!parent.def.inspectorTabsResolved.Any(t => t is ITab_ProcessSelection))
            {
                parent.def.inspectorTabsResolved.Add(InspectTabManager.GetSharedInstance(typeof(ITab_ProcessSelection)));
                parent.def.inspectorTabsResolved.Add(InspectTabManager.GetSharedInstance(typeof(ITab_ProcessorContents)));
            }

            if (PF_Settings.initialProcessState == PF_Settings.InitialProcessState.firstonly)
            {
                ToggleProcess(Props.processes.First(), true);
            }
            else if (PF_Settings.initialProcessState == PF_Settings.InitialProcessState.enabled)
            {
                EnableAllProcesses();
            }
        }

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            refuelComp = parent.GetComp<CompRefuelable>();
            powerTradeComp = parent.GetComp<CompPowerTrader>();
            flickComp = parent.GetComp<CompFlickable>();
            parent.Map.GetComponent<MapComponent_Processors>().Register(parent);
            if (!Empty)
            {
                graphicChangeQueued = true;
            }
            if (enabledProcesses == null) //backCompatibility otherwise dict is null because the saved value doesn't exist (=null)
            {
                enabledProcesses = new Dictionary<ProcessDef, ProcessFilter>();
            }
        }

        public override void PostDestroy(DestroyMode mode, Map previousMap)
        {
            base.PostDestroy(mode, previousMap);
            if (mode != DestroyMode.Vanish && Props.dropIngredients)
            {
                foreach (Thing thing in innerContainer)
                {
                    GenSpawn.Spawn(thing, parent.Position, previousMap);
                }
            }
        }

        public override void PostDeSpawn(Map map)
        {
            base.PostDeSpawn(map);
            map.GetComponent<MapComponent_Processors>().Deregister(parent);
        }
        
        public override void PostExposeData()
        {
            Scribe_Deep.Look(ref innerContainer, "PF_innerContainer", this);
            Scribe_Collections.Look(ref activeProcesses,  "PF_activeProcesses", LookMode.Deep, this);
            Scribe_Collections.Look(ref enabledProcesses, "PF_enabledProcesses", LookMode.Def, LookMode.Deep);
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            //Dev options			
            if (Prefs.DevMode)
            {
                yield return ProcessorFramework_Utility.DebugGizmo();
            }
            //Default buttons
            foreach (Gizmo c in base.CompGetGizmosExtra())
            {
                yield return c;
            }
            if (activeProcesses.Any(x => x.processDef.usesQuality))
            {
                if (emptyNow)
                {
                    yield return ProcessorFramework_Utility.dontEmptyGizmo;
                }
                else
                {
                    yield return ProcessorFramework_Utility.emptyNowGizmo;
                }
                yield return ProcessorFramework_Utility.qualityGizmos[activeProcesses.First(x => x.processDef.usesQuality).TargetQuality];
            }
        }

        public override void PostDraw()
        {
            base.PostDraw();
            if (!Empty)
            {
                if (graphicChangeQueued)
                {
                    GraphicChange(false);
                    graphicChangeQueued = false;
                }
                bool showCurrentQuality = !Props.parallelProcesses && activeProcesses[0].processDef.usesQuality && PF_Settings.showCurrentQualityIcon;
                Vector3 drawPos = parent.DrawPos;
                drawPos.x += Props.barOffset.x - (showCurrentQuality ? 0.1f : 0f);
                drawPos.y += 0.02f;
                drawPos.z += Props.barOffset.y;

                Vector2 size = Static_Bar.Size * Props.barScale;

                // Border
                Graphics.DrawMesh(MeshPool.plane10, Matrix4x4.TRS(drawPos, Quaternion.identity, new Vector3(size.x + 0.1f, 1, size.y + 0.1f)), Static_Bar.UnfilledMat, 0);

                float xPosAccum = 0;
                for (int i = 0; i < activeProcesses.Count; i++)
                {
                    ActiveProcess activeProcess = activeProcesses[i];
                    float width = size.x * ((float)activeProcess.ingredientCount * activeProcess.processDef.capacityFactor / Props.capacity);
                    float xPos = (drawPos.x - (size.x / 2.0f)) + (width / 2.0f) + xPosAccum;
                    xPosAccum += width;
                    Graphics.DrawMesh(MeshPool.plane10, Matrix4x4.TRS(new Vector3(xPos, drawPos.y + 0.01f, drawPos.z), Quaternion.identity, new Vector3(width, 1, size.y)), activeProcess.ProgressColorMaterial, 0);
                }

                if (showCurrentQuality) // show small icon for current quality over bar
                {
                    drawPos.y += 0.02f;
                    drawPos.x += 0.45f * Props.barScale.x;
                    Matrix4x4 matrix2 = default(Matrix4x4);
                    matrix2.SetTRS(drawPos, Quaternion.identity, new Vector3(0.2f * Props.barScale.x, 1f, 0.2f * Props.barScale.y));
                    Graphics.DrawMesh(MeshPool.plane10, matrix2, ProcessorFramework_Utility.qualityMaterials[activeProcesses[0].CurrentQuality], 0);
                }
            }
            if (!activeProcesses.NullOrEmpty() && Props.showProductIcon && PF_Settings.showProcessIconGlobal && parent.Map.designationManager.DesignationOn(parent) == null && !emptyNow)
            {
                Vector3 drawPos = parent.DrawPos;
                float sizeX = PF_Settings.processIconSize * Props.productIconSize.x;
                float sizeZ = PF_Settings.processIconSize * Props.productIconSize.y;
                if (Props.processes.Count == 1 && activeProcesses[0].processDef.usesQuality) // show larger, centered quality icon if object has only one process
                {
                    drawPos.y += 0.02f;
                    drawPos.z += 0.05f;
                    Matrix4x4 matrix = default(Matrix4x4);
                    matrix.SetTRS(drawPos, Quaternion.identity, new Vector3(0.6f * sizeX, 1f, 0.6f * sizeZ));
                    Graphics.DrawMesh(MeshPool.plane10, matrix, ProcessorFramework_Utility.qualityMaterials[activeProcesses[0].TargetQuality], 0);
                }
                else if (!Empty) // show process icon if object has more than one process
                {
                    IEnumerable<ProcessDef> uniqueProcessDefs = activeProcesses.GroupBy(x => x.processDef).Select(x => x.Key);
                    drawPos.y += 0.2f;
                    drawPos.z += 0.05f;
                    drawPos.x -= (uniqueProcessDefs.Count() - 1) * sizeX * 0.25f;
                    foreach (ProcessDef processDef in uniqueProcessDefs)
                    {
                        Matrix4x4 matrix = default;
                        matrix.SetTRS(drawPos, Quaternion.identity, new Vector3(sizeX, 1f, sizeZ));
                        Graphics.DrawMesh(MeshPool.plane10, matrix, ProcessorFramework_Utility.processMaterials[processDef], 0);
                        drawPos.x += sizeX * 0.5f;
                        drawPos.y -= 0.01f;
                    }
                    /*if (activeProcesses[0].processDef.usesQuality && PF_Settings.showTargetQualityIcon) // show small offset quality icon if object also uses quality
                    {
                        drawPos.y += 0.01f;
                        drawPos.x += 0.25f * sizeX;
                        drawPos.z -= 0.35f * sizeZ;
                        Matrix4x4 matrix2 = default(Matrix4x4);
                        matrix2.SetTRS(drawPos, Quaternion.identity, new Vector3(0.4f * sizeX, 1f, 0.4f * sizeZ));
                        Graphics.DrawMesh(MeshPool.plane10, matrix2, ProcessorFramework_Utility.qualityMaterials[activeProcesses[0].TargetQuality], 0);
                    }*/
                }
            }
            if (emptyNow)
            {
                Matrix4x4 matrix = default;
                matrix.SetTRS(parent.DrawPos + new Vector3(0f, 0.3f, 0f), Quaternion.identity, new Vector3(0.8f, 1f, 0.8f));
                Graphics.DrawMesh(MeshPool.plane10, matrix, MaterialPool.MatFrom(ProcessorFramework_Utility.emptyNowDesignation), 0);
            }
        }

        public override void CompTick()
        {
            //If TickerType=Normal is chosen for unrelated reasons the comp shouldn't tick all the time
            if (parent.IsHashIntervalTick(60))
            {
                DoTicks(60);
            }
            if (parent.IsHashIntervalTick(250))
            {
                DoActiveProcessesRareTicks();
                AdjustPowerConsumption();
            }
        }
        public override void CompTickRare()
        {
            DoTicks(GenTicks.TickRareInterval);
            DoActiveProcessesRareTicks();
            AdjustPowerConsumption();
        }
        public override void CompTickLong()
        {
            DoTicks(GenTicks.TickLongInterval);
            DoActiveProcessesRareTicks();
        }

        //----------------------------------------------------------------------------------------------------
        // Functional Methods

        public void EnableAllProcesses()
        {
            enabledProcesses.Clear();
            foreach (ProcessDef processDef in Props.processes)
            {
                ProcessFilter processFilter = new ProcessFilter(processDef.ingredientFilter.AllowedThingDefs.ToList());
                enabledProcesses.Add(processDef, processFilter);
            }
        }
        public void ToggleProcess(ProcessDef processDef, bool on)
        {
            if (on && !enabledProcesses.ContainsKey(processDef))
            {
                ProcessFilter processFilter = new ProcessFilter(processDef.ingredientFilter.AllowedThingDefs.ToList());
                enabledProcesses.Add(processDef, processFilter);
            }
            else if (!on && enabledProcesses.ContainsKey(processDef))
            {
                enabledProcesses.Remove(processDef);
            }
        }
        public void ToggleIngredient(ProcessDef processDef, ThingDef ingredient, bool on)
        {
            if (on)
            {
                if (enabledProcesses.ContainsKey(processDef))
                {
                    enabledProcesses[processDef].allowedIngredients.Add(ingredient);
                }
                else
                {
                    enabledProcesses[processDef] = new ProcessFilter(new List<ThingDef> { ingredient });
                }
            }
            else if (!on && enabledProcesses.ContainsKey(processDef) && enabledProcesses[processDef].allowedIngredients.Contains(ingredient))
            {
                if (enabledProcesses[processDef].allowedIngredients.Count == 1)
                {
                    enabledProcesses.Remove(processDef);
                }
                else
                {
                    enabledProcesses[processDef].allowedIngredients.Remove(ingredient);
                }
            }
        }

        public int SpaceLeftFor(ProcessDef processDef)
        {
            if (!activeProcesses.NullOrEmpty() && !Props.parallelProcesses && processDef != activeProcesses.First().processDef)
            {
                return 0;
            }
            return Mathf.FloorToInt((Props.capacity - activeProcesses.Sum(x => x.ingredientCount * x.processDef.capacityFactor)) / processDef.capacityFactor);
        }

        public void DoTicks(int ticks)
        {
            if (!Empty && FlickedOn)
            {
                foreach (ActiveProcess activeProcess in activeProcesses)
                {
                    activeProcess.DoTicks(ticks);
                }
                ConsumeFuel(ticks);
            }
            //Log.Message("Space: " + SpaceLeft + " | unreservedSpaceLeft: " + unreservedSpaceLeft);
        }
        public void AdjustPowerConsumption()
        {
            if (powerTradeComp != null)
            {
                powerTradeComp.PowerOutput = -powerTradeComp.Props.basePowerConsumption * PowerConsumptionRate;
            }        
        }
        public void ConsumeFuel(int ticks)
        {
            if (refuelComp == null) return;
            if (parent.def.tickerType == TickerType.Normal && !refuelComp.Props.consumeFuelOnlyWhenUsed) return; //in this case the fuel comp will handle the consumption
            if (!Fueled || !FlickedOn) return;
            if (refuelComp.Props.consumeFuelOnlyWhenUsed && Empty) return;
            if (refuelComp.Props.consumeFuelOnlyWhenPowered && !Powered) return;
            refuelComp.ConsumeFuel(ticks * FuelConsumptionRate * refuelComp.Props.fuelConsumptionRate / GenDate.TicksPerDay);
        }

        //Updates speed factors
        public void DoActiveProcessesRareTicks()
        {
            foreach (ActiveProcess activeProcess in activeProcesses)
            {
                activeProcess.TickRare();
            }
        }

        public ActiveProcess FindActiveProcess(ProcessDef processDef)
        {
            foreach (ActiveProcess activeProcess in activeProcesses)
            {
                if (activeProcess.processDef == processDef)
                {
                    return activeProcess;
                }
            }
            return null;
        }

        public void AddIngredient(Thing ingredient, ProcessDef processDef)
        {
            int num = Mathf.Min(ingredient.stackCount, SpaceLeftFor(processDef));
            if (num < ingredient.stackCount)
            {
                GenDrop.TryDropSpawn(ingredient.SplitOff(ingredient.stackCount - num), parent.Position, parent.Map, ThingPlaceMode.Near, out _);
            }
            bool emptyBefore = Empty;
            if (num > 0 && processDef != null)
            {
                if (!Props.independentProcesses && FindActiveProcess(processDef) is ActiveProcess existingProcess)
                {
                    TryMergeProcess(ingredient, existingProcess);
                }
                else
                {
                    TryAddNewProcess(ingredient, processDef);
                }
                if (emptyBefore && !Empty)
                {
                    GraphicChange(false);
                }
            }
        }
        private void TryAddNewProcess(Thing ingredient, ProcessDef processDef)
        {
            activeProcesses.Add(new ActiveProcess(this)
            {
                processDef = processDef,
                ingredientCount = ingredient.stackCount,
                ingredientThings = new List<Thing> { ingredient },
                targetQuality = cachedTargetQualities.ContainsKey(processDef) ? cachedTargetQualities[processDef] : (QualityCategory)PF_Settings.defaultTargetQualityInt
            });
            innerContainer.TryAddOrTransfer(ingredient, false);
            
        }
        private void TryMergeProcess(Thing ingredient, ActiveProcess activeProcess)
        {
            activeProcess.MergeProcess(ingredient);
        }


        public Thing TakeOutProduct(ActiveProcess activeProcess)
        {
            Thing thing = null;
            if (!activeProcess.Ruined)
            {
                thing = ThingMaker.MakeThing(activeProcess.processDef.thingDef, null);
                thing.stackCount = GenMath.RoundRandom(activeProcess.ingredientCount * activeProcess.processDef.efficiency);

                //Ingredient transfer
                CompIngredients compIngredients = thing.TryGetComp<CompIngredients>();
                List<ThingDef> ingredientList = new List<ThingDef>();
                foreach (Thing ingredientThing in activeProcess.ingredientThings)
                {
                    List<ThingDef> innerIngredients = ingredientThing.TryGetComp<CompIngredients>()?.ingredients;
                    if (!innerIngredients.NullOrEmpty())
                    {
                        ingredientList.AddRange(innerIngredients);
                    }
                }
                if (compIngredients != null && !ingredientList.NullOrEmpty())
                {
                    compIngredients.ingredients.AddRange(ingredientList);
                }

                //Quality
                if (activeProcess.processDef.usesQuality)
                {
                    CompQuality compQuality = thing.TryGetComp<CompQuality>();
                    if (compQuality != null)
                    {
                        compQuality.SetQuality(activeProcess.CurrentQuality, ArtGenerationContext.Colony);
                    }
                }
                //Bonus Outputs
                foreach (BonusOutput bonusOutput in activeProcess.processDef.bonusOutputs)
                {
                    if (Rand.Chance(bonusOutput.chance))
                    {
                        int amount = GenMath.RoundRandom(activeProcess.ingredientCount * activeProcess.processDef.capacityFactor / Props.capacity * bonusOutput.amount);
                        if (amount > 0)
                        {
                            if (bonusOutput.thingDef.race != null)
                            {
                                for (int i = 0; i < amount; i++)
                                {
                                    PawnGenerationRequest request = new PawnGenerationRequest(bonusOutput.thingDef.race.AnyPawnKind, null, PawnGenerationContext.NonPlayer, -1, false, true, false, false, true, false, 1f, false, true, true, true, false, false, false, false, 0f, 0f, null, 1f, null, null, null, null, null, null, null, null, null, null, null, null, null, false, false, false);
                                    Pawn productPawn = PawnGenerator.GeneratePawn(request);
                                    GenSpawn.Spawn(productPawn, parent.Position, parent.Map);
                                }
                            }
                            else
                            {
                                Thing bonusThing = ThingMaker.MakeThing(bonusOutput.thingDef, null);
                                bonusThing.stackCount = amount;
                                GenPlace.TryPlaceThing(bonusThing, parent.Position, parent.Map, ThingPlaceMode.Near);
                            }
                        }
                    }
                }

            }
            //Remove ingredients and active process
            foreach (Thing ingredient in activeProcess.ingredientThings)
            {
                innerContainer.Remove(ingredient);
                ingredient.Destroy();
            }
            activeProcesses.Remove(activeProcess);

            //Destroy chance
            if (Rand.Chance(activeProcess.processDef.destroyChance * activeProcess.ingredientCount * activeProcess.processDef.capacityFactor / Props.capacity))
            {
                if (PF_Settings.replaceDestroyedProcessors)
                {
                    GenConstruct.PlaceBlueprintForBuild_NewTemp(parent.def, parent.Position, parent.Map, parent.Rotation, Faction.OfPlayer, null, null, null);
                }
                parent.Destroy(DestroyMode.Vanish);
                return thing;
            }
            if (Empty)
            {
                GraphicChange(true);
            }
            if (!activeProcesses.Any(x => x.processDef.usesQuality))
            {
                emptyNow = false;
            }
            return thing;
        }

        public void GraphicChange(bool toEmpty)
        {
            if (parent is Pawn) return;
            string texPath = parent.def.graphicData.texPath;
            if (!toEmpty)
            {
                texPath += activeProcesses.MaxByWithFallback(x => x.ingredientCount)?.processDef?.filledGraphicSuffix ?? "";
            }
            Static_TexReloader.Reload(parent, texPath);
        }

        public override string CompInspectStringExtra()
        {
            // Perf: Only recalculate this inspect string periodically
            if (activeProcesses.Count == 0)
                return "PF_NoIngredient".TranslateSimple();

            StringBuilder str = new StringBuilder();

            // Line 1. Show the current number of items in the fermenter
            ProcessDef singleDef = Props.parallelProcesses ? null : activeProcesses[0].processDef;
            if (singleDef != null)
            {
                if (activeProcesses.Count == 1 && singleDef.usesQuality && activeProcesses[0].ActiveProcessDays >= singleDef.qualityDays.awful)
                {
                    ActiveProcess progress = activeProcesses[0];
                    str.AppendTagged("PF_ContainsProduct".Translate(TotalIngredientCount, Props.capacity, singleDef.thingDef.Named("PRODUCT"), progress.CurrentQuality.GetLabel().ToLower().Named("QUALITY")));
                }
                /*else if (!activeProcesses.First().ingredientThings.NullOrEmpty())
                {
                    // Usually this will only be one def label shown
                    string ingredientLabels = activeProcesses.First().ingredientThings.Distinct().Select(x => x.LabelNoCount).Join();
                    str.AppendTagged("PF_ContainsIngredient".Translate(TotalIngredientCount, Props.capacity, ingredientLabels.Named("INGREDIENTS")));
                }*/
                else
                {
                    str.AppendTagged("PF_ContainsIngredientsGeneric".Translate(TotalIngredientCount, Props.capacity));
                }
            }
            else
            {
                str.AppendTagged("PF_ContainsIngredientsGeneric".Translate(TotalIngredientCount, Props.capacity));
            }

            str.AppendLine();

            // Line 2. Show how many processes are running, or the current status of the process
            if (singleDef == null || (Props.independentProcesses && !Props.parallelProcesses))
            {
                int running = activeProcesses.Count;
                str.AppendTagged("PF_NumProcessing".Translate(running, running == 1
                    ? "PF_RunningStacksNoun".Translate().Named("STACKS")
                    : Find.ActiveLanguageWorker.Pluralize("PF_RunningStacksNoun".Translate(), running).Named("STACKS")));

                int slow = activeProcesses.Count(p => p.SpeedFactor < 0.75f);
                if (slow > 0)
                    str.AppendTagged("PF_RunningCountSlow".Translate(slow));

                int finished = activeProcesses.Count(p => p.Complete);
                if (finished > 0)
                    str.AppendTagged("PF_RunningCountFinished".Translate(finished));

                int ruined = activeProcesses.Count(p => p.Ruined);
                if (ruined > 0)
                    str.AppendTagged("PF_RunningCountRuined".Translate(ruined));
            }
            else
            {
                if (activeProcesses[0].Complete)
                    str.AppendTagged("PF_Finished".Translate());
                else if (activeProcesses[0].Ruined)
                    str.AppendTagged("PF_Ruined".Translate());
                else if (activeProcesses[0].SpeedFactor < 0.75f)
                    str.AppendTagged("PF_RunningSlow".Translate(activeProcesses[0].SpeedFactor.ToStringPercent(), activeProcesses[0].ActiveProcessPercent));
                else
                    str.AppendTagged("PF_RunningInfo".Translate(activeProcesses[0].ActiveProcessPercent.ToStringPercent()));
            }

            str.AppendLine();

            if (activeProcesses.Any(p => p.processDef.usesTemperature))
            {
                // Line 3. Show the ambient temperature, and if overheating/freezing
                float ambientTemperature = parent.AmbientTemperature;
                str.AppendFormat("{0}: {1}", "Temperature".TranslateSimple(), ambientTemperature.ToStringTemperature("F0"));

                if (singleDef != null)
                {
                    if (singleDef.temperatureSafe.Includes(ambientTemperature))
                    {
                        str.AppendFormat(" ({0})", singleDef.temperatureIdeal.Includes(ambientTemperature) ? "PF_Ideal".TranslateSimple() : "PF_Safe".TranslateSimple());
                    }
                    else if (!Empty)
                    {
                        bool overheating = ambientTemperature < singleDef.temperatureSafe.TrueMin;
                        str.AppendFormat(" ({0}{1})".Colorize(overheating ? Color.red : Color.blue),
                            overheating ? "Freezing".TranslateSimple() : "Overheating".TranslateSimple(),
                            activeProcesses.Count == 1 && !Props.independentProcesses ? $" {activeProcesses[0].ruinedPercent.ToStringPercent()}" : "");
                    }
                }
                else if (activeProcesses.Count > 0)
                {
                    bool abort = false;
                    foreach (ActiveProcess progress in activeProcesses)
                    {
                        if (ambientTemperature > progress.processDef.temperatureSafe.TrueMax)
                        {
                            str.AppendFormat(" ({0})", "Freezing".TranslateSimple());
                            abort = true;
                            break;
                        }

                        if (ambientTemperature < progress.processDef.temperatureSafe.TrueMin)
                        {
                            str.AppendFormat(" ({0})", "Overheating".TranslateSimple());
                            abort = true;
                            break;
                        }
                    }

                    if (!abort)
                    {
                        foreach (ActiveProcess progress in activeProcesses)
                        {
                            if (progress.processDef.temperatureIdeal.Includes(ambientTemperature))
                            {
                                str.AppendFormat(" ({0})", "PF_Safe".TranslateSimple());
                                abort = true;
                                break;
                            }
                        }
                    }

                    if (!abort)
                    {
                        str.AppendFormat(" ({0})", "PF_Ideal".TranslateSimple());
                    }
                }

                str.AppendLine();

                // Line 4. Ideal temp range
                if (singleDef != null && singleDef.usesTemperature)
                {
                    str.AppendFormat("{0}: {1}~{2} ({3}~{4})", "PF_IdealSafeProductionTemperature".TranslateSimple(),
                        singleDef.temperatureIdeal.min.ToStringTemperature("F0"),
                        singleDef.temperatureIdeal.max.ToStringTemperature("F0"),
                        singleDef.temperatureSafe.min.ToStringTemperature("F0"),
                        singleDef.temperatureSafe.max.ToStringTemperature("F0"));
                }
            }

            return str.ToString().TrimEndNewlines();
        }
    }
}
