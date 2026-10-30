using System;
using System.Globalization;
using System.Diagnostics;
using Naispo.Overhead;
using UnityEditor;
using UnityEngine;
using Debug=UnityEngine.Debug;

// Run in the same Unity process to compare the former analytic pixel loop with
// the runtime cache. Timings cover CPU work; they are not whole-game frame rates.
public static class RoomLightingBenchmark
{
    const int Width=288,Height=180;
    static readonly int[] Steps={1,11,25,26,37,64,100,115,116,128,160,166,190,210,243,256};

    [MenuItem("Overhead/Measure room lighting")]
    public static void Run()
    {
        var shade=new Color32[Width*Height];
        var warm=new Color32[Width*Height];
        long started=Stopwatch.GetTimestamp();
        var cache=new RoomLightingCache(Width,Height);
        double prepare=Milliseconds(started);
        var shadeTexture=Texture();
        var warmTexture=Texture();
        try
        {
            // Warm up JIT and the same-sized upload before timing either path.
            LegacyPixels(0,shade,warm);
            cache.TryUpdate(0);
            Upload(shadeTexture,warmTexture,cache);
            int count=Steps.Length*2,maxError=0;
            var legacyMs=new double[count];
            var cachedMs=new double[count];
            var uploadMs=new double[count];
            long allocated=0;
            for(int i=0;i<count;i++)
            {
                float progress=Steps[i%Steps.Length]/256f;
                // Alternate order to reduce a systematic first/second-run bias.
                if(i%2==0)
                {
                    started=Stopwatch.GetTimestamp();
                    LegacyPixels(progress,shade,warm);
                    legacyMs[i]=Milliseconds(started);
                }
                long bytes=GC.GetAllocatedBytesForCurrentThread();
                started=Stopwatch.GetTimestamp();
                if(!cache.TryUpdate(progress))throw new InvalidOperationException("Benchmark repeated a cached step");
                cachedMs[i]=Milliseconds(started);
                allocated+=GC.GetAllocatedBytesForCurrentThread()-bytes;
                if(i%2!=0)
                {
                    started=Stopwatch.GetTimestamp();
                    LegacyPixels(progress,shade,warm);
                    legacyMs[i]=Milliseconds(started);
                }
                started=Stopwatch.GetTimestamp();
                Upload(shadeTexture,warmTexture,cache);
                uploadMs[i]=Milliseconds(started);
                for(int pixel=0;pixel<shade.Length;pixel++)
                {
                    maxError=Math.Max(maxError,Math.Abs(shade[pixel].a-cache.ShadePixels[pixel].a));
                    maxError=Math.Max(maxError,Math.Abs(warm[pixel].a-cache.WarmPixels[pixel].a));
                }
            }
            if(maxError>1)throw new InvalidOperationException("Lighting output changed: "+maxError+" alpha bytes");
            Debug.Log(string.Format(CultureInfo.InvariantCulture,
                "ROOM_LIGHTING_BENCHMARK pixels={0} samples={1} prepare_ms={2:F3} legacy_median_ms={3:F3} legacy_max_ms={4:F3} cached_median_ms={5:F3} cached_max_ms={6:F3} upload_median_ms={7:F3} upload_max_ms={8:F3} cached_allocated_bytes={9} max_alpha_byte_error={10}",
                Width*Height,count,prepare,Median(legacyMs),Maximum(legacyMs),Median(cachedMs),Maximum(cachedMs),
                Median(uploadMs),Maximum(uploadMs),allocated,maxError));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(shadeTexture);
            UnityEngine.Object.DestroyImmediate(warmTexture);
        }
    }

    public static void RunAndBuild()
    {
        Run();
        OverheadSetup.BuildWindows();
    }

    static Texture2D Texture() => new Texture2D(Width,Height,TextureFormat.RGBA32,false)
    {
        hideFlags=HideFlags.DontSave,
        wrapMode=TextureWrapMode.Clamp,
        filterMode=FilterMode.Bilinear
    };

    static void Upload(Texture2D shade,Texture2D warm,RoomLightingCache cache)
    {
        shade.SetPixels32(cache.ShadePixels);
        shade.Apply(false,false);
        warm.SetPixels32(cache.WarmPixels);
        warm.Apply(false,false);
    }

    static void LegacyPixels(float progress,Color32[] shade,Color32[] warm)
    {
        for(int y=0;y<Height;y++)for(int x=0;x<Width;x++)
        {
            Vector2 point=new Vector2((x+0.5f)*1440/Width,900-(y+0.5f)*900/Height);
            int index=y*Width+x;
            shade[index]=new Color32(3,10,17,(byte)Mathf.RoundToInt(RoomAwakening.ShadeAlpha(point,progress)*255));
            warm[index]=new Color32(245,179,86,(byte)Mathf.RoundToInt(RoomAwakening.WarmAlpha(point,progress)*255));
        }
    }

    static double Milliseconds(long started) => (Stopwatch.GetTimestamp()-started)*1000.0/Stopwatch.Frequency;
    static double Median(double[] samples)
    {
        var ordered=(double[])samples.Clone();
        Array.Sort(ordered);
        return (ordered[(ordered.Length-1)/2]+ordered[ordered.Length/2])*0.5;
    }
    static double Maximum(double[] samples)
    {
        double value=0;
        foreach(double sample in samples)value=Math.Max(value,sample);
        return value;
    }
}
