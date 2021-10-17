using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ProcessorFramework
{
    public class ITab_ProcessorContents : ITab
    {
        private const float H = 28;
        private const float Icon = 24;
        private const float Pad = 4;
        private const float PaddedIcon = Pad + Icon + Pad;
        private const float ThingWidth = 230;

        public static readonly List<Pair<float, Color>> WhiteToYellowToRed = new List<Pair<float, Color>>
        {
            new Pair<float, Color>(0, Color.red),
            new Pair<float, Color>(0.5f, Color.yellow),
            new Pair<float, Color>(1, ITab_Pawn_Gear.ThingLabelColor)
        };
        public static readonly List<Pair<float, Color>> GreenToYellowToRed = new List<Pair<float, Color>>
        {
            new Pair<float, Color>(0, Color.red),
            new Pair<float, Color>(0.5f, Color.yellow),
            new Pair<float, Color>(1, Color.green)
        };

        private float lastDrawnHeight;
        private Vector2 scrollPosition;

        public ITab_ProcessorContents()
        {
            labelKey = "PF_TabContents";
            size = new Vector2(600f, 450f);
        }

        //public override bool IsVisible => Find.Selector.SingleSelectedThing != null;

        protected override void FillTab()
        {
            IEnumerable<CompProcessor> processors = Find.Selector.SelectedObjects.Select(o => (o as ThingWithComps)?.TryGetComp<CompProcessor>());
            if (processors.EnumerableNullOrEmpty())
            {
                return;
            }

            Rect outRect = new Rect(default, size).ContractedBy(10f);
            outRect.yMin += 20f;
            Rect rect = new Rect(0f, 0f, outRect.width, Mathf.Max(lastDrawnHeight, outRect.height));
            Text.Font = GameFont.Small;
            Widgets.BeginScrollView(outRect, ref scrollPosition, rect);
            float num = 0f;
            DoItemsLists(rect, ref num, processors);
            lastDrawnHeight = num;
            Widgets.EndScrollView();
        }

        protected void DoItemsLists(Rect inRect, ref float curY, IEnumerable<CompProcessor> processors)
        {
            GUI.BeginGroup(inRect);

            GUI.color = Widgets.SeparatorLabelColor;
            Text.Anchor = TextAnchor.UpperLeft;

            Widgets.Label(new Rect(ThingWidth, curY + 3, ThingWidth, 30), "PF_Product".Translate());
            Widgets.Label(new Rect(ThingWidth + ThingWidth, curY + 3, inRect.width - ThingWidth - ThingWidth, 30), "PF_TargetQuality".Translate());
            Widgets.ListSeparator(ref curY, inRect.width, "PF_FermentingItems".Translate());
            bool flag = false;

            foreach (CompProcessor processor in processors)
            {
                for (int i = 0; i < processor.innerContainer.Count; i++)
                {
                    Thing t = processor.innerContainer[i];
                    if (t != null)
                    {
                        flag = true;
                        DoThingRow(t.def, t, inRect.width, ref curY, processor);
                    }
                }
            }

            if (!flag)
            {
                Widgets.NoneLabel(ref curY, inRect.width);
            }

            GUI.EndGroup();
        }

        protected void DoThingRow(ThingDef thingDef, Thing thing, float width, ref float y, CompProcessor processor)
        {
            ActiveProcess progress = processor.activeProcesses.Find(x => x.ingredientThings.Contains(thing));
            if (progress == null)
                return;

            // Areas
            Rect ingredientArea = new Rect(0, y, ThingWidth, H);
            Rect productArea = new Rect(ingredientArea.xMax, y, ThingWidth, H);

            Rect qualityArea = new Rect(productArea.xMax, y, 50, H);
            Rect percentAndSpeed = new Rect(width - 70, y, 75, H);

            if (Mouse.IsOver(new Rect(0, y, width, H)))
                TargetHighlighter.Highlight(thing);

            // Hover over percentage or speed icon
            if (Mouse.IsOver(percentAndSpeed))
            {
                GUI.color = ITab_Pawn_Gear.HighlightColor;
                GUI.DrawTexture(percentAndSpeed, TexUI.HighlightTex);
            }

            // Draw ✓, ~, or X based on the current speed
            Texture2D runningTexture = progress.SpeedFactor <= 0 ? Widgets.CheckboxOffTex
                : progress.SpeedFactor <= 0.75f ? Widgets.CheckboxPartialTex
                : Widgets.CheckboxOnTex;

            GUI.color = Color.white;
            GUI.DrawTexture(new Rect((percentAndSpeed.xMax - PaddedIcon) + Pad, y, Icon, Icon), runningTexture);

            // Progress %
            GUI.color = ITab_Pawn_Gear.ThingLabelColor;
            Text.Anchor = TextAnchor.MiddleRight;
            Widgets.Label(new Rect(percentAndSpeed.x, y, percentAndSpeed.xMax - percentAndSpeed.x - PaddedIcon, H), $"{Mathf.Floor(progress.ActiveProcessPercent * 100):0}%");

            // Hover over progress %
            TooltipHandler.TipRegion(percentAndSpeed, () => progress.ProgressTooltip, 23492376);

            // Quality label or dropdown to change the quality
            if (progress.processDef.usesQuality)
            {
                Widgets.Dropdown(qualityArea, progress, p => p?.TargetQuality ?? QualityCategory.Normal, GetProgressQualityDropdowns,
                    progress.TargetQuality.GetLabel().CapitalizeFirst(),
                    (Texture2D)ProcessorFramework_Utility.qualityMaterials[progress.TargetQuality].mainTexture);
            }
            else
            {
                if (Mouse.IsOver(qualityArea))
                {
                    GUI.color = ITab_Pawn_Gear.HighlightColor;
                    GUI.DrawTexture(qualityArea, TexUI.HighlightTex);
                }

                GUI.color = ITab_Pawn_Gear.ThingLabelColor;
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(qualityArea, "PF_NA".Translate());
            }

            TooltipHandler.TipRegion(qualityArea, () => progress.QualityTooltip, "PF_QualityTooltip1".GetHashCode());

            GUI.color = Color.white;

            // Info buttons
            Widgets.InfoCardButton(ingredientArea.xMax - Pad - Icon, y, thing);
            Widgets.InfoCardButton(productArea.xMax - Pad - Icon, y, progress.processDef.thingDef);

            // Hover over product or ingredient
            Rect ingredientHover = new Rect(ingredientArea.x, y, ingredientArea.width - PaddedIcon, H);
            if (Mouse.IsOver(ingredientHover))
            {
                GUI.color = ITab_Pawn_Gear.HighlightColor;
                GUI.DrawTexture(ingredientHover, TexUI.HighlightTex);
            }

            Rect productHover = new Rect(productArea.x, y, productArea.width - PaddedIcon, H);
            if (Mouse.IsOver(productHover))
            {
                GUI.color = ITab_Pawn_Gear.HighlightColor;
                GUI.DrawTexture(productHover, TexUI.HighlightTex);
            }

            GUI.color = Color.white;

            // Icons for product and ingredient
            if (thingDef.DrawMatSingle?.mainTexture != null)
                Widgets.ThingIcon(new Rect(ingredientArea.x, y, PaddedIcon, H), thing);

            if (progress.processDef.thingDef?.DrawMatSingle?.mainTexture != null)
                Widgets.ThingIcon(new Rect(productArea.x, y, PaddedIcon, H), progress.processDef.thingDef);

            // Product and ingredient labels
            Text.Anchor = TextAnchor.MiddleLeft;
            GUI.color = GenUI.LerpColor(WhiteToYellowToRed, 1.0f - progress.ruinedPercent);

            var (ingredientLabel, productLabel) = Tuple.Create(thing.LabelCap, GenLabel.ThingLabel(progress.processDef.thingDef, null, Mathf.RoundToInt(thing.stackCount * progress.processDef.efficiency)).CapitalizeFirst());

            Text.WordWrap = false;
            Widgets.Label(new Rect(ingredientArea.x + PaddedIcon, y, 200 - PaddedIcon, H), ingredientLabel.Truncate(ingredientArea.width));
            Widgets.Label(new Rect(productArea.x + PaddedIcon, y, 200 - PaddedIcon, H), productLabel.Truncate(productArea.width));

            Text.WordWrap = true;
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = Color.white;
            TooltipHandler.TipRegion(new Rect(productArea.x, y, productArea.width - PaddedIcon, H), () => progress.ProcessTooltip(ingredientLabel, productLabel), "PF_CreatingTooltip1".GetHashCode());
            TooltipHandler.TipRegion(new Rect(ingredientArea.x, y, productArea.width - PaddedIcon, H), () => progress.ProcessTooltip(ingredientLabel, productLabel), "PF_CreatingTooltip2".GetHashCode());

            y += H;
        }

        private IEnumerable<Widgets.DropdownMenuElement<QualityCategory>> GetProgressQualityDropdowns(ActiveProcess activeProcess)
        {
            if (activeProcess == null)
                yield break;

            foreach (QualityCategory quality in QualityUtility.AllQualityCategories)
            {
                Material iconMaterial = ProcessorFramework_Utility.qualityMaterials[quality];
                yield return new Widgets.DropdownMenuElement<QualityCategory>
                {
                    option = new FloatMenuOption(
                        quality.GetLabel().CapitalizeFirst(),
                        () => activeProcess.TargetQuality = quality,
                        (Texture2D)iconMaterial.mainTexture,
                        iconMaterial.color),
                    payload = quality
                };
            }
        }
    }
}
