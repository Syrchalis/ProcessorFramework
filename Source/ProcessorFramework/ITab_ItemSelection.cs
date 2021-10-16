using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace ProcessorFramework
{
    [HotSwappable]
    public class ITab_ItemSelection : ITab
    {
        private Vector2 scrollPosition;
        private Dictionary<ProcessDef, bool> categoryOpen = new Dictionary<ProcessDef, bool>();
        private ThingFilter localProductFilter = null;
        private ThingFilter localIngredientFilter = null;
        private IEnumerable<CompProcessor> processors;
        private Thing cachedThing;
        private bool callbackActive = false;
        //The cachedThing is set at the end of FillTab and compared at the start, allowing the method to detect when the basis for the filters changed, thus resetting the filters

        public ITab_ItemSelection()
        {
            labelKey = "PF_ITab_ItemSelection";
            size = new Vector2(300, 400);
        }

        public override bool IsVisible => Find.Selector.SelectedObjects.All(x => x is Thing thing && thing.Faction == Faction.OfPlayerSilentFail && thing.TryGetComp<CompProcessor>() != null);

        protected override void FillTab()
        {
            processors = Find.Selector.SelectedObjects.Select(o => (o as ThingWithComps)?.TryGetComp<CompProcessor>());
            if (cachedThing != Find.Selector.SelectedObjects.First())
            {
                //Log.Message("CachedThing changed");
                localProductFilter = null;
                localIngredientFilter = null;
            }
            if (localProductFilter == null && localIngredientFilter == null)
            {
                localProductFilter = new ThingFilter(ProductFilterCallback);
                localProductFilter.CopyAllowancesFrom(processors.First().productFilter);
                localIngredientFilter = new ThingFilter(IngredientFilterCallback);
                localIngredientFilter.CopyAllowancesFrom(processors.First().ingredientFilter);
            }
            if (processors.EnumerableNullOrEmpty())
            {
                return;
            }
            Rect outRect = new Rect(default, size).ContractedBy(12f);
            outRect.yMin += 24;
            outRect.height -= 24;
            Rect viewRect = new Rect(0f, 0f, outRect.width + 8, outRect.height + 8);
            Widgets.DrawMenuSection(outRect);
            Rect buttonRect = new Rect(outRect.x + 1f, outRect.y + 1f, (outRect.width - 2f) / 2f, 24f);
            Text.Font = GameFont.Small;
            if (Widgets.ButtonText(buttonRect, "ClearAll".Translate(), true, true, true))
            {
                foreach (CompProcessor processor in processors)
                {
                    localProductFilter.SetDisallowAll();
                }
                SoundDefOf.Checkbox_TurnedOff.PlayOneShotOnCamera(null);
            }
            if (Widgets.ButtonText(new Rect(buttonRect.xMax + 1f, buttonRect.y, outRect.xMax - 1f - (buttonRect.xMax + 1f), 24f), "AllowAll".Translate(), true, true, true))
            {
                foreach (CompProcessor processor in processors)
                {
                    foreach (ProcessDef processDef in processor.Props.processes)
                    {
                        localProductFilter.SetAllow(processDef.thingDef, true);
                    }
                }
                SoundDefOf.Checkbox_TurnedOn.PlayOneShotOnCamera(null);
            }
            outRect.yMin += 30;
            outRect.xMax -= 4;
            outRect.height -= 12;
            Rect listRect = new Rect(0f, 2f, viewRect.width, 9999f);
            GUI.BeginGroup(viewRect);
            GUI.color = Widgets.MenuSectionBGFillColor;
            Text.Anchor = TextAnchor.UpperLeft;
            Widgets.BeginScrollView(outRect, ref scrollPosition, viewRect);
            foreach (ProcessDef processDef in processors.First().Props.processes)
            {
                if (!categoryOpen.ContainsKey(processDef))
                {
                    categoryOpen.Add(processDef, false);
                }
                DoItemsList(ref listRect, processors, processDef);
            }
            Widgets.EndScrollView();
            GUI.EndGroup();
            if (Widgets.ButtonText(new Rect(outRect.xMin + 24, outRect.yMax + 16, outRect.width - 48, 24f), "PF_ApplySettings".Translate()))
            {
                foreach (CompProcessor processor in processors)
                {
                    processor.productFilter.CopyAllowancesFrom(localProductFilter);
                    processor.ingredientFilter.CopyAllowancesFrom(localIngredientFilter);
                }
                SoundDefOf.Click.PlayOneShotOnCamera(null);
            }
            cachedThing = Find.Selector.SelectedObjects.First() as Thing;
        }
        public void DoItemsList(ref Rect listRect, IEnumerable<CompProcessor> processors, ProcessDef processDef)
        {
            bool productAllowed = localProductFilter.Allows(processDef.thingDef);
            bool open = categoryOpen[processDef];
            
            Rect headerRect = listRect.TopPartPixels(24);
            Rect expandRect = headerRect.LeftPartPixels(20);
            headerRect.xMin += 24;
            Rect checkboxRect = headerRect.RightPartPixels(56);
            Texture2D tex = open ? TexButton.Collapse : TexButton.Reveal;
            if (Widgets.ButtonImage(expandRect, tex, true))
            {
                if (open) SoundDefOf.TabClose.PlayOneShotOnCamera(null);
                else SoundDefOf.TabOpen.PlayOneShotOnCamera(null);
                categoryOpen[processDef] = !open;
            }

            Widgets.Label(headerRect, processDef.thingDef.LabelCap);
            Widgets.Checkbox(new Vector2(checkboxRect.xMin, checkboxRect.yMin), ref productAllowed);
            localProductFilter.SetAllow(processDef.thingDef, productAllowed);

            if (open)
            {
                headerRect.xMin += 12;
                foreach (ThingDef ingredient in processDef.ingredientFilter.AllowedThingDefs)
                {
                    checkboxRect.y += 22;
                    headerRect.y += 22;
                    bool ingredientAllowed = localIngredientFilter.Allows(ingredient);
                    Widgets.Label(headerRect, ingredient.LabelCap);
                    Widgets.Checkbox(new Vector2(checkboxRect.xMin, checkboxRect.yMin), ref ingredientAllowed);
                    localIngredientFilter.SetAllow(ingredient, ingredientAllowed);
                    listRect.y += 22;
                }
            }
            listRect.y += 28;
        }
        public void ProductFilterCallback()
        {
            if (callbackActive || localIngredientFilter == null)
            {
                return;
            }
            callbackActive = true;
            localIngredientFilter.SetDisallowAll();
            foreach (ProcessDef processDef in EnabledProcesses)
            {
                foreach (ThingDef thingDef in processDef.ingredientFilter.AllowedThingDefs)
                {
                    localIngredientFilter.SetAllow(thingDef, true);
                }
            }
            callbackActive = false;
        }
        public void IngredientFilterCallback()
        {
            if (callbackActive || localIngredientFilter == null || localProductFilter == null)
            {
                return;
            }
            callbackActive = true;
            foreach (ThingDef ingredient in localIngredientFilter.AllowedThingDefs)
            {
                ThingDef relatedProduct = processors.First().Props.processes.Find(x => x.ingredientFilter.Allows(ingredient)).thingDef;
                if (!localProductFilter.Allows(relatedProduct))
                {
                    localProductFilter.SetAllow(relatedProduct, true);
                }
            }
            List<ThingDef> productsToDisable = new List<ThingDef>();
            foreach (ProcessDef processDef in EnabledProcesses)
            {
                if (!localIngredientFilter.AllowedThingDefs.SharesElementWith(processDef.ingredientFilter.AllowedThingDefs))
                {
                    productsToDisable.Add(processDef.thingDef);
                }
            }
            foreach (ThingDef product in productsToDisable)
            {
                localProductFilter.SetAllow(product, false);
            }
            callbackActive = false;
        }
        public IEnumerable<ProcessDef> EnabledProcesses
        {
            get
            {
                if (!processors.EnumerableNullOrEmpty())
                {
                    foreach (ThingDef product in localProductFilter.AllowedThingDefs)
                    {
                        yield return processors.First().Props.processes.Find(x => x.thingDef == product);
                    }
                }
            }
        }
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
    public class HotSwappableAttribute : Attribute
    {
    }
}
