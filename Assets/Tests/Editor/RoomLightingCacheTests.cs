using System;
using NUnit.Framework;
using UnityEngine;

namespace Naispo.Overhead.Tests
{
    public sealed class RoomLightingCacheTests
    {
        [TestCase(37,23,1)]
        [TestCase(288,180,11)]
        public void EveryExposureMatchesTheOriginalLightingIncludingTextureOrientation(int width,int height,int stride)
        {
            var cache=new RoomLightingCache(width,height);
            Assert.That(cache.Width,Is.EqualTo(width));
            Assert.That(cache.Height,Is.EqualTo(height));
            Assert.That(cache.Step,Is.EqualTo(-1));
            for(int step=0;step<=RoomLightingCache.Steps;step++)
            {
                float progress=step/(float)RoomLightingCache.Steps;
                Assert.That(cache.TryUpdate(progress),Is.True);
                Assert.That(cache.Step,Is.EqualTo(step));
                for(int y=0;y<height;y+=stride)for(int x=0;x<width;x+=stride)
                {
                    // Reference the original independent evaluators at canvas coordinates.
                    // Row zero is the floor, not the ceiling: Unity textures have a Y flip.
                    Vector2 point=new Vector2((x+0.5f)*1440/width,900-(y+0.5f)*900/height);
                    int index=y*width+x;
                    int shade=Mathf.RoundToInt(RoomAwakening.ShadeAlpha(point,progress)*255);
                    int warm=Mathf.RoundToInt(RoomAwakening.WarmAlpha(point,progress)*255);
                    Color32 actualShade=cache.ShadePixels[index],actualWarm=cache.WarmPixels[index];
                    if(Math.Abs(actualShade.a-shade)>1||Math.Abs(actualWarm.a-warm)>1)
                        Assert.Fail($"Exposure {step}, pixel ({x},{y}) in {width}x{height}: " +
                            $"shade {actualShade.a}/{shade}, warm {actualWarm.a}/{warm}.");
                    if(actualShade.r!=3||actualShade.g!=10||actualShade.b!=17||
                        actualWarm.r!=245||actualWarm.g!=179||actualWarm.b!=86)
                        Assert.Fail("Caching must retain both original overlay colors.");
                }
            }
        }

        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        [TestCase(-1f)]
        [TestCase(float.MinValue)]
        public void InvalidAndNegativeProgressReturnToTheUnlitRoom(float progress)
        {
            var cache=new RoomLightingCache(13,9);
            cache.TryUpdate(0);
            var initialShade=(Color32[])cache.ShadePixels.Clone();
            var initialWarm=(Color32[])cache.WarmPixels.Clone();
            cache.TryUpdate(1);
            Assert.That(cache.TryUpdate(progress),Is.True);
            Assert.That(cache.Step,Is.Zero);
            Assert.That(cache.ShadePixels,Is.EqualTo(initialShade));
            Assert.That(cache.WarmPixels,Is.EqualTo(initialWarm));
            Assert.That(cache.TryUpdate(progress),Is.False);
        }

        [Test]
        public void RepeatedExposureBinsSkipUpdatesAndFiniteProgressClampsToTheEndpoints()
        {
            var cache=new RoomLightingCache(9,7);
            Assert.That(cache.TryUpdate(0.5f),Is.True);
            Assert.That(cache.Step,Is.EqualTo(128));
            Assert.That(cache.TryUpdate(0.5001f),Is.False);
            Assert.That(cache.TryUpdate(0.501f),Is.False);
            Assert.That(cache.TryUpdate(0.503f),Is.True);
            Assert.That(cache.Step,Is.EqualTo(129));
            Assert.That(cache.TryUpdate(float.MaxValue),Is.True);
            Assert.That(cache.Step,Is.EqualTo(256));
            Assert.That(cache.TryUpdate(1f),Is.False);
            Assert.That(cache.TryUpdate(2f),Is.False);
        }

        [Test]
        public void ReverseProgressAndReplayRestorePixelsWithoutReplacingEitherBuffer()
        {
            var cache=new RoomLightingCache(19,13);
            Color32[] shadeBuffer=cache.ShadePixels,warmBuffer=cache.WarmPixels;
            cache.TryUpdate(0);
            var initialShade=(Color32[])shadeBuffer.Clone();
            var initialWarm=(Color32[])warmBuffer.Clone();
            cache.TryUpdate(0.5f);
            var middleShade=(Color32[])shadeBuffer.Clone();
            var middleWarm=(Color32[])warmBuffer.Clone();
            for(int step=129;step<=RoomLightingCache.Steps;step++)
                cache.TryUpdate(step/(float)RoomLightingCache.Steps);
            Assert.That(cache.TryUpdate(0.5f),Is.True);
            Assert.That(cache.ShadePixels,Is.EqualTo(middleShade));
            Assert.That(cache.WarmPixels,Is.EqualTo(middleWarm));
            Assert.That(cache.TryUpdate(0),Is.True);
            Assert.That(cache.ShadePixels,Is.EqualTo(initialShade));
            Assert.That(cache.WarmPixels,Is.EqualTo(initialWarm));
            Assert.That(cache.ShadePixels,Is.SameAs(shadeBuffer));
            Assert.That(cache.WarmPixels,Is.SameAs(warmBuffer));
            Assert.That(cache.TryUpdate(0),Is.False);
        }

        [TestCase(0,10)]
        [TestCase(10,0)]
        [TestCase(-1,10)]
        [TestCase(10,-1)]
        public void InvalidTextureDimensionsAreRejected(int width,int height)
        {
            Assert.Throws<ArgumentOutOfRangeException>(()=>new RoomLightingCache(width,height));
        }
    }
}
