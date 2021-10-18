using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

namespace ProcessorFramework
{
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

            if (comp == null) return false;
            if (!comp.EnabledProcesses.EnumerableNullOrEmpty())
            {
                float minFactor = comp.EnabledProcesses.MinBy(x => x.capacityFactor).capacityFactor;
                if (comp.SpaceLeft < minFactor)
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
                            || !pawn.CanReserveAndReach(t, PathEndMode.Touch, pawn.NormalMaxDanger(), 1, -1, null, forced)
                            || t.IsBurning())
            {
                return false;
            }

            if (FindIngredient(pawn, t) == null)
            {
                JobFailReason.Is("PF_NoIngredient".Translate());
                return false;
            }

            return true;
        }


        public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            Thing t2 = FindIngredient(pawn, t);
            return new Job(DefOf.FillProcessor, t, t2);
        }

        private static Thing FindIngredient(Pawn pawn, Thing processor)
        {
            CompProcessor comp = processor.TryGetComp<CompProcessor>();
            if (comp is null)
            {
                return null;
            }
            ThingFilter filter;
            if (comp.Props.parallelProcesses || comp.Empty)
            {
                filter = comp.ingredientFilter;
            }
            else
            {
                filter = comp.activeProcesses.First().processDef.ingredientFilter;
            }
            //Needs to check that there is enough ingredient for at least one product && needs to check that space left is enough to accomodate the product before sending to JobDriver
            Predicate<Thing> validator = x => !x.IsForbidden(pawn) && pawn.CanReserve(x) && filter.Allows(x) && x.stackCount >= 1f / comp.EnabledProcesses.First(y => y.ingredientFilter.Allows(x)).efficiency
            && comp.SpaceLeft >= comp.EnabledProcesses.First(y => y.ingredientFilter.Allows(x)).capacityFactor;
            return GenClosest.ClosestThingReachable(pawn.Position, pawn.Map, filter.BestThingRequest, PathEndMode.ClosestTouch, TraverseParms.For(pawn), 9999f, validator); ;
        }
    }
}