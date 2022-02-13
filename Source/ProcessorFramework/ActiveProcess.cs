using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;

namespace ProcessorFramework
{
    public class ActiveProcess : IExposable
    {
        private readonly CompProcessor processor;
        
        public ProcessDef processDef;
        public long activeProcessTicks;
        public int ingredientCount;
        public List<Thing> ingredientThings;
        public QualityCategory targetQuality;
        public float ruinedPercent;
        public float speedFactor;

        [Unsaved]
        private Material activeProcessColorMaterial;

        public ActiveProcess(CompProcessor parent)
        {
            processor = parent;
        }

        public long ActiveProcessTicks
        {
            get => activeProcessTicks;
            set
            {
                if (value == activeProcessTicks)
                {
                    return;
                }
                activeProcessTicks = value;
                activeProcessColorMaterial = null;
            }
        }
        public float ActiveProcessDays => (float)ActiveProcessTicks / GenDate.TicksPerDay;
        public float ActiveProcessPercent => Mathf.Clamp01(ActiveProcessDays / (processDef.usesQuality ? DaysToReachTargetQuality : processDef.processDays));
        public bool Complete => ActiveProcessPercent >= 1f || EmptyNow;
        public bool EmptyNow => processDef.usesQuality && ActiveProcessDays >= processDef.qualityDays.awful && processor.emptyNow;
        public bool Ruined => ruinedPercent >= 1f;
        public float SpeedFactor => speedFactor;
        public Map CurrentMap => processor.parent.Map;
        public QualityCategory TargetQuality
        {
            get => processDef.usesQuality ? targetQuality : QualityCategory.Normal;
            set
            {
                if (value == targetQuality || !processDef.usesQuality)
                    return;

                targetQuality = value;
                activeProcessColorMaterial = null;
            }
        }
        public float DaysToReachTargetQuality
        {
            get
            {
                return processDef.qualityDays.DaysForQuality(targetQuality);
            }
        }
        public QualityCategory CurrentQuality
        {
            get
            {
                if (ActiveProcessDays < processDef.qualityDays.poor)
                    return QualityCategory.Awful;
                if (ActiveProcessDays < processDef.qualityDays.normal)
                    return QualityCategory.Poor;
                if (ActiveProcessDays < processDef.qualityDays.good)
                    return QualityCategory.Normal;
                if (ActiveProcessDays < processDef.qualityDays.excellent)
                    return QualityCategory.Good;
                if (ActiveProcessDays < processDef.qualityDays.masterwork)
                    return QualityCategory.Excellent;
                if (ActiveProcessDays < processDef.qualityDays.legendary)
                    return QualityCategory.Masterwork;
                if (ActiveProcessDays >= processDef.qualityDays.legendary)
                    return QualityCategory.Legendary;
                return QualityCategory.Normal;
            }
        }
        public int EstimatedTicksLeft => SpeedFactor <= 0 ? -1 : Mathf.Max(processDef.usesQuality ? 
            Mathf.RoundToInt((DaysToReachTargetQuality * GenDate.TicksPerDay) - ActiveProcessTicks) : Mathf.RoundToInt((processDef.processDays * GenDate.TicksPerDay) - ActiveProcessTicks), 0);

        public void DoTicks(int ticks)
        {
            ActiveProcessTicks += Mathf.RoundToInt(ticks * SpeedFactor);

            if (!Ruined && processDef.usesTemperature)
            {
                float ambientTemperature = processor.parent.AmbientTemperature;
                if (ambientTemperature > processDef.temperatureSafe.max)
                {
                    ruinedPercent += (ambientTemperature - processDef.temperatureSafe.max) * (processDef.ruinedPerDegreePerHour / GenDate.TicksPerHour / 100f) * ticks;
                }
                else if (ambientTemperature < processDef.temperatureSafe.min)
                {
                    ruinedPercent -= (ambientTemperature - processDef.temperatureSafe.min) * (processDef.ruinedPerDegreePerHour / GenDate.TicksPerHour / 100f) * ticks;
                }
                if (ruinedPercent >= 1f)
                {
                    ruinedPercent = 1f;
                    processor.parent.BroadcastCompSignal("RuinedByTemperature");
                }
                else if (ruinedPercent < 0f)
                {
                    ruinedPercent = 0f;
                }
            }
        }

        public void TickRare()
        {
            speedFactor = CalcSpeedFactor();
        }

        /// <summary>Used when processes are not independent.</summary>
        public void MergeProcess(Thing ingredient)
        {
            activeProcessTicks = Mathf.RoundToInt(GenMath.WeightedAverage(0f, ingredient.stackCount, activeProcessTicks, ingredientCount));
            ingredientCount += ingredient.stackCount;
            if (!ingredientThings.Any(x => x.CanStackWith(ingredient)))
            {
                ingredientThings.Add(ingredient);
            }
            processor.innerContainer.TryAddOrTransfer(ingredient, true);
        }

        private float CalcSpeedFactor()
        {
            return Mathf.Max(CurrentPowerFactor * CurrentFuelFactor * CurrentTemperatureFactor * CurrentSunFactor * CurrentRainFactor * CurrentSnowFactor * CurrentWindFactor, 0f);
        }

        public float CurrentPowerFactor
        {
            get
            {
                return processor.Powered ? 1f : processDef.unpoweredFactor;
            }
        }
        public float CurrentFuelFactor
        {
            get
            {
                return processor.Fueled ? 1f : processDef.unfueledFactor;
            }
        }
        public float CurrentSunFactor
        {
            get
            {
                if (CurrentMap == null)
                    return 0f;

                if (processDef.sunFactor.Span == 0)
                    return 1f;

                float skyGlow = CurrentMap.skyManager.CurSkyGlow * (1 - processor.RoofCoverage);
                return GenMath.LerpDouble(Static_Weather.SunGlowRange.TrueMin, Static_Weather.SunGlowRange.TrueMax,
                    processDef.sunFactor.min, processDef.sunFactor.max,
                    skyGlow);
            }
        }
        public float CurrentTemperatureFactor
        {
            get
            {
                if (!processDef.usesTemperature)
                    return 1f;

                float ambientTemperature = processor.parent.AmbientTemperature;
                // Temperature out of a safe range
                if (ambientTemperature < processDef.temperatureSafe.min)
                    return processDef.speedBelowSafe;

                if (ambientTemperature > processDef.temperatureSafe.max)
                    return processDef.speedAboveSafe;

                // Temperature out of an ideal range but still within a safe range
                if (ambientTemperature < processDef.temperatureIdeal.min)
                    return GenMath.LerpDouble(processDef.temperatureSafe.min, processDef.temperatureIdeal.min, processDef.speedBelowSafe, 1f, ambientTemperature);

                if (ambientTemperature > processDef.temperatureIdeal.max)
                    return GenMath.LerpDouble(processDef.temperatureIdeal.max, processDef.temperatureSafe.max, 1f, processDef.speedAboveSafe, ambientTemperature);

                // Temperature within an ideal range
                return 1f;
            }
        }
        public float CurrentRainFactor
        {
            get
            {
                if (CurrentMap == null)
                    return 0f;

                if (processDef.rainFactor.Span == 0)
                    return 1f;

                // When snowing, the game also increases RainRate.
                // Therefore, non-zero SnowRate puts RainRespect to a state as if it was not raining.
                if (CurrentMap.weatherManager.SnowRate != 0)
                    return processDef.rainFactor.min;

                float rainRate = CurrentMap.weatherManager.RainRate * (1 - processor.RoofCoverage);
                return GenMath.LerpDoubleClamped(Static_Weather.RainRateRange.TrueMin, Static_Weather.RainRateRange.TrueMax,
                    processDef.rainFactor.min, processDef.rainFactor.max,
                    rainRate);
            }
        }
        public float CurrentSnowFactor
        {
            get
            {
                if (CurrentMap == null)
                    return 0f;

                if (processDef.snowFactor.Span == 0)
                    return 1f;

                float snowRate = CurrentMap.weatherManager.SnowRate * (1 - processor.RoofCoverage);
                return GenMath.LerpDoubleClamped(Static_Weather.SnowRateRange.TrueMin, Static_Weather.SnowRateRange.TrueMax,
                    processDef.snowFactor.min, processDef.snowFactor.max,
                    snowRate);
            }
        }
        public float CurrentWindFactor
        {
            get
            {
                if (CurrentMap == null)
                    return 0f;

                if (processDef.windFactor.Span == 0)
                    return 1f;

                if (processor.RoofCoverage != 0)
                    return processDef.windFactor.min;

                return GenMath.LerpDoubleClamped(Static_Weather.WindSpeedRange.TrueMin, Static_Weather.WindSpeedRange.TrueMax,
                    processDef.windFactor.min, processDef.windFactor.max,
                    CurrentMap.windManager.WindSpeed);
            }
        }
        public string ProgressTooltip
        {
            get
            {
                StringBuilder progressTip = new StringBuilder();
                progressTip.AppendTagged("PF_SpeedTooltip1".Translate(ActiveProcessPercent.ToStringPercent().Named("COMPLETEPERCENT"), SpeedFactor.ToStringPercentColored().Named("SPEED")));
                progressTip.AppendTagged("PF_SpeedTooltip2".Translate(
                    CurrentTemperatureFactor.ToStringPercentColored().Named("TEMPERATURE"),
                    CurrentWindFactor.ToStringPercentColored().Named("WIND"),
                    CurrentRainFactor.ToStringPercentColored().Named("RAIN"),
                    CurrentSnowFactor.ToStringPercentColored().Named("SNOW"),
                    CurrentSunFactor.ToStringPercentColored().Named("SUN")));

                if (!Complete)
                    progressTip.AppendTagged("PF_SpeedTooltip3".Translate(EstimatedTicksLeft.ToStringTicksToPeriod(canUseDecimals: false).Named("ESTIMATED")));

                return progressTip.ToString();
            }
        }
        public string QualityTooltip
        {
            get
            {
                if (!processDef.usesQuality)
                    return "PF_QualityTooltipNA".Translate(processDef.thingDef.Named("PRODUCT")).CapitalizeFirst();

                StringBuilder qualityTip = new StringBuilder();

                qualityTip.AppendTagged("PF_QualityTooltip1".Translate(
                    ActiveProcessDays < processDef.qualityDays.awful
                        ? "PF_None".TranslateSimple().Named("CURRENT")
                        : CurrentQuality.GetLabel().Named("CURRENT"),
                    TargetQuality.GetLabel().Named("TARGET")));

                qualityTip.AppendTagged("PF_QualityTooltip2".Translate(
                    TimeForQualityLeft(QualityCategory.Awful).Named("AWFUL"),
                    TimeForQualityLeft(QualityCategory.Poor).Named("POOR"),
                    TimeForQualityLeft(QualityCategory.Normal).Named("NORMAL"),
                    TimeForQualityLeft(QualityCategory.Good).Named("GOOD"),
                    TimeForQualityLeft(QualityCategory.Excellent).Named("EXCELLENT"),
                    TimeForQualityLeft(QualityCategory.Masterwork).Named("MASTERWORK"),
                    TimeForQualityLeft(QualityCategory.Legendary).Named("LEGENDARY")
                ));

                return qualityTip.ToString();
            }
        }
        public string TimeForQualityLeft(QualityCategory qualityCategory)
        {
            int ticksLeft = Mathf.Max(Mathf.RoundToInt(processDef.qualityDays.DaysForQuality(qualityCategory) * GenDate.TicksPerDay - ActiveProcessTicks), 0);
            return ticksLeft == 0 ? "PF_None".Translate() : ticksLeft.ToStringTicksToPeriod(canUseDecimals: false);
        }

        public string ProcessTooltip(string ingredientLabel, string productLabel)
        {
            StringBuilder creatingTip = new StringBuilder();

            string qualityStr = processDef.usesQuality ? $" ({TargetQuality.GetLabel().CapitalizeFirst()})" : "";

            creatingTip.AppendTagged("PF_CreatingTooltip1".Translate(productLabel.Named("PRODUCT"), ingredientLabel.Named("INGREDIENT"), qualityStr.Named("QUALITY")));
            creatingTip.AppendTagged(processDef.usesQuality
                ? "PF_CreatingTooltip2_Quality".Translate(Mathf.RoundToInt(processDef.qualityDays.awful * GenDate.TicksPerDay).ToStringTicksToPeriod().Named("TOAWFUL"))
                : "PF_CreatingTooltip2_NoQuality".Translate(Mathf.RoundToInt(processDef.processDays * GenDate.TicksPerDay).ToStringTicksToPeriod().Named("TIME")));

            if (processDef.usesTemperature)
            {
                creatingTip.AppendTagged("PF_CreatingTooltip3".Translate(
                    processDef.temperatureIdeal.min.ToStringTemperature().Named("MIN"),
                    processDef.temperatureIdeal.max.ToStringTemperature().Named("MAX")));
                creatingTip.AppendTagged("PF_CreatingTooltip4".Translate(
                    processDef.temperatureSafe.min.ToStringTemperature().Named("MIN"),
                    processDef.temperatureSafe.max.ToStringTemperature().Named("MAX"),
                    (processDef.ruinedPerDegreePerHour / 100f).ToStringPercent().Named("PERHOUR")
                ));
            }

            if (ruinedPercent > 0.05f)
            {
                creatingTip.AppendTagged("PF_CreatingTooltip5".Translate(ruinedPercent.ToStringPercent().Colorize(Color.red)));
            }

            if (!processDef.temperatureSafe.Includes(processor.parent.AmbientTemperature) && !Ruined)
            {
                creatingTip.Append("PF_CreatingTooltip6".Translate(processor.parent.AmbientTemperature.ToStringTemperature()).Resolve().Colorize(Color.red));
            }

            return creatingTip.ToString();
        }


        public Material ProgressColorMaterial
        {
            get
            {
                activeProcessColorMaterial ??= SolidColorMaterials.SimpleSolidColorMaterial(Color.Lerp(Static_Bar.ZeroProgressColor, Static_Bar.FermentedColor, ActiveProcessPercent));
                return activeProcessColorMaterial;
            }
        }

        public void ExposeData()
        {
            Scribe_Defs.Look<ProcessDef>(ref processDef, "PF_processDef");
            Scribe_Collections.Look(ref ingredientThings, "ingredientThings", LookMode.Reference);
            Scribe_Values.Look(ref ruinedPercent, "PF_ruinedPercent", 0f);
            Scribe_Values.Look(ref ingredientCount, "PF_ingredientCount", 0);
            Scribe_Values.Look(ref activeProcessTicks, "PF_activeProcessTicks", 0);
            Scribe_Values.Look(ref targetQuality, "targetQuality", QualityCategory.Normal);
        }
    }
}
