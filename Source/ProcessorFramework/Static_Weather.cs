using UnityEngine;
using Verse;

namespace ProcessorFramework
{
	[StaticConstructorOnStartup]
	public static class Static_Weather
	{
		public static readonly FloatRange SunGlowRange = new FloatRange(0f, 1.0f);
		public static readonly FloatRange SnowRateRange = new FloatRange(0f, 1.2f);
		public static readonly FloatRange RainRateRange = new FloatRange(0f, 1.0f);
		public static readonly FloatRange WindSpeedRange = new FloatRange(0f, 3f);
	}
}