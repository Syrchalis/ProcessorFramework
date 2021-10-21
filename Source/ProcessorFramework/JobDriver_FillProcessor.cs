using System;
using System.Collections.Generic;

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
        private bool succeeded = false;
        private int stackCount;
        private int carriedByPawn;

		protected Thing Processor => job.GetTarget(TargetIndex.A).Thing;

        protected Thing Ingredient => job.GetTarget(TargetIndex.B).Thing;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(Processor, job, 10, 0) && pawn.Reserve(Ingredient, job, 10, Mathf.Min(job.count, Ingredient.stackCount), null, errorOnFailed);
		}

		protected override IEnumerable<Toil> MakeNewToils()
		{
            comp = Processor.TryGetComp<CompProcessor>();
            float capacityFactor = comp.Props.processes.Find(x => x.ingredientFilter.Allows(Ingredient)).capacityFactor;
            stackCount = Mathf.Min(Mathf.FloorToInt(comp.unreservedSpaceLeft / capacityFactor), pawn.carryTracker.AvailableStackSpace(Ingredient.def)); //Get optimal amount to carry
            job.count = stackCount;
            comp.unreservedSpaceLeft -= Mathf.CeilToInt(stackCount * capacityFactor); //Reserve space based on optimal carry amount
            
            this.FailOnDespawnedNullOrForbidden(ProcessorInd);
			this.FailOnBurningImmobile(ProcessorInd);
            AddEndCondition(delegate
            {
                if (comp.SpaceLeft < capacityFactor || !comp.ingredientFilter.Allows(Ingredient) || stackCount <= 0)
                {
                    return JobCondition.Succeeded;
                }
                return JobCondition.Ongoing;
            });

            // Creating the toil before yielding allows for CheckForGetOpportunityDuplicate
            Toil reserveIngredient = Toils_Reserve.Reserve(IngredientInd, 1, -1/*Mathf.Min(job.count, Ingredient.stackCount)*/);
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
                initAction = () => comp.AddIngredient(Ingredient),
                defaultCompleteMode = ToilCompleteMode.Instant,
            };
            addIngredient.AddPreInitAction(delegate
            {
                carriedByPawn = pawn.carryTracker.CarriedThing.stackCount; //Get what pawn actually carries
            });
            addIngredient.AddFinishAction(delegate
            {
                succeeded = true; //Set flag that job succeeded and ingredients were in fact put into processor
                comp.unreservedSpaceLeft += Mathf.CeilToInt((stackCount - carriedByPawn) * capacityFactor); //Release space that wasn't actually used in the end
            });
            yield return addIngredient;
            AddFinishAction(delegate
            {
                if (!succeeded)
                {
                    comp.unreservedSpaceLeft += Mathf.CeilToInt(stackCount * capacityFactor);
                }
            });
		}
	}
}
