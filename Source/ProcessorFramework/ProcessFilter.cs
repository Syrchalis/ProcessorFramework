using System.Collections.Generic;
using System.Text;
using System.Linq;
using UnityEngine;
using RimWorld;
using Verse;
using HarmonyLib;

namespace ProcessorFramework
{
    //for now this is just a list of allowed ingredients, but might become more
    public class ProcessFilter : IExposable
    {
        public List<ThingDef> allowedIngredients;

        public ProcessFilter()
        {
        }

        public ProcessFilter(List<ThingDef> ingredients)
        {
            allowedIngredients = ingredients;
        }

        public void ExposeData()
        {
            Scribe_Collections.Look(ref allowedIngredients, "PF_allowedIngredients", LookMode.Def);
        }
    }
}
