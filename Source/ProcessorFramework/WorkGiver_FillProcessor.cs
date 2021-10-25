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
                return 1f / comp.unreservedSpaceLeft;
            }
            return 0f;
        }

        public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            CompProcessor comp = t.TryGetComp<CompProcessor>();

            if (comp == null) return false;
            if (!comp.EnabledProcesses.EnumerableNullOrEmpty())
            {
                float minFactor = comp.EnabledProcesses.MinBy(x => x.capacityFactor).capacityFactor;
                if (Mathf.Min(comp.unreservedSpaceLeft, comp.SpaceLeft) < minFactor)
                {
                    return false;
                }
            }
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
            if (comp.queuedIngredient == null) comp.queuedIngredient = ingredient.def;
            Job job = new Job(DefOf.FillProcessor, t, ingredient)
            {
                count = Mathf.Min(Mathf.FloorToInt(comp.unreservedSpaceLeft / comp.Props.processes.Find(x => x.ingredientFilter.Allows(ingredient)).capacityFactor), pawn.carryTracker.AvailableStackSpace(ingredient.def))
            };
            return job;
        }

        private Thing FindIngredient(Pawn pawn, CompProcessor comp)
        {
            if (comp is null)
            {
                return null;
            }

            ThingFilter filter;
            if (comp.Props.parallelProcesses || (comp.Empty && comp.queuedIngredient == null))
            {
                filter = comp.ingredientFilter;
            }
            else if (!comp.activeProcesses.NullOrEmpty())
            {
                filter = comp.activeProcesses.First().processDef.ingredientFilter;
            }
            else
            {
                filter = comp.EnabledProcesses.First(x => x.ingredientFilter.Allows(comp.queuedIngredient)).ingredientFilter;
            }
            //Needs to check that there is enough ingredient for at least one product && needs to check that space left is enough to accomodate one ingredient before sending to JobDriver
            bool validator(Thing x)
            {
                if (x.IsForbidden(pawn) || !filter.Allows(x)) return false;
                ProcessDef processDef = comp.EnabledProcesses.First(y => y.ingredientFilter.Allows(x));
                if (!pawn.CanReserve(x, 10, Mathf.Min(Mathf.FloorToInt(comp.unreservedSpaceLeft / processDef.capacityFactor), x.stackCount)) || x.stackCount < 1f / processDef.efficiency || comp.unreservedSpaceLeft < processDef.capacityFactor)
                {
                    return false;
                }
                return true;
            }
            return GenClosest.ClosestThingReachable(pawn.Position, pawn.Map, filter.BestThingRequest, PathEndMode.ClosestTouch, TraverseParms.For(pawn), 9999f, validator);
        }
    }
}