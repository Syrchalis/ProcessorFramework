using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ProcessorFramework
{
    [HotSwappable]
    public class WorkGiver_FillProcessor : WorkGiver_Scanner
    {
        public override PathEndMode PathEndMode => PathEndMode.Touch;

        public override IEnumerable<Thing> PotentialWorkThingsGlobal(Pawn pawn)
        {
            return pawn.Map.GetComponent<MapComponent_Processors>().thingsWithProcessorComp;
        }

        public override bool Prioritized => true;
        public override bool ShouldSkip(Pawn pawn, bool forced = false)
        {
            return !pawn.Map.GetComponent<MapComponent_Processors>().thingsWithProcessorComp.Any();
        }
        public override float GetPriority(Pawn pawn, TargetInfo t)
        {
            CompProcessor comp = t.Thing.TryGetComp<CompProcessor>();
            if (comp != null)
            {
                return 1f / comp.SpaceLeft;
            }
            return 0f;
        }

        public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            CompProcessor comp = t.TryGetComp<CompProcessor>();

            if (comp == null || comp.enabledProcesses.EnumerableNullOrEmpty()) return false;

            ProcessDef smallestProcess = comp.Props.parallelProcesses || comp.activeProcesses.NullOrEmpty() ? comp.enabledProcesses.Keys.MinBy(x => x.capacityFactor) : comp.activeProcesses.First().processDef; 
            //process with smallest capacity factor, if not empty and no parallel processes the current active process is taken instead
            if (comp.SpaceLeftFor(smallestProcess) < 1) return false; //check if enough space for one ingredient for smallest process

            if (!comp.TemperatureOk)
            {
                JobFailReason.Is("BadTemperature".Translate().ToLower());
                return false;
            }

            if (pawn.Map.designationManager.DesignationOn(t, DesignationDefOf.Deconstruct) != null
                || t.IsForbidden(pawn)
                || !pawn.CanReserveAndReach(t, PathEndMode.Touch, pawn.NormalMaxDanger(), 10, 0, null, forced)
                || t.IsBurning())
            {
                return false;
            }
            if (FindIngredient(pawn, comp) == null)
            {
                JobFailReason.Is("PF_NoIngredient".Translate());
                return false;
            }
            return true;
        }


        public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            CompProcessor comp = t.TryGetComp<CompProcessor>();
            Thing ingredient = FindIngredient(pawn, comp);
            Job job = new Job(DefOf.FillProcessor, t, ingredient)
            {
                count = Mathf.Min(comp.SpaceLeftFor(comp.enabledProcesses.FirstOrDefault(y => y.Value.allowedIngredients.Contains(ingredient.def)).Key), ingredient.stackCount, pawn.carryTracker.AvailableStackSpace(ingredient.def))
            };
            return job;
        }

        private Thing FindIngredient(Pawn pawn, CompProcessor comp)
        {
            //Needs to check that space left is enough to accomodate one ingredient before sending to JobDriver
            bool validator(Thing x)
            {
                if (x.IsForbidden(pawn)) return false;

                ProcessDef processDef = comp.enabledProcesses.FirstOrDefault(y => y.Value.allowedIngredients.Contains(x.def)).Key;
                if (processDef == null) return false;

                if (!pawn.CanReserve(x, 1, Mathf.Min(comp.SpaceLeftFor(processDef), x.stackCount, pawn.carryTracker.AvailableStackSpace(x.def))) 
                    || comp.SpaceLeftFor(processDef) < 1)
                {
                    return false;
                }
                return true;
            }
            return GenClosest.ClosestThingReachable(pawn.Position, pawn.Map, ThingRequest.ForGroup(ThingRequestGroup.HaulableEver), PathEndMode.ClosestTouch, TraverseParms.For(pawn), 9999f, validator);
        }
    }
}