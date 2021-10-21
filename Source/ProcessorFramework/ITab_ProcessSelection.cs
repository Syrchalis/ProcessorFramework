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
    public class ITab_ProcessSelection : ITab
    {
        private Vector2 scrollPosition;

        private IEnumerable<CompProcessor> processors;
        private Dictionary<ProcessDef, bool> categoryOpen = new Dictionary<ProcessDef, bool>();
        private ThingFilter localProductFilter = null;
        private ThingFilter localIngredientFilter = null;
        
        private Thing cachedThing; //The cachedThing is set at the end of FillTab and compared at the start, allowing the method to detect when the selected object changed
        private bool callbackActive = false;

        private const int lineHeight = 22;

        public ITab_ProcessSelection()
        {
            labelKey = "PF_ITab_ItemSelection";
            size = new Vector2(300, 400);
        }

        public override bool IsVisible => Find.Selector.SelectedObjects.All(x => x is Thing thing && thing.Faction == Faction.OfPlayerSilentFail && thing.TryGetComp<CompProcessor>() != null);

        protected override void FillTab()
        {
            processors = Find.Selector.SelectedObjects.Select(o => (o as ThingWithComps)?.TryGetComp<CompProcessor>());
            List<ProcessDef> processDefs = processors.First().Props.processes;

            //Reset filters for the window every time a new object is selected so player can actually see how the object is configured
            if (cachedThing != Find.Selector.SelectedObjects.First())
            {
                localProductFilter = null;
                localIngredientFilter = null;
            }

            if (localProductFilter == null && localIngredientFilter == null)
            {
                ResetFilters();
            }

            if (processors.EnumerableNullOrEmpty())
            {
                return;
            }

            Rect outRect = new Rect(default, size).ContractedBy(12f);
            outRect.yMin += 24; //top space
            outRect.height -= 24; //height adjust
            int viewRectHeight = processDefs.Count * lineHeight + 80; //increase scroll area for each product listed and extra space at the end
            foreach (KeyValuePair<ProcessDef, bool> keyValuePair in categoryOpen)
            {
                //adjust scroll area for each node opened
                viewRectHeight += processDefs.Contains(keyValuePair.Key) && keyValuePair.Value ? keyValuePair.Key.ingredientFilter.AllowedDefCount * 24 : 0;
            }
            Rect viewRect = new Rect(0f, 0f, outRect.width - GUI.skin.verticalScrollbar.fixedWidth - 1f, viewRectHeight);
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
                foreach (ProcessDef processDef in processors.SelectMany(x => x.Props.processes))
                {
                    localProductFilter.SetAllow(processDef.thingDef, true);
                }
                SoundDefOf.Checkbox_TurnedOn.PlayOneShotOnCamera(null);
            }
            outRect.yMin += buttonRect.height + 6;
            Rect listRect = new Rect(0f, 2f, 280, 9999f);
            Widgets.BeginScrollView(outRect, ref scrollPosition, viewRect);
            foreach (ProcessDef processDef in processDefs)
            {
                if (!categoryOpen.ContainsKey(processDef))
                {
                    categoryOpen.Add(processDef, false);
                }
                DoItemsList(ref listRect, processors, processDef);
            }
            Widgets.EndScrollView();
            if (Widgets.ButtonText(new Rect(outRect.xMin + 24, outRect.yMax + 4, outRect.width - 48, 24f), "PF_ApplySettings".Translate()))
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
            Rect arrowRect = new Rect(headerRect.x, headerRect.y, 18, 18);
            headerRect.xMin += 18;
            Rect checkboxRect = headerRect.RightPartPixels(48);
            Texture2D tex = open ? TexButton.Collapse : TexButton.Reveal;
            if (Widgets.ButtonImage(arrowRect, tex, true))
            {
                if (open) SoundDefOf.TabClose.PlayOneShotOnCamera(null);
                else SoundDefOf.TabOpen.PlayOneShotOnCamera(null);
                categoryOpen[processDef] = !open;
            }

            Widgets.Label(headerRect, processDef.thingDef.LabelCap);
            Widgets.Checkbox(new Vector2(checkboxRect.xMin, checkboxRect.yMin), ref productAllowed, 20);
            localProductFilter.SetAllow(processDef.thingDef, productAllowed);

            if (open)
            {
                headerRect.xMin += 12;
                List<ThingDef> sortedIngredients = processDef.ingredientFilter.AllowedThingDefs.ToList();
                sortedIngredients.SortBy(x => x.label);
                foreach (ThingDef ingredient in sortedIngredients)
                {
                    checkboxRect.y += lineHeight;
                    headerRect.y += lineHeight;
                    bool ingredientAllowed = localIngredientFilter.Allows(ingredient);
                    Widgets.Label(headerRect, ingredient.LabelCap);
                    if (processDef.efficiency != 1)
                    {
                        Text.Font = GameFont.Tiny;
                        GUI.color = Color.gray;
                        Widgets.Label(new Rect(headerRect.width - 58f, headerRect.y + 2, 30f, 20f), "x" + (1f / processDef.efficiency).ToStringByStyle(ToStringStyle.FloatMaxTwo));
                        Text.Font = GameFont.Small;
                        GUI.color = Color.white;
                    }
                    Widgets.Checkbox(new Vector2(checkboxRect.xMin, checkboxRect.yMin), ref ingredientAllowed, 20);
                    localIngredientFilter.SetAllow(ingredient, ingredientAllowed);
                    listRect.y += lineHeight;
                }
            }
            listRect.y += lineHeight;
        }

        public void ResetFilters()
        {
            localProductFilter = new ThingFilter(ProductFilterCallback);
            localProductFilter.CopyAllowancesFrom(processors.First().productFilter);
            localIngredientFilter = new ThingFilter(IngredientFilterCallback);
            localIngredientFilter.CopyAllowancesFrom(processors.First().ingredientFilter);
        }

        public void ProductFilterCallback()
        {
            if (callbackActive || localIngredientFilter == null)
            {
                return;
            }
            callbackActive = true;
            foreach (ProcessDef processDef in localEnabledProcesses)
            {
                if (!processDef.ingredientFilter.AllowedThingDefs.SharesElementWith(localIngredientFilter.AllowedThingDefs))
                {
                    foreach (ThingDef thingDef in processDef.ingredientFilter.AllowedThingDefs)
                    {
                        localIngredientFilter.SetAllow(thingDef, true);
                    }
                }
            }
            foreach (ProcessDef processDef in processors.First().Props.processes.Except(localEnabledProcesses))
            {
                if (processDef.ingredientFilter.AllowedThingDefs.SharesElementWith(localIngredientFilter.AllowedThingDefs))
                {
                    foreach (ThingDef thingDef in processDef.ingredientFilter.AllowedThingDefs)
                    {
                        localIngredientFilter.SetAllow(thingDef, false);
                    }
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
            foreach (ProcessDef processDef in localEnabledProcesses)
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

        public IEnumerable<ProcessDef> localEnabledProcesses
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
