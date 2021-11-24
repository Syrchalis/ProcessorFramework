using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

namespace ProcessorFramework
{
	[HotSwappable]
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
			return pawn.Reserve(Processor, job, Processor.TryGetComp<CompProcessor>().activeProcesses.Count(x => x.Complete || x.Ruined), 0, DefOf.PF_Empty, errorOnFailed);
		}

		protected override IEnumerable<Toil> MakeNewToils()
		{
			CompProcessor comp = Processor.TryGetComp<CompProcessor>();
			// Verify fermenter validity
			this.FailOn(() => (!comp.AnyComplete && !comp.AnyRuined) || comp.Empty);
			this.FailOnDestroyedNullOrForbidden(ProcessorInd);
			AddEndCondition(delegate
			{
				if (comp.Empty)
				{
					return JobCondition.Succeeded;
				}
				return JobCondition.Ongoing;
			});
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
					if (activeProcess == null)
                    {
						EndJobWith(JobCondition.Incompletable);
						return;
					}
					Thing product = comp.TakeOutProduct(activeProcess);
					if (product == null || product.stackCount == 0)
                    {
						EndJobWith(JobCondition.Succeeded);
						return;
                    }
					//This is very stupid, but you can produce pawns like muffalos as product and it works
					if (product.def.race != null)
                    {
						for (int i = 0; i < product.stackCount; i++)
                        {
							PawnGenerationRequest request = new PawnGenerationRequest(product.def.race.AnyPawnKind, Faction.OfPlayerSilentFail, PawnGenerationContext.NonPlayer, -1, false, true, false, false, true, false, 1f, false, true, true, true, false, false, false, false, 0f, 0f, null, 1f, null, null, null, null, null, null, null, null, null, null, null, null, null, false, false, false);
							Pawn productPawn = PawnGenerator.GeneratePawn(request);
							GenSpawn.Spawn(productPawn, pawn.Position, Map);
						}
						EndJobWith(JobCondition.Succeeded);
                    }
					else
                    {
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
							EndJobWith(JobCondition.Succeeded);
						}
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
