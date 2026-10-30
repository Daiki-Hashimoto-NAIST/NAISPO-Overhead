using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Naispo.Overhead.Tests
{
    public sealed class GardenAtmosphereTests
    {
        const BindingFlags Members=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
        GameObject host;
        OverheadApp app;

        [SetUp]
        public void SetUp()
        {
            // Keep artwork, device capture and audio inactive while exercising the round clock.
            host=new GameObject("Garden atmosphere test");
            host.SetActive(false);
            app=host.AddComponent<OverheadApp>();
            Set(app,"source",host.AddComponent<OverheadDepthSource>());
            Set(app,"sounds",host.AddComponent<GameAudio>());
            Set(app,"playTime",100f);
        }

        [TearDown]
        public void TearDown()
        {
            if(host!=null)UnityEngine.Object.DestroyImmediate(host);
        }

        [Test]
        public void NoLightsKeepTheRoomDarkAndCalm()
        {
            var garden=new GardenProgress();
            foreach(float time in new[]{0f,100f,10000f})
            {
                Assert.That(GardenAtmosphere.Progress(garden,time),Is.Zero);
                Assert.That(GardenAtmosphere.WallWarmth(garden,time),Is.Zero);
                Assert.That(GardenAtmosphere.FloorWarmth(garden,time),Is.Zero);
                Assert.That(GardenAtmosphere.WindowReveal(garden,time),Is.Zero);
                foreach(float x in new[]{0f,720f,1440f})
                    Assert.That(GardenAtmosphere.BreezeAngle(garden,time,x),Is.Zero);
            }
        }

        [Test]
        public void EachSuccessAddsOneTwentiethOnlyAfterItsFlightAndFade()
        {
            var garden=new GardenProgress();
            Assert.That(garden.Deliver(100f,false,new Vector2(500,700)),Is.True);
            float arrival=100f+GardenProgress.LightFlightDuration;

            Assert.That(GardenAtmosphere.Progress(garden,100f),Is.Zero);
            Assert.That(GardenAtmosphere.Progress(garden,arrival-0.001f),Is.Zero,
                "The light must not contribute before it reaches the central lantern.");
            Assert.That(GardenAtmosphere.Progress(garden,arrival),Is.Zero,
                "Arrival itself must not flash the room.");
            Assert.That(GardenAtmosphere.Progress(garden,arrival+0.325f),
                Is.EqualTo(0.5f/GardenProgress.Goal).Within(0.0001f));
            Assert.That(GardenAtmosphere.Progress(garden,arrival+LanternRoom.FadeDuration),
                Is.EqualTo(1f/GardenProgress.Goal).Within(0.0001f));
            Assert.That(GardenAtmosphere.Progress(garden,999f),
                Is.EqualTo(1f/GardenProgress.Goal).Within(0.0001f),
                "A past contribution must be retained after its flight has finished.");
        }

        [Test]
        public void NineteenLightsApproachFullAndTheTwentiethCompletesTheRoom()
        {
            var garden=new GardenProgress();
            FillTo(garden,19,100f);

            Assert.That(garden.IsComplete,Is.False);
            Assert.That(GardenAtmosphere.Progress(garden,1000f),
                Is.EqualTo(19f/GardenProgress.Goal).Within(0.0001f));

            Assert.That(garden.Deliver(200f,true,new Vector2(900,700)),Is.True);
            Assert.That(garden.IsComplete,Is.True);
            Assert.That(GardenAtmosphere.Progress(garden,1000f),Is.EqualTo(1f).Within(0.0001f));
        }

        [Test]
        public void OverlappingFlightsKeepBothContributions()
        {
            var garden=new GardenProgress();
            garden.Deliver(100f,false,new Vector2(500,700));
            garden.Deliver(100.3f,true,new Vector2(900,700));

            Assert.That(GardenAtmosphere.Progress(garden,100.5f),Is.Zero);
            float partial=GardenAtmosphere.Progress(garden,101.75f);
            Assert.That(partial,Is.GreaterThan(1f/GardenProgress.Goal));
            Assert.That(partial,Is.LessThan(2f/GardenProgress.Goal));
            Assert.That(GardenAtmosphere.Progress(garden,102.05f),
                Is.EqualTo(2f/GardenProgress.Goal).Within(0.0001f));
        }

        [Test]
        public void ProgressIsMonotonicAndStaysWithinZeroAndOne()
        {
            var garden=new GardenProgress();
            FillTo(garden,GardenProgress.Goal,100f);
            float previous=0;
            for(int step=0;step<=200;step++)
            {
                float value=GardenAtmosphere.Progress(garden,99f+step*0.02f);
                Assert.That(value,Is.InRange(previous,1f));
                previous=value;
            }
            Assert.That(previous,Is.EqualTo(1f).Within(0.0001f));
        }

        [Test]
        public void EitherSideAloneEventuallyLightsTheWholeRoom()
        {
            var left=new GardenProgress();
            var right=new GardenProgress();
            FillTo(left,GardenProgress.Goal,100f,false);
            FillTo(right,GardenProgress.Goal,100f,true);

            foreach(var garden in new[]{left,right})
            {
                Assert.That(garden.IsComplete,Is.True);
                Assert.That(GardenAtmosphere.Progress(garden,1000f),Is.EqualTo(1f).Within(0.0001f));
                Assert.That(GardenAtmosphere.WallWarmth(garden,1000f),Is.EqualTo(1f).Within(0.0001f));
                Assert.That(GardenAtmosphere.FloorWarmth(garden,1000f),Is.EqualTo(1f).Within(0.0001f));
                Assert.That(GardenAtmosphere.WindowReveal(garden,1000f),Is.EqualTo(1f).Within(0.0001f));
            }
        }

        [Test]
        public void WallFloorAndWindowFollowTheDelayedProgress()
        {
            var garden=new GardenProgress();
            FillTo(garden,GardenProgress.Goal,100f);
            const float time=101.4f;

            float progress=GardenAtmosphere.Progress(garden,time);
            Assert.That(progress,Is.GreaterThan(0).And.LessThan(1));
            Assert.That(GardenAtmosphere.Warmth(garden,time),Is.EqualTo(progress));
            Assert.That(GardenAtmosphere.WallWarmth(garden,time),
                Is.EqualTo(GardenAtmosphere.Progress(garden,time-GardenAtmosphere.WallDelay)));
            Assert.That(GardenAtmosphere.FloorWarmth(garden,time),
                Is.EqualTo(GardenAtmosphere.Progress(garden,time-GardenAtmosphere.FloorDelay)));
            Assert.That(GardenAtmosphere.WindowReveal(garden,time),
                Is.EqualTo(GardenAtmosphere.WallWarmth(garden,time)*GardenAtmosphere.WallWarmth(garden,time)));
        }

        [Test]
        public void SleepingRoomKeepsTheNightSkyVisibleWhileTheWoodAndFloorAreDark()
        {
            float wood=RoomAwakening.ShadeAlpha(new Vector2(270,330),0);
            Assert.That(wood,Is.GreaterThan(0.65f));
            Assert.That(RoomAwakening.ShadeAlpha(RoomAwakening.FloorCenter,0),Is.GreaterThan(0.65f));
            Assert.That(RoomAwakening.ShadeAlpha(RoomAwakening.MoonCenter,0),Is.LessThan(0.30f),
                "The painted moon should still read as a light source before the first delivery.");
            Assert.That(RoomAwakening.ShadeAlpha(new Vector2(720,400),0),Is.LessThan(wood-0.35f),
                "The exterior sky and unlit interior must read as separate spaces.");
        }

        [Test]
        public void AwakenedRoomStronglyRevealsItsWoodAndFloor()
        {
            foreach(Vector2 interior in new[]{new Vector2(270,330),new Vector2(1170,330),
                RoomAwakening.FloorCenter,new Vector2(430,780),new Vector2(1010,780)})
            {
                float before=RoomAwakening.ShadeAlpha(interior,0);
                float after=RoomAwakening.ShadeAlpha(interior,1);
                Assert.That(before-after,Is.GreaterThan(0.4f),
                    "The completed room needs an immediately visible brightness difference at "+interior);
                Assert.That(RoomAwakening.WarmAlpha(interior,1),Is.GreaterThan(0));
            }
        }

        [Test]
        public void LightingTheRoomDoesNotWarmOrBrightenTheExteriorSkyAndMoon()
        {
            foreach(Vector2 exterior in new[]{RoomAwakening.MoonCenter,new Vector2(600,250),
                new Vector2(720,400),new Vector2(850,500),new Vector2(720,545)})
            {
                float night=RoomAwakening.ShadeAlpha(exterior,0);
                Assert.That(RoomAwakening.InteriorWeight(exterior),Is.LessThan(0.01f));
                for(int step=0;step<=20;step++)
                {
                    float progress=step/20f;
                    Assert.That(RoomAwakening.ShadeAlpha(exterior,progress),Is.EqualTo(night).Within(0.005f),
                        "The sky through the glass should remain a blue night as the interior lights up.");
                    Assert.That(RoomAwakening.WarmAlpha(exterior,progress),Is.LessThan(0.005f));
                }
            }
        }

        [Test]
        public void RoomShadeWaitsForLightArrivalAndResetsWithTheRound()
        {
            var garden=new GardenProgress();
            Vector2 wall=new Vector2(270,330);
            float before=RoomAwakening.ShadeAlpha(wall,GardenAtmosphere.WallWarmth(garden,100));
            FillTo(garden,8,100);
            Assert.That(RoomAwakening.ShadeAlpha(wall,GardenAtmosphere.WallWarmth(garden,100.5f)),
                Is.EqualTo(before),"A travelling light should not brighten the room prematurely.");
            Assert.That(RoomAwakening.ShadeAlpha(wall,GardenAtmosphere.WallWarmth(garden,103)),
                Is.LessThan(before));
            garden.Reset();
            Assert.That(RoomAwakening.ShadeAlpha(wall,GardenAtmosphere.WallWarmth(garden,103)),
                Is.EqualTo(before));
        }

        [Test]
        public void RoomShadeAndWarmthStayBoundedAndNeverReverseAsMoreLightsArrive()
        {
            for(int y=0;y<=900;y+=90)for(int x=0;x<=1440;x+=120)
            {
                Vector2 point=new Vector2(x,y);
                float previous=1,previousWarmth=0;
                Assert.That(RoomAwakening.InteriorWeight(point),Is.InRange(0f,1f));
                for(int step=0;step<=20;step++)
                {
                    float shade=RoomAwakening.ShadeAlpha(point,step/20f);
                    float warmth=RoomAwakening.WarmAlpha(point,step/20f);
                    Assert.That(shade,Is.InRange(0f,1f));
                    Assert.That(shade,Is.LessThanOrEqualTo(previous+0.00001f));
                    Assert.That(warmth,Is.InRange(previousWarmth-0.00001f,1f));
                    previous=shade;
                    previousWarmth=warmth;
                }
            }
        }

        [Test]
        public void RoomShadeTreatsInvalidProgressAsUnlitAndRejectsInvalidLocations()
        {
            Vector2 wall=new Vector2(270,330);
            float unlit=RoomAwakening.ShadeAlpha(wall,0);
            foreach(float value in new[]{float.NaN,float.PositiveInfinity,float.NegativeInfinity,-10f})
            {
                Assert.That(RoomAwakening.ShadeAlpha(wall,value),Is.EqualTo(unlit));
                Assert.That(RoomAwakening.WarmAlpha(wall,value),Is.Zero);
            }
            Assert.That(RoomAwakening.ShadeAlpha(wall,10),Is.EqualTo(RoomAwakening.ShadeAlpha(wall,1)));
            Assert.That(RoomAwakening.WarmAlpha(wall,10),Is.EqualTo(RoomAwakening.WarmAlpha(wall,1)));
            foreach(Vector2 point in new[]{new Vector2(float.NaN,400),new Vector2(200,float.PositiveInfinity)})
            {
                Assert.That(RoomAwakening.ShadeAlpha(point,1),Is.InRange(0f,1f));
                Assert.That(RoomAwakening.InteriorWeight(point),Is.Zero);
                Assert.That(RoomAwakening.WarmAlpha(point,1),Is.Zero);
            }
        }

        [Test]
        public void GustsTravelLeftToRightAndLeavePeriodsOfCompleteCalm()
        {
            var garden=new GardenProgress();
            FillTo(garden,GardenProgress.Goal,100f);
            float[] positions={0,720,1440};
            float[] firstMotion={float.PositiveInfinity,float.PositiveInfinity,float.PositiveInfinity};
            int calmSamples=0,longestCalm=0,gusts=0;
            bool previouslyMoving=false;
            for(int step=0;step<=500;step++)
            {
                float time=100+step*0.1f;
                bool moving=false;
                for(int i=0;i<positions.Length;i++)
                {
                    float value=GardenAtmosphere.BreezeAngle(garden,time,positions[i]);
                    Assert.That(value,Is.InRange(0f,5f));
                    Assert.That(GardenAtmosphere.BreezeAngle(garden,time,positions[i]),Is.EqualTo(value),
                        "Repeated GUI passes must not advance the breeze.");
                    if(value>0.001f)
                    {
                        if(float.IsPositiveInfinity(firstMotion[i]))firstMotion[i]=time;
                        moving=true;
                    }
                }
                if(moving&&!previouslyMoving)gusts++;
                calmSamples=moving ? 0 : calmSamples+1;
                longestCalm=Mathf.Max(longestCalm,calmSamples);
                previouslyMoving=moving;
            }
            Assert.That(firstMotion[0],Is.LessThan(firstMotion[1]));
            Assert.That(firstMotion[1],Is.LessThan(firstMotion[2]));
            Assert.That(firstMotion[2],Is.LessThan(float.PositiveInfinity));
            Assert.That(gusts,Is.GreaterThanOrEqualTo(2),"The breeze should return occasionally.");
            Assert.That(longestCalm,Is.GreaterThanOrEqualTo(40),"Flowers need a visible rest between gusts.");
        }

        [TestCase("paused")]
        [TestCase("settingsOpen")]
        public void PausingTheRoundFreezesBothTheWarmingBackgroundAndAnActiveGust(string pauseField)
        {
            FillTo(Garden,11,99f);
            FillTo(Garden,GardenProgress.Goal,104f);
            Set(app,"playTime",104.5f);
            float warmth=GardenAtmosphere.Progress(Garden,104.5f);
            float breeze=GardenAtmosphere.BreezeAngle(Garden,104.5f,0);
            Assert.That(warmth,Is.GreaterThan(0).And.LessThan(1));
            Assert.That(breeze,Is.GreaterThan(0));
            Set(app,pauseField,true);

            for(int frame=0;frame<4;frame++)Invoke("Update");

            float stoppedTime=Get<float>(app,"playTime");
            Assert.That(stoppedTime,Is.EqualTo(104.5f));
            Assert.That(GardenAtmosphere.Progress(Garden,stoppedTime),Is.EqualTo(warmth));
            Assert.That(GardenAtmosphere.BreezeAngle(Garden,stoppedTime,0),Is.EqualTo(breeze));
        }

        [Test]
        public void RestartClearsTheAtmosphereEvenThoughTheSessionClockContinues()
        {
            FillTo(Garden,GardenProgress.Goal,100f);
            Set(app,"playTime",104.5f);
            Assert.That(GardenAtmosphere.Progress(Garden,104.5f),Is.GreaterThan(0));
            Assert.That(GardenAtmosphere.BreezeAngle(Garden,104.5f,0),Is.GreaterThan(0));

            Invoke("ResetRound");

            float time=Get<float>(app,"playTime");
            Assert.That(time,Is.EqualTo(104.5f));
            Assert.That(GardenAtmosphere.Progress(Garden,time),Is.Zero);
            Assert.That(GardenAtmosphere.BreezeAngle(Garden,time,0),Is.Zero);
            FillTo(Garden,GardenProgress.Goal,time);
            Assert.That(GardenAtmosphere.Progress(Garden,time),Is.Zero);
            Assert.That(GardenAtmosphere.BreezeAngle(Garden,time,0),Is.Zero,
                "The next round must earn its own breeze delay.");
            Assert.That(GardenAtmosphere.Progress(Garden,time+5),Is.GreaterThan(0));
            Assert.That(GardenAtmosphere.BreezeAngle(Garden,time+5,0),Is.GreaterThan(0));
        }

        [Test]
        public void FinalCelebrationWaitsForTheLastArrivalAndThenEnds()
        {
            FillTo(Garden,19,100f);
            Set(app,"phase",GardenPhase.Playing);
            // The delivery starts before the current clock so the approach has already
            // finished: playTime 100 means age 1.0s, past the 0.42s mouth-reaching flight.
            AddDelivery(true,99f);
            Invoke("UpdateFeedback",0.02f);

            Assert.That(Garden.DeliveredCount,Is.EqualTo(GardenProgress.Goal));
            Assert.That(Get<GardenPhase>(app,"phase"),Is.EqualTo(GardenPhase.Awakening));
            Assert.That(Celebrating,Is.False,"The twentieth success must not start the finale.");
            Assert.That(Get<bool>(app,"finalSoundPlayed"),Is.False);

            Set(app,"playTime",Garden.FinalArrivalAt-0.001f);
            Invoke("UpdateFeedback",0.02f);
            Assert.That(Celebrating,Is.False);
            Assert.That(Get<bool>(app,"finalSoundPlayed"),Is.False);

            Set(app,"playTime",Garden.FinalArrivalAt);
            Invoke("UpdateFeedback",0.02f);
            Assert.That(Celebrating,Is.True);
            Assert.That(Get<bool>(app,"finalSoundPlayed"),Is.True);
            Invoke("UpdateFeedback",0.02f);
            Assert.That(Get<bool>(app,"finalSoundPlayed"),Is.True,
                "The blessing sound must be requested only once.");

            Set(app,"playTime",Garden.FinalArrivalAt+GardenAtmosphere.FinaleDuration);
            Invoke("UpdateFeedback",0.02f);
            Assert.That(Celebrating,Is.False);
            Assert.That(Get<GardenPhase>(app,"phase"),Is.EqualTo(GardenPhase.Complete));
        }

        GardenProgress Garden => Get<GardenProgress>(app,"garden");
        bool Celebrating => (bool)typeof(OverheadApp).GetProperty("Celebrating",Members).GetValue(app);

        static void FillTo(GardenProgress garden,int count,float time,bool right=false)
        {
            if(count<0||count>GardenProgress.MaxLights)
                throw new ArgumentOutOfRangeException(nameof(count),"Tests may fill at most one goal.");
            while(garden.DeliveredCount<count)
                garden.Deliver(time,right,new Vector2(right ? 900f : 500f,700f));
        }

        void AddDelivery(bool correct,float startedAt)
        {
            Type type=typeof(OverheadApp).GetNestedType("Delivery",BindingFlags.NonPublic);
            object delivery=Activator.CreateInstance(type,true);
            Set(delivery,"Correct",correct);
            Set(delivery,"StartedAt",startedAt);
            Get<IList>(app,"deliveries").Add(delivery);
        }

        void Invoke(string method,params object[] arguments) =>
            typeof(OverheadApp).GetMethod(method,Members).Invoke(app,arguments);

        static T Get<T>(object instance,string field) =>
            (T)instance.GetType().GetField(field,Members).GetValue(instance);

        static void Set(object instance,string field,object value) =>
            instance.GetType().GetField(field,Members).SetValue(instance,value);
    }
}
