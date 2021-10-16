using System.Collections.Generic;

using RimWorld;
using Verse;
using Verse.AI;

namespace ProcessorFramework
{

	public class JobDriver_EmptyProcessor : JobDriver
	{

		private const TargetIndex ProcessorInd = TargetIndex.A;
		private const TargetIndex ProductToHaulInd = TargetIndex.B;
		private const TargetIndex StorageCellInd = TargetIndex.C;
		private const int Duration = 200;

		protected Thing Processor => job.GetTarget(TargetIndex.A).Thing;

        protected Thing Product => job.GetTarget(TargetIndex.B).Thing;

		public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
			return pawn.Reserve(Processor, job, 1, -1, null);
		}

		protected override IEnumerable<Toil> MakeNewToils()
		{
			CompProcessor comp = Processor.TryGetComp<CompProcessor>();
			// Verify fermenter validity
			this.FailOn(() => !comp.AnyComplete || comp.Empty);
			this.FailOnDestroyedNullOrForbidden(ProcessorInd);

			// Reserve fermenter
			yield return Toils_Reserve.Reserve(ProcessorInd);

			// Go to the fermenter
			yield return Toils_Goto.GotoThing(ProcessorInd, PathEndMode.ClosestTouch);

			// Add delay for collecting product from fermenter, if it is ready
			yield return Toils_General.Wait(Duration).FailOnDestroyedNullOrForbidden(ProcessorInd).WithProgressBarToilDelay(ProcessorInd);

            // Collect product
            Toil collect = new Toil
            {
                initAction = () =>
                {
					ActiveProcess activeProcess = comp.activeProcesses.Find(x => x.Complete || x.Ruined);
					Thing product = comp.TakeOutProduct(activeProcess);

					if (product == null)
                    {
						EndJobWith(JobCondition.Succeeded);
						return;
                    }

                    GenPlace.TryPlaceThing(product, pawn.Position, Map, ThingPlaceMode.Near);
                    StoragePriority storagePriority = StoreUtility.CurrentStoragePriorityOf(product);

                // Try to find a suitable storage spot for the product
                if (StoreUtility.TryFindBestBetterStoreCellFor(product, pawn, Map, storagePriority, pawn.Faction, out IntVec3 c))
                    {
                        job.SetTarget(ProductToHaulInd, product);
                        job.count = product.stackCount;
                        job.SetTarget(StorageCellInd, c);
                    }
                // If there is no spot to store the product, end this job
                else
                    {
                        EndJobWith(JobCondition.Incompletable);
                    }
                },
                defaultCompleteMode = ToilCompleteMode.Instant
            };
            yield return collect;

			// Reserve the product
			yield return Toils_Reserve.Reserve(ProductToHaulInd);

			// Reserve the storage cell
			yield return Toils_Reserve.Reserve(StorageCellInd);

			// Go to the product
			yield return Toils_Goto.GotoThing(ProductToHaulInd, PathEndMode.ClosestTouch);

			// Pick up the product
			yield return Toils_Haul.StartCarryThing(ProductToHaulInd);

			// Carry the product to the storage cell, then place it down
			Toil carry = Toils_Haul.CarryHauledThingToCell(StorageCellInd);
			yield return carry;
			yield return Toils_Haul.PlaceHauledThingInCell(StorageCellInd, carry, true);
		}
	}
}
