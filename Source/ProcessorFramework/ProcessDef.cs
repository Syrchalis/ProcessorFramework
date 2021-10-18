using System;
using System.Globalization;
using System.Xml;
using UnityEngine;
using Verse;

namespace ProcessorFramework
{
	public class ProcessDef : Def
	{
        public int uniqueID; //mainly for multiplayer

        public ThingDef thingDef;
		public ThingFilter ingredientFilter = new ThingFilter();

        public float processDays = 6f;
        public float capacityFactor = 1;
        public float efficiency = 1f;
        public bool usesTemperature = true;
		public FloatRange temperatureSafe = new FloatRange(-1f, 32f);
		public FloatRange temperatureIdeal = new FloatRange(7f, 32f);
		public float ruinedPerDegreePerHour = 2.5f;
        public float speedBelowSafe = 0.1f;
        public float speedAboveSafe = 1f;
		public FloatRange sunFactor = new FloatRange(1f, 1f);
		public FloatRange rainFactor = new FloatRange(1f, 1f);
		public FloatRange snowFactor = new FloatRange(1f, 1f);
		public FloatRange windFactor = new FloatRange(1f, 1f);
        public float unpoweredFactor = 0f;
        public float unfueledFactor = 0f;
        public float powerUseFactor = 1f;
        public float fuelUseFactor = 1f;
        public string filledGraphicSuffix = null;
        public bool usesQuality = false;
        public QualityDays qualityDays = new QualityDays(1, 0, 0, 0, 0, 0, 0);
        public Color color = new Color(1.0f, 1.0f, 1.0f);
        public string customLabel = "";

		public override void ResolveReferences()
		{			
			ingredientFilter.ResolveReferences();			
		}

        public override string ToString()
        {
            return thingDef?.ToString() ?? "[invalid process]";
        }
    }

    public class QualityDays
    {
        public QualityDays()
        {
        }

        public QualityDays(float awful, float poor, float normal, float good, float excellent, float masterwork, float legendary)
        {
            this.awful = awful;
            this.poor = poor;
            this.normal = normal;
            this.good = good;
            this.excellent = excellent;
            this.masterwork = masterwork;
            this.legendary = legendary;
        }

        public void LoadDataFromXmlCustom(XmlNode xmlRoot)
        {
            if (xmlRoot.ChildNodes.Count != 1) Log.Error("PF: QualityDays configured incorrectly");
            else
            {
                string str = xmlRoot.FirstChild.Value;
                str = str.TrimStart(new char[]
                {
                    '('
                });
                str = str.TrimEnd(new char[]
                {
                    ')'
                });
                string[] array = str.Split(new char[]
                {
                    ','
                });
                CultureInfo invariantCulture = CultureInfo.InvariantCulture;
                awful = Convert.ToSingle(array[0], invariantCulture);
                poor = Convert.ToSingle(array[1], invariantCulture);
                normal = Convert.ToSingle(array[2], invariantCulture);
                good = Convert.ToSingle(array[3], invariantCulture);
                excellent = Convert.ToSingle(array[4], invariantCulture);
                masterwork = Convert.ToSingle(array[5], invariantCulture);
                legendary = Convert.ToSingle(array[6], invariantCulture);
            }
        }
        public float awful;
        public float poor;
        public float normal;
        public float good;
        public float excellent;
        public float masterwork;
        public float legendary;
    }
}