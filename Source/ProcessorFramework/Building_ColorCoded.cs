using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using RimWorld;
using Verse;

namespace ProcessorFramework
{
    public class Building_ColorCoded : Building
    {
        public override Color DrawColorTwo
        {
            get
            {
                CompProcessor comp = this.TryGetComp<CompProcessor>();
                if (comp != null && !comp.Props.parallelProcesses && comp.Props.colorCoded && !comp.activeProcesses.NullOrEmpty() && comp.activeProcesses.First().processDef.color != Color.white)
                {
                    return comp.activeProcesses.First().processDef.color;
                }
                return DrawColor;
            }
        }
    }
}
