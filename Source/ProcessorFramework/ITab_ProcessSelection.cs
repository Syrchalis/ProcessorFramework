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

        private IEnumerable<CompProcessor> processorComps;
        private Dictionary<ProcessDef, bool> categoryOpen = new Dictionary<ProcessDef, bool>();

        private const int lineHeight = 22;

        public ITab_ProcessSelection()
        {
            labelKey = "PF_ITab_ItemSelection";
            size = new Vector2(300, 400);
        }

        public override bool IsVisible => Find.Selector.SelectedObjects.All(x => x is Thing thing && thing.Faction == Faction.OfPlayerSilentFail && thing.TryGetComp<CompProcessor>() != null);

        protected override void FillTab()
        {
            List<object> selectedObjects = Find.Selector.SelectedObjects;
            processorComps = selectedObjects.Select(o => (o as ThingWithComps)?.TryGetComp<CompProcessor>());
            
            List<ProcessDef> processDefs = processorComps.First().Props.processes;

            if (processorComps.EnumerableNullOrEmpty())
            {
                return;
            }

            Rect outRect = new Rect(default, size).ContractedBy(12f);
            outRect.yMin += 24; //top space
            //outRect.height -= 24; //height adjust, not needed anymore since no apply button anymore
            int viewRectHeight = processDefs.Count * lineHeight + 80; //increase scroll area for each product listed and extra space at the end
            foreach (KeyValuePair<ProcessDef, bool> keyValuePair in categoryOpen)
            {
                //adjust scroll area for each node opened
                viewRectHeight += processDefs.Contains(keyValuePair.Key) && keyValuePair.Value ? keyValuePair.Key.ingredientFilter.AllowedDefCount * lineHeight : 0;
            }
            Rect viewRect = new Rect(0f, 0f, outRect.width - GUI.skin.verticalScrollbar.fixedWidth - 1f, viewRectHeight);
            Widgets.DrawMenuSection(outRect);
            Rect buttonRect = new Rect(outRect.x + 1f, outRect.y + 1f, (outRect.width - 2f) / 2f, 24f);
            Text.Font = GameFont.Small;
            if (Widgets.ButtonText(buttonRect, "ClearAll".Translate(), true, true, true))
            {
                foreach (CompProcessor processor in processorComps)
                {
                    processor.enabledProcesses.Clear();
                }
                SoundDefOf.Checkbox_TurnedOff.PlayOneShotOnCamera(null);
            }
            if (Widgets.ButtonText(new Rect(buttonRect.xMax + 1f, buttonRect.y, outRect.xMax - 1f - (buttonRect.xMax + 1f), 24f), "AllowAll".Translate(), true, true, true))
            {
                foreach (CompProcessor processor in processorComps)
                {
                    processor.EnableAllProcesses();
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
                DoItemsList(ref listRect, processDef);
            }
            Widgets.EndScrollView();
        }
        public void DoItemsList(ref Rect listRect, ProcessDef processDef)
        {
            bool open = categoryOpen[processDef];
            
            Rect headerRect = listRect.TopPartPixels(24);
            Rect arrowRect = new Rect(headerRect.x, headerRect.y, 18, 18);
            headerRect.xMin += 18;
            Rect checkboxRect = new Rect(headerRect.x + headerRect.width - 48f, headerRect.y, 20, 20);
            Texture2D tex = open ? TexButton.Collapse : TexButton.Reveal;
            if (Widgets.ButtonImage(arrowRect, tex, true))
            {
                if (open) SoundDefOf.TabClose.PlayOneShotOnCamera(null);
                else SoundDefOf.TabOpen.PlayOneShotOnCamera(null);
                categoryOpen[processDef] = !open;
            }

            Widgets.DrawTextureFitted(new Rect(headerRect.x - 4, headerRect.y, 24, 24), ProcessorFramework_Utility.processIcons[processDef], 1);
            Widgets.Label(new Rect(headerRect.x + 20, headerRect.y, 280, 24), processDef.thingDef.LabelCap);
            if (processDef.destroyChance != 0)
            {
                Rect destroyChanceRect = new Rect(headerRect.width - 80, headerRect.y + 2, 32, 20f);
                Text.Anchor = TextAnchor.UpperRight;
                if (Mouse.IsOver(destroyChanceRect))
                {
                    GUI.color = ITab_Pawn_Gear.HighlightColor;
                    GUI.DrawTexture(destroyChanceRect, TexUI.HighlightTex);
                }
                TooltipHandler.TipRegion(destroyChanceRect, () => "PF_DestroyChanceTooltip".Translate(), 23492389);
                Text.Font = GameFont.Tiny;
                GUI.color = Color.red;
                Widgets.Label(destroyChanceRect, processDef.destroyChance.ToStringByStyle(ToStringStyle.PercentZero));
                GUI.color = Color.white;
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.UpperLeft;
            }

            MultiCheckboxState processState = ProcessStateOf(processDef);
            MultiCheckboxState multiCheckboxState = Widgets.CheckboxMulti(checkboxRect, processState, true);
            if (processState != multiCheckboxState && multiCheckboxState != MultiCheckboxState.Partial)
            {
                foreach (CompProcessor compProcessor in processorComps)
                {
                    compProcessor.ToggleProcess(processDef, multiCheckboxState == MultiCheckboxState.On);
                }
            }

            if (open)
            {
                headerRect.xMin += 12;
                List<ThingDef> sortedIngredients = processDef.ingredientFilter.AllowedThingDefs.ToList();
                sortedIngredients.SortBy(x => x.label);
                foreach (ThingDef ingredient in sortedIngredients)
                {
                    checkboxRect.y += lineHeight;
                    headerRect.y += lineHeight;
                    Widgets.DrawTextureFitted(new Rect(headerRect.x - 4, headerRect.y, 24, 24), ProcessorFramework_Utility.ingredientIcons[ingredient], 1);
                    Widgets.Label(new Rect(headerRect.x + 20, headerRect.y, 280, 24), ingredient.LabelCap);
                    if (processDef.efficiency != 1)
                    {
                        Rect efficiencyRect = new Rect(headerRect.width - 70, headerRect.y + 2, 32, 20f);
                        Text.Anchor = TextAnchor.UpperRight;
                        if (Mouse.IsOver(efficiencyRect))
                        {
                            GUI.color = ITab_Pawn_Gear.HighlightColor;
                            GUI.DrawTexture(efficiencyRect, TexUI.HighlightTex);
                        }
                        TooltipHandler.TipRegion(efficiencyRect, () => "PF_EfficiencyTooltip".Translate(), 23492389);
                        Text.Font = GameFont.Tiny;
                        GUI.color = Color.gray;
                        Widgets.Label(efficiencyRect, "x" + (1f / processDef.efficiency).ToStringByStyle(ToStringStyle.FloatMaxTwo));
                        Text.Font = GameFont.Small;
                        GUI.color = Color.white;
                        Text.Anchor = TextAnchor.UpperLeft;
                    }
                    MultiCheckboxState ingredientState = IngredientStateOf(processDef, ingredient);
                    MultiCheckboxState multiCheckboxState2 = Widgets.CheckboxMulti(checkboxRect, ingredientState, true);
                    if (ingredientState != multiCheckboxState2 && multiCheckboxState2 != MultiCheckboxState.Partial)
                    {
                        foreach (CompProcessor compProcessor in processorComps)
                        {
                            compProcessor.ToggleIngredient(processDef, ingredient, multiCheckboxState2 == MultiCheckboxState.On);
                        }
                    }
                    listRect.y += lineHeight;
                }
            }
            listRect.y += lineHeight;
        }

        public MultiCheckboxState ProcessStateOf(ProcessDef processDef)
        {
            int count = processorComps.Count(x => x.enabledProcesses.ContainsKey(processDef));
            if (count > 0)
            {
                if (count == processorComps.Count())
                {
                    return MultiCheckboxState.On;
                }
                return MultiCheckboxState.Partial;
            }
            return MultiCheckboxState.Off;
        }
        public MultiCheckboxState IngredientStateOf(ProcessDef processDef, ThingDef ingredient)
        {
            int count = processorComps.Count(x => x.enabledProcesses.TryGetValue(processDef, out ProcessFilter processFilter) && processFilter.allowedIngredients.Contains(ingredient));
            if (count > 0)
            {
                if (count == processorComps.Count())
                {
                    return MultiCheckboxState.On;
                }
                return MultiCheckboxState.Partial;
            }
            return MultiCheckboxState.Off;
        }
        /*
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
            foreach (ProcessDef processDef in processorComps.First().Props.processes.Except(localEnabledProcesses))
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
                ThingDef relatedProduct = processorComps.First().Props.processes.Find(x => x.ingredientFilter.Allows(ingredient)).thingDef;
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
                if (!processorComps.EnumerableNullOrEmpty())
                {
                    foreach (ThingDef product in localProductFilter.AllowedThingDefs)
                    {
                        yield return processorComps.First().Props.processes.Find(x => x.thingDef == product);
                    }
                }
            }
        }*/
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
    public class HotSwappableAttribute : Attribute
    {
    }
}
