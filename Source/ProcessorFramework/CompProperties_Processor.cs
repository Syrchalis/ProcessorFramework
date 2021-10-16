using Verse;
using System.Collections.Generic;
using UnityEngine;

namespace ProcessorFramework
{

	public class CompProperties_Processor : CompProperties
	{
        public bool showProductIcon = true;
        public Vector2 barOffset = new Vector2(0f, 0.25f);
        public Vector2 barScale = new Vector2(1f, 1f);
        public Vector2 productIconSize = new Vector2(1f, 1f);

		public bool independentProcesses = false;
		public bool parallelProcesses = false;
		
		public bool colorCoded = false;

		public int capacity = 25;

		public List<ProcessDef> processes = new List<ProcessDef>();

		public CompProperties_Processor()
		{
			compClass = typeof(CompProcessor);
		}

		public override void ResolveReferences(ThingDef parentDef)
		{
			base.ResolveReferences(parentDef);
			foreach (ProcessDef processDef in processes)
			{
				processDef.ResolveReferences();
			}
		}
	}
}
