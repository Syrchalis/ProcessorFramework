using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

namespace ProcessorFramework
{
    [HotSwappable]
    public class WorkGiver_EmptyProcessor : WorkGiver_Scanner
    {
        public override PathEndMode PathEndMode => PathEndMode.Touch;

        public override bool ShouldSkip(Pawn pawn, bool forced = false)
        {
            return !pawn.Map.GetComponent<MapComponent_Processors>().thingsWithProcessorComp.Any();
        }

        public override IEnumerable<Thing> PotentialWorkThingsGlobal(Pawn pawn)
        {
            return pawn.Map.GetComponent<MapComponent_Processors>().thingsWithProcessorComp;
        }

        public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            CompProcessor comp = t.TryGetComp<CompProcessor>();
            return comp != null && (comp.AnyComplete || comp.AnyRuined) && !t.IsBurning() && !t.IsForbidden(pawn)
                && pawn.CanReserveAndReach(t, PathEndMode.Touch, pawn.NormalMaxDanger(), comp.activeProcesses.Count(x => x.Complete), -1, DefOf.PF_Empty, forced);
        }

        public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            return new Job(DefOf.EmptyProcessor, t);
        }
    }
}