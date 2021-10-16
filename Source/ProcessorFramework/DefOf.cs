using System.Collections.Generic;
using System.Text;
using System.Linq;
using UnityEngine;
using RimWorld;
using Verse;

namespace ProcessorFramework
{
    [RimWorld.DefOf]
    public static class DefOf
    {
        static DefOf()
        {
        }
        public static JobDef FillProcessor;
        public static JobDef EmptyProcessor;

        public static SoundDef PF_Honk;

        public static ThingDef BarrelProcessor;
    }
}
