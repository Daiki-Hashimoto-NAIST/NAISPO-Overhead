using UnityEngine;

namespace Naispo.Overhead
{
    // Evaluate on the game's clock so pausing, repeated GUI passes and replay stay in sync.
    public static class GardenAtmosphere
    {
        public const float MilestoneDuration=2.6f,FinaleDuration=3.4f;

        public static float Warmth(GardenProgress garden,float playTime)
        {
            float warmth=0;
            for(int stage=0;stage<garden.BloomCount;stage++)
            {
                float t=Mathf.Clamp01((playTime-garden.BloomStartedAt(stage))/3.2f);
                warmth+=t*t*(3-2*t)/GardenProgress.StageCount;
            }
            return Mathf.Clamp01(warmth);
        }

        public static float BreezeAngle(GardenProgress garden,float playTime,float rootX)
        {
            if(garden.BloomCount<2)return 0;
            float elapsed=playTime-garden.BloomStartedAt(1)-3.8f;
            if(elapsed<0)return 0;
            // A slow gust crosses left to right, followed by seven seconds of complete calm.
            float phase=Mathf.Repeat(elapsed,12.5f)-Mathf.Clamp01(rootX/1440)*1.8f;
            if(phase<=0||phase>=3.4f)return 0;
            float wave=Mathf.Sin(phase/3.4f*Mathf.PI);
            return 4.6f*wave*wave;
        }
    }
}
