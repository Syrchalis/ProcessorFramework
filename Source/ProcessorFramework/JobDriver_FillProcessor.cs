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
	public class JobDriver_FillProcessor : JobDriver
	{
		private const TargetIndex ProcessorInd = TargetIndex.A;
		private const TargetIndex IngredientInd = TargetIndex.B;
		private const int Duration = 200;
        private CompProcessor comp = null;

		protected Thing Processor => job.GetTarget(TargetIndex.A).Thing;

        protected Thing Ingredient => job.GetTarget(TargetIndex.B).Thing;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(Processor, job, 1, -1) && pawn.Reserve(Ingredient, job, 1, job.count, null, errorOnFailed);
		}

		protected override IEnumerable<Toil> MakeNewToils()
		{
            comp = Processor.TryGetComp<CompProcessor>();
            ProcessDef processDef = comp.enabledProcesses.FirstOrDefault(y => y.Value.allowedIngredients.Contains(Ingredient.def)).Key;
            if (processDef == null) Log.Error("Processor Framework: Unable to find enabled process that allows " + Ingredient.Label + " for " + Processor);
            float capacityFactor = processDef.capacityFactor;

            this.FailOnDespawnedNullOrForbidden(ProcessorInd);
			this.FailOnBurningImmobile(ProcessorInd);
            AddEndCondition(delegate
            {
                if (comp.SpaceLeftFor(processDef) < 1 || !comp.enabledProcesses.TryGetValue(processDef, out ProcessFilter processFilter) || !processFilter.allowedIngredients.Contains(Ingredient.def))
                {
                    return JobCondition.Succeeded;
                }
                return JobCondition.Ongoing;
            });

            // Creating the toil before yielding allows for CheckForGetOpportunityDuplicate
            Toil reserveIngredient = Toils_Reserve.Reserve(IngredientInd, 1, job.count);
			yield return reserveIngredient;
            yield return Toils_Goto.GotoThing(IngredientInd, PathEndMode.ClosestTouch).FailOnDespawnedNullOrForbidden(IngredientInd).FailOnSomeonePhysicallyInteracting(IngredientInd);
			yield return Toils_Haul.StartCarryThing(IngredientInd, false, true, false).FailOnDestroyedNullOrForbidden(IngredientInd);
			yield return Toils_Haul.CheckForGetOpportunityDuplicate(reserveIngredient, IngredientInd, TargetIndex.None, false);
            yield return Toils_Goto.GotoThing(ProcessorInd, PathEndMode.ClosestTouch);
            yield return Toils_General.Wait(Duration, ProcessorInd).FailOnDestroyedNullOrForbidden(IngredientInd).FailOnDestroyedNullOrForbidden(ProcessorInd)
                .FailOnCannotTouch(ProcessorInd, PathEndMode.Touch).WithProgressBarToilDelay(ProcessorInd, false, -0.5f);

            // The Processor automatically destroys held ingredients
            Toil addIngredient = new Toil
            {
                initAction = () => comp.AddIngredient(Ingredient, processDef),
                defaultCompleteMode = ToilCompleteMode.Instant,
            };
            yield return addIngredient;
		}
	}
}
