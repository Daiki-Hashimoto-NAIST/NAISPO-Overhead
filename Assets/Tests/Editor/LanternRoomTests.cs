using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Naispo.Overhead.Tests
{
    public sealed class LanternRoomTests
    {
        const BindingFlags Members=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
        GameObject host;
        OverheadApp app;
        GameAudio sounds;

        [SetUp]
        public void SetUp()
        {
            host=new GameObject("Lantern room test");
            host.SetActive(false);
            app=host.AddComponent<OverheadApp>();
            var source=host.AddComponent<OverheadDepthSource>();
            sounds=host.AddComponent<GameAudio>();
            Set(app,"source",source);
            Set(app,"sounds",sounds);
            Set(app,"playTime",100f);
        }

        [TearDown]
        public void TearDown()
        {
            if(host!=null)UnityEngine.Object.DestroyImmediate(host);
        }

        [Test]
        public void MilestoneLightStaysOffUntilArrivalThenFadesAndPersists()
        {
            var garden=GardenAt(10,100f);

            LanternRoom.MilestoneLight launch=LanternRoom.Evaluate(garden,0,100f);
            Assert.That(launch.Started,Is.True);
            Assert.That(launch.Arrived,Is.False);
            Assert.That(launch.TravelT,Is.Zero);
            Assert.That(launch.Brightness,Is.Zero);

            LanternRoom.MilestoneLight mid=LanternRoom.Evaluate(garden,0,100.55f);
            Assert.That(mid.Started,Is.True);
            Assert.That(mid.Arrived,Is.False);
            Assert.That(mid.TravelT,Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(mid.Brightness,Is.Zero,"The target must stay off while the light travels.");

            LanternRoom.MilestoneLight beforeArrival=LanternRoom.Evaluate(garden,0,101.099f);
            Assert.That(beforeArrival.Arrived,Is.False);
            Assert.That(beforeArrival.Brightness,Is.Zero);

            LanternRoom.MilestoneLight atArrival=LanternRoom.Evaluate(garden,0,101.1f);
            Assert.That(atArrival.Arrived,Is.True);
            Assert.That(atArrival.Brightness,Is.Zero,"Arrival itself must not flash the target.");

            LanternRoom.MilestoneLight halfway=LanternRoom.Evaluate(garden,0,101.425f);
            Assert.That(halfway.Brightness,Is.EqualTo(0.5f).Within(0.001f));

            LanternRoom.MilestoneLight lit=LanternRoom.Evaluate(garden,0,101.75f);
            Assert.That(lit.Brightness,Is.EqualTo(1f).Within(0.0001f));

            LanternRoom.MilestoneLight muchLater=LanternRoom.Evaluate(garden,0,999f);
            Assert.That(muchLater.Brightness,Is.EqualTo(1f).Within(0.0001f),
                "A lit fixture must remain lit forever.");
        }

        [Test]
        public void NonFiniteNegativeAndUnearnedMilestonesNeverProduceRewards()
        {
            var garden=GardenAt(10,100f);
            Assert.That(LanternRoom.Evaluate(garden,0,float.NaN).Started,Is.False);
            Assert.That(LanternRoom.Evaluate(garden,0,float.PositiveInfinity).Started,Is.False);
            Assert.That(LanternRoom.Evaluate(garden,0,99f).Started,Is.False,
                "A clock before the milestone is a negative elapsed time.");

            var empty=new GardenProgress();
            Assert.That(LanternRoom.Evaluate(empty,0,100f).Started,Is.False);
            Assert.That(LanternRoom.Evaluate(empty,-1,100f).Started,Is.False);
            Assert.That(LanternRoom.Evaluate(empty,GardenProgress.StageCount,100f).Started,Is.False);
        }

        [Test]
        public void EachMilestoneHasExactlyOneTravelerAndEarlierLightsPersist()
        {
            var garden=GardenAtMilestones(100f,200f,300f);

            AssertTravelerCount(garden,100.5f,1,0);
            AssertTravelerCount(garden,200.5f,1,1);
            AssertTravelerCount(garden,300.5f,1,2);

            Assert.That(LanternRoom.Evaluate(garden,0,101.75f).Brightness,Is.EqualTo(1f).Within(0.0001f));
            Assert.That(LanternRoom.Evaluate(garden,1,201.75f).Brightness,Is.EqualTo(1f).Within(0.0001f));
            Assert.That(LanternRoom.Evaluate(garden,2,301.75f).Brightness,Is.EqualTo(1f).Within(0.0001f));
            for(int stage=0;stage<GardenProgress.StageCount;stage++)
                Assert.That(LanternRoom.Evaluate(garden,stage,1000f).Brightness,Is.EqualTo(1f).Within(0.0001f));
        }

        [Test]
        public void WrongArrivalAndOrdinarySuccessesDoNotStartOrRetriggerRewards()
        {
            AddDelivery(false,0);
            Invoke("UpdateFeedback",0.02f);
            Assert.That(Garden.DeliveredCount,Is.Zero);
            Assert.That(Garden.BloomStartedAt(0),Is.EqualTo(float.NegativeInfinity));
            Assert.That(LanternRoom.Evaluate(Garden,0,101.75f).Started,Is.False,
                "A wrong delivery must not light a reward.");

            FillTo(Garden,9,0);
            AddDelivery(true,0);
            Invoke("UpdateFeedback",0.02f);
            Assert.That(Garden.DeliveredCount,Is.EqualTo(10));
            Assert.That(Garden.BloomStartedAt(0),Is.EqualTo(100f));
            Assert.That(Garden.BloomStartedAt(1),Is.EqualTo(float.NegativeInfinity));

            AddDelivery(true,0);
            Invoke("UpdateFeedback",0.02f);
            Assert.That(Garden.DeliveredCount,Is.EqualTo(11));
            Assert.That(Garden.BloomStartedAt(0),Is.EqualTo(100f));
            Assert.That(Garden.BloomStartedAt(1),Is.EqualTo(float.NegativeInfinity),
                "An ordinary success after a milestone must not create another traveler.");
            Assert.That(LanternRoom.Evaluate(Garden,0,101.75f).Brightness,Is.EqualTo(1f).Within(0.0001f));
        }

        [Test]
        public void RestartExtinguishesEveryRewardAndTravelerButKeepsTheSessionClock()
        {
            FillTo(Garden,30,100f);
            Set(app,"playTime",102f);
            Set(app,"cloudMood",0.8f);
            Set(app,"celebrationAt",100f);
            for(int stage=0;stage<GardenProgress.StageCount;stage++)
                Assert.That(LanternRoom.Evaluate(Garden,stage,102f).Brightness,Is.EqualTo(1f).Within(0.0001f));

            Invoke("ResetRound");

            Assert.That(Get<float>(app,"playTime"),Is.EqualTo(102f),
                "Replay must keep the continuing session clock.");
            Assert.That(Garden.DeliveredCount,Is.Zero);
            Assert.That(Garden.BloomCount,Is.Zero);
            for(int stage=0;stage<GardenProgress.StageCount;stage++)
            {
                Assert.That(Garden.BloomStartedAt(stage),Is.EqualTo(float.NegativeInfinity));
                Assert.That(LanternRoom.Evaluate(Garden,stage,102f).Started,Is.False);
            }
            Assert.That(Get<float>(app,"cloudMood"),Is.Zero);
            Assert.That(Get<float>(app,"celebrationAt"),Is.EqualTo(-100f));
            Assert.That(LanternRoom.LitTint,Is.EqualTo(Color.white),
                "The central lantern stays lit independently of the earned rewards.");
        }

        [TestCase("paused")]
        [TestCase("settingsOpen")]
        public void PauseAndSettingsFreezeTravelSwayBlinkAndMood(string pauseField)
        {
            FillTo(Garden,10,100f);
            Set(app,"playTime",101.265f);
            Set(app,"cloudMood",0.6f);
            Set(app,pauseField,true);
            LanternRoom.MilestoneLight before=LanternRoom.Evaluate(Garden,0,101.265f);
            float swayBefore=LanternRoom.SwayDegrees(101.265f);
            float blinkBefore=LanternRoom.BlinkAmount(101.265f);
            Assert.That(before.Started,Is.True);
            Assert.That(before.Arrived,Is.True);
            Assert.That(before.Brightness,Is.GreaterThan(0));
            Assert.That(swayBefore,Is.Not.Zero);
            Assert.That(blinkBefore,Is.GreaterThan(0.9f));

            for(int frame=0;frame<4;frame++)Invoke("Update");

            Assert.That(Get<float>(app,"playTime"),Is.EqualTo(101.265f));
            LanternRoom.MilestoneLight after=LanternRoom.Evaluate(Garden,0,Get<float>(app,"playTime"));
            Assert.That(after.Elapsed,Is.EqualTo(before.Elapsed));
            Assert.That(after.Brightness,Is.EqualTo(before.Brightness));
            Assert.That(LanternRoom.SwayDegrees(Get<float>(app,"playTime")),Is.EqualTo(swayBefore));
            Assert.That(LanternRoom.BlinkAmount(Get<float>(app,"playTime")),Is.EqualTo(blinkBefore));
            Assert.That(Get<float>(app,"cloudMood"),Is.EqualTo(0.6f));
        }

        [Test]
        public void CompletedGardenFinishesTheArchFadeWithoutTrackedHands()
        {
            FillTo(Garden,9,0);
            Garden.Deliver(90f);
            FillTo(Garden,19,0);
            Garden.Deliver(95f);
            FillTo(Garden,29,0);
            Garden.Deliver(100f);
            Set(app,"live",true);
            Set(app,"ready",false);
            Set(app,"celebrationAt",100f);
            Set(app,"playTime",102f);

            Invoke("Update");

            float time=Get<float>(app,"playTime");
            Assert.That(time,Is.GreaterThan(102f));
            Assert.That(Garden.IsComplete,Is.True);
            Assert.That(Get<bool>(app,"ready"),Is.False);
            for(int stage=0;stage<GardenProgress.StageCount;stage++)
            {
                LanternRoom.MilestoneLight light=LanternRoom.Evaluate(Garden,stage,time);
                Assert.That(light.Started,Is.True);
                Assert.That(light.Arrived,Is.True);
                Assert.That(light.Brightness,Is.EqualTo(1f).Within(0.0001f));
            }
        }

        [Test]
        public void SourceCropAnchorsAndArchGeometryMatchTheMeasuredLantern()
        {
            Assert.That(LanternRoom.Crop,Is.EqualTo(new Rect(145,57,962,1132)));
            Assert.That(LanternRoom.CentralBody.x,Is.EqualTo(650.459f).Within(0.001f));
            Assert.That(LanternRoom.CentralBody.y,Is.EqualTo(48f).Within(0.001f));
            Assert.That(LanternRoom.CentralBody.width,Is.EqualTo(139.371f).Within(0.001f));
            Assert.That(LanternRoom.CentralBody.height,Is.EqualTo(164f).Within(0.001f));
            Assert.That(LanternRoom.CentralBody.yMax,Is.EqualTo(212f).Within(0.001f));

            Assert.That(Vector2.Distance(LanternRoom.SourcePoint(LanternRoom.SourceGlass,LanternRoom.CentralBody),
                LanternRoom.CentralGlass),Is.LessThan(0.001f));
            Assert.That(Vector2.Distance(LanternRoom.SourcePoint(LanternRoom.SourceRing,LanternRoom.CentralBody),
                LanternRoom.CentralRing),Is.LessThan(0.001f));
            Assert.That(Vector2.Distance(LanternRoom.CentralBody.center,LanternRoom.CentralGlass),
                Is.GreaterThan(1f),"The glass anchor must not be the centre of the crop rectangle.");

            Assert.That(LanternRoom.LeftBody.y,Is.EqualTo(340f).Within(0.001f));
            Assert.That(LanternRoom.LeftBody.height,Is.EqualTo(96f).Within(0.001f));
            Assert.That(LanternRoom.LeftBody.width,Is.EqualTo(81.583f).Within(0.001f));
            Assert.That(Vector2.Distance(LanternRoom.GlassPoint(LanternRoom.LeftBody),LanternRoom.LeftGlass),
                Is.LessThan(0.001f));
            Assert.That(Vector2.Distance(LanternRoom.RingPoint(LanternRoom.LeftBody),
                new Vector2(206,341.866f)),Is.LessThan(0.001f));

            Assert.That(LanternRoom.ArchAnchors,Has.Length.EqualTo(6));
            Assert.That(LanternRoom.ArchBodies,Has.Length.EqualTo(6));
            Assert.That(LanternRoom.ArchGlasses,Has.Length.EqualTo(6));
            Assert.That(Vector2.Distance(LanternRoom.ArchGlassTarget,new Vector2(604,154.421f)),
                Is.LessThan(0.01f));
            Assert.That(Vector2.Distance(LanternRoom.GlassPoint(LanternRoom.ArchBodies[2]),
                LanternRoom.ArchGlassTarget),Is.LessThan(0.001f));
        }

        [Test]
        public void LanternCropUvKeepsThePaintedTopAtTheTop()
        {
            Rect uv=(Rect)typeof(OverheadApp)
                .GetMethod("CropUv",BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic)
                .Invoke(null,new object[]{LanternRoom.Crop,1254f,1254f});

            Assert.That(uv.x,Is.EqualTo(145f/1254f).Within(0.000001f));
            Assert.That(uv.y,Is.EqualTo(1f-1189f/1254f).Within(0.000001f));
            Assert.That(uv.width,Is.EqualTo(962f/1254f).Within(0.000001f));
            Assert.That(uv.height,Is.EqualTo(1132f/1254f).Within(0.000001f));
            Assert.That(uv.y,Is.LessThan(uv.yMax),"The crop must not be vertically flipped.");
        }

        [Test]
        public void OneSwayingTransformKeepsTheLampAndGlowBelowTheWarning()
        {
            float peakTime=LanternRoom.SwayPeriod*0.25f;
            Assert.That(Mathf.Abs(LanternRoom.SwayDegrees(peakTime)),Is.EqualTo(LanternRoom.SwayAmplitude).Within(0.0001f));
            Assert.That(Mathf.Abs(LanternRoom.SwayDegrees(peakTime)),Is.LessThan(1f));

            Matrix4x4 transform=LanternRoom.CentralTransform(peakTime);
            Vector2 ceiling=transform.MultiplyPoint3x4(new Vector3(LanternRoom.Ceiling.x,LanternRoom.Ceiling.y,0));
            Assert.That(Vector2.Distance(ceiling,LanternRoom.Ceiling),Is.LessThan(0.0001f),
                "The ceiling attachment must remain fixed while the body sways.");

            float bodyBottom=MaxCornerY(LanternRoom.CentralBody,transform);
            Rect glowRect=new Rect(LanternRoom.CentralGlass.x-48,LanternRoom.CentralGlass.y-54,96,108);
            float glowBottom=MaxCornerY(glowRect,transform);
            Assert.That(bodyBottom,Is.LessThan(220f));
            Assert.That(glowBottom,Is.LessThan(220f));
            Assert.That(bodyBottom,Is.GreaterThan(LanternRoom.CentralBody.yMax-0.01f),
                "The sampled peak must actually rotate the body.");
        }

        [Test]
        public void TravelerLaunchesFromTheSwayingCentralGlassAndReachesItsTarget()
        {
            Vector2 expectedStart=LanternRoom.CentralTransform(100f)
                .MultiplyPoint3x4(new Vector3(LanternRoom.CentralGlass.x,LanternRoom.CentralGlass.y,0));
            Assert.That(Vector2.Distance(LanternRoom.TravelPoint(0,0,100f),expectedStart),Is.LessThan(0.0001f));
            Assert.That(Vector2.Distance(LanternRoom.TravelPoint(0,1,100f),LanternRoom.LeftGlass),Is.LessThan(0.0001f));
            Assert.That(Vector2.Distance(LanternRoom.TravelPoint(1,1,100f),LanternRoom.RightGlass),Is.LessThan(0.0001f));
            Assert.That(Vector2.Distance(LanternRoom.TravelPoint(2,1,100f),LanternRoom.ArchGlassTarget),Is.LessThan(0.0001f));
        }

        [Test]
        public void OffTintIsOpaqueAndBlinkRepeatsOnTheSessionClock()
        {
            Assert.That(LanternRoom.OffTint.a,Is.EqualTo(1f));
            Assert.That(LanternRoom.OffTint.r,Is.EqualTo(0.24f).Within(0.0001f));
            Assert.That(LanternRoom.OffTint.g,Is.EqualTo(0.27f).Within(0.0001f));
            Assert.That(LanternRoom.OffTint.b,Is.EqualTo(0.26f).Within(0.0001f));
            Assert.That(LanternRoom.Tint(0f),Is.EqualTo(LanternRoom.OffTint));
            Assert.That(LanternRoom.Tint(1f),Is.EqualTo(Color.white));

            Assert.That(LanternRoom.BlinkAmount(0.065f),Is.GreaterThan(0.99f));
            Assert.That(LanternRoom.BlinkAmount(LanternRoom.BlinkPeriod+0.065f),Is.GreaterThan(0.99f));
            Assert.That(LanternRoom.BlinkAmount(2.3f),Is.Zero);
            Assert.That(LanternRoom.BlinkAmount(float.NaN),Is.Zero);
        }

        [Test]
        public void SatelliteLightsFollowArrivalInOrderAndStayLit()
        {
            var garden=GardenAt(10,100f);
            float duringCascade=101.4f;
            float primary=RoomAwakening.Reveal(garden,0,duringCascade,0);
            float first=RoomAwakening.Reveal(garden,0,duringCascade,0.16f);
            float second=RoomAwakening.Reveal(garden,0,duringCascade,0.32f);
            Assert.That(primary,Is.GreaterThan(first));
            Assert.That(first,Is.GreaterThan(0));
            Assert.That(second,Is.Zero,"The last lamp must wait for its turn.");
            Assert.That(RoomAwakening.Reveal(garden,1,1000f,0.16f),Is.Zero,
                "A left-side reward must not illuminate the right side.");
            Assert.That(RoomAwakening.Reveal(garden,0,1000f,0.32f),Is.EqualTo(1));
        }

        [Test]
        public void RoomDecorationsResetEvenWhenTheSessionClockContinues()
        {
            FillTo(Garden,30,100f);
            Set(app,"playTime",500f);
            Assert.That(RoomAwakening.Reveal(Garden,2,500f,0.52f),Is.EqualTo(1));
            Invoke("ResetRound");
            float time=Get<float>(app,"playTime");
            Assert.That(time,Is.EqualTo(500f));
            for(int stage=0;stage<3;stage++)
            {
                Assert.That(RoomAwakening.Reveal(Garden,stage,time,0.32f),Is.Zero);
                Assert.That(RoomAwakening.ArrivalPulse(Garden,stage,time),Is.Zero);
            }
        }

        [Test]
        public void ArrivalPulseEndsWhileTheEarnedRoomLightRemains()
        {
            var garden=GardenAt(10,100f);
            Assert.That(RoomAwakening.ArrivalPulse(garden,0,100.8f),Is.Zero);
            Assert.That(RoomAwakening.ArrivalPulse(garden,0,101.775f),Is.GreaterThan(0.99f));
            Assert.That(RoomAwakening.ArrivalPulse(garden,0,102.5f),Is.Zero);
            Assert.That(RoomAwakening.Reveal(garden,0,102.5f,0),Is.EqualTo(1));
        }

        GardenProgress Garden => Get<GardenProgress>(app,"garden");

        static GardenProgress GardenAt(int count,float time)
        {
            var garden=new GardenProgress();
            FillTo(garden,count,time);
            return garden;
        }

        static GardenProgress GardenAtMilestones(float first,float second,float third)
        {
            var garden=new GardenProgress();
            FillTo(garden,9,0);
            garden.Deliver(first);
            FillTo(garden,19,0);
            garden.Deliver(second);
            FillTo(garden,29,0);
            garden.Deliver(third);
            return garden;
        }

        static void FillTo(GardenProgress garden,int count,float time)
        {
            while(garden.DeliveredCount<count)garden.Deliver(time);
        }

        static void AssertTravelerCount(GardenProgress garden,float time,int expectedCount,int expectedStage)
        {
            int count=0,stage=-1;
            for(int i=0;i<GardenProgress.StageCount;i++)
            {
                LanternRoom.MilestoneLight light=LanternRoom.Evaluate(garden,i,time);
                if(!light.Started||light.Arrived)continue;
                count++;
                stage=i;
            }
            Assert.That(count,Is.EqualTo(expectedCount));
            Assert.That(stage,Is.EqualTo(expectedStage));
        }

        static float MaxCornerY(Rect rect,Matrix4x4 transform)
        {
            float maximum=float.NegativeInfinity;
            maximum=Mathf.Max(maximum,transform.MultiplyPoint3x4(new Vector3(rect.x,rect.y,0)).y);
            maximum=Mathf.Max(maximum,transform.MultiplyPoint3x4(new Vector3(rect.xMax,rect.y,0)).y);
            maximum=Mathf.Max(maximum,transform.MultiplyPoint3x4(new Vector3(rect.x,rect.yMax,0)).y);
            maximum=Mathf.Max(maximum,transform.MultiplyPoint3x4(new Vector3(rect.xMax,rect.yMax,0)).y);
            return maximum;
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
