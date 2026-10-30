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
            // Inactive components exercise the real round methods; feedback only runs in play.
            Set(app,"phase",GardenPhase.Playing);
        }

        [TearDown]
        public void TearDown()
        {
            if(host!=null)UnityEngine.Object.DestroyImmediate(host);
        }

        [Test]
        public void EveryLightStaysOffUntilArrivalThenFadesAndPersists()
        {
            var garden=GardenAt(1,100f);

            LanternRoom.LightFlight launch=LanternRoom.Evaluate(garden,0,100f);
            Assert.That(launch.Started,Is.True);
            Assert.That(launch.Arrived,Is.False);
            Assert.That(launch.TravelT,Is.Zero);
            Assert.That(launch.Brightness,Is.Zero);

            LanternRoom.LightFlight mid=LanternRoom.Evaluate(garden,0,100.55f);
            Assert.That(mid.Started,Is.True);
            Assert.That(mid.Arrived,Is.False);
            Assert.That(mid.TravelT,Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(mid.Brightness,Is.Zero,"The target must stay off while the light travels.");

            LanternRoom.LightFlight beforeArrival=LanternRoom.Evaluate(garden,0,101.099f);
            Assert.That(beforeArrival.Arrived,Is.False);
            Assert.That(beforeArrival.Brightness,Is.Zero);

            LanternRoom.LightFlight atArrival=LanternRoom.Evaluate(garden,0,101.1f);
            Assert.That(atArrival.Arrived,Is.True);
            Assert.That(atArrival.Brightness,Is.Zero,"Arrival itself must not flash the target.");

            LanternRoom.LightFlight halfway=LanternRoom.Evaluate(garden,0,101.425f);
            Assert.That(halfway.Brightness,Is.EqualTo(0.5f).Within(0.001f));

            LanternRoom.LightFlight lit=LanternRoom.Evaluate(garden,0,101.75f);
            Assert.That(lit.Brightness,Is.EqualTo(1f).Within(0.0001f));

            LanternRoom.LightFlight muchLater=LanternRoom.Evaluate(garden,0,999f);
            Assert.That(muchLater.Brightness,Is.EqualTo(1f).Within(0.0001f),
                "A lit fixture must remain lit forever.");
        }

        [Test]
        public void NonFiniteNegativeAndUnearnedLightsNeverProduceRewards()
        {
            var garden=GardenAt(10,100f);
            Assert.That(LanternRoom.Evaluate(garden,0,float.NaN).Started,Is.False);
            Assert.That(LanternRoom.Evaluate(garden,0,float.PositiveInfinity).Started,Is.False);
            Assert.That(LanternRoom.Evaluate(garden,0,99f).Started,Is.False,
                "A clock before the delivery is a negative elapsed time.");

            var empty=new GardenProgress();
            Assert.That(LanternRoom.Evaluate(empty,0,100f).Started,Is.False);
            Assert.That(LanternRoom.Evaluate(empty,-1,100f).Started,Is.False);
            Assert.That(LanternRoom.Evaluate(empty,GardenProgress.Goal,100f).Started,Is.False);
            Assert.That(LanternRoom.Evaluate(garden,garden.LightCount,100f).Started,Is.False,
                "Only earned successes may start a light.");
        }

        [Test]
        public void EachSuccessHasItsOwnTravelerAndEarlierLightsPersist()
        {
            var garden=new GardenProgress();
            garden.Deliver(100f,false,new Vector2(500,700));
            garden.Deliver(200f,true,new Vector2(900,700));
            garden.Deliver(300f,false,new Vector2(500,700));

            AssertTravelerCount(garden,100.5f,1,0);
            AssertTravelerCount(garden,200.5f,1,1);
            AssertTravelerCount(garden,300.5f,1,2);

            Assert.That(LanternRoom.Evaluate(garden,0,101.75f).Brightness,Is.EqualTo(1f).Within(0.0001f));
            Assert.That(LanternRoom.Evaluate(garden,1,201.75f).Brightness,Is.EqualTo(1f).Within(0.0001f));
            Assert.That(LanternRoom.Evaluate(garden,2,301.75f).Brightness,Is.EqualTo(1f).Within(0.0001f));
            for(int i=0;i<garden.LightCount;i++)
                Assert.That(LanternRoom.Evaluate(garden,i,1000f).Brightness,Is.EqualTo(1f).Within(0.0001f));
        }

        [Test]
        public void WrongDeliveryDoesNotRecordAndEverySuccessGetsItsOwnLight()
        {
            AddDelivery(false,0);
            Invoke("UpdateFeedback",0.02f);
            Assert.That(Garden.DeliveredCount,Is.Zero);
            Assert.That(Garden.LightCount,Is.Zero);
            Assert.That(Get<int>(app,"misses"),Is.EqualTo(1));
            Assert.That(LanternRoom.Evaluate(Garden,0,101.75f).Started,Is.False,
                "A wrong delivery must not light a reward.");

            FillTo(Garden,9,0);
            AddDelivery(true,0);
            Invoke("UpdateFeedback",0.02f);
            Assert.That(Garden.DeliveredCount,Is.EqualTo(10));
            Assert.That(Garden.LightCount,Is.EqualTo(10));
            Assert.That(Garden.LightAt(9).Time,Is.EqualTo(100f));
            Assert.That(LanternRoom.Evaluate(Garden,9,100.5f).Started,Is.True);

            AddDelivery(true,0);
            Invoke("UpdateFeedback",0.02f);
            Assert.That(Garden.DeliveredCount,Is.EqualTo(11));
            Assert.That(Garden.LightCount,Is.EqualTo(11));
            Assert.That(Garden.LightAt(10).Time,Is.EqualTo(100f));
            Assert.That(LanternRoom.Evaluate(Garden,10,100.5f).Started,Is.True,
                "Every success owns a light; ordinary successes no longer wait for a milestone.");
            Assert.That(Garden.FinalArrivalAt,Is.EqualTo(float.NegativeInfinity));
        }

        [Test]
        public void RestartExtinguishesEveryRewardAndTravelerButKeepsTheSessionClock()
        {
            FillTo(Garden,GardenProgress.Goal,100f);
            Set(app,"playTime",102f);
            Set(app,"cloudMood",0.8f);
            Set(app,"phase",GardenPhase.Awakening);
            for(int i=0;i<Garden.LightCount;i++)
                Assert.That(LanternRoom.Evaluate(Garden,i,102f).Brightness,Is.EqualTo(1f).Within(0.0001f));

            Invoke("ResetRound");

            Assert.That(Get<float>(app,"playTime"),Is.EqualTo(102f),
                "Replay must keep the continuing session clock.");
            Assert.That(Garden.DeliveredCount,Is.Zero);
            Assert.That(Garden.LightCount,Is.Zero);
            Assert.That(Garden.IsComplete,Is.False);
            Assert.That(Garden.FinalArrivalAt,Is.EqualTo(float.NegativeInfinity));
            for(int i=0;i<GardenProgress.Goal;i++)
                Assert.That(LanternRoom.Evaluate(Garden,i,102f).Started,Is.False);
            Assert.That(GardenAtmosphere.Progress(Garden,102f),Is.Zero);
            Assert.That(Get<float>(app,"cloudMood"),Is.Zero);
            Assert.That(Get<GardenPhase>(app,"phase"),Is.EqualTo(GardenPhase.Intro));
            Assert.That(Get<bool>(app,"finalSoundPlayed"),Is.False);
        }

        [TestCase("paused")]
        [TestCase("settingsOpen")]
        public void PauseAndSettingsFreezeTravelSwayBlinkAndMood(string pauseField)
        {
            FillTo(Garden,1,100f);
            Set(app,"playTime",101.265f);
            Set(app,"cloudMood",0.6f);
            Set(app,pauseField,true);
            LanternRoom.LightFlight before=LanternRoom.Evaluate(Garden,0,101.265f);
            float swayBefore=LanternRoom.SwayDegrees(101.265f);
            float blinkBefore=LanternRoom.BlinkAmount(101.265f);
            Assert.That(before.Started,Is.True);
            Assert.That(before.Arrived,Is.True);
            Assert.That(before.Brightness,Is.GreaterThan(0));
            Assert.That(swayBefore,Is.Not.Zero);
            Assert.That(blinkBefore,Is.GreaterThan(0.9f));

            for(int frame=0;frame<4;frame++)Invoke("Update");

            Assert.That(Get<float>(app,"playTime"),Is.EqualTo(101.265f));
            LanternRoom.LightFlight after=LanternRoom.Evaluate(Garden,0,Get<float>(app,"playTime"));
            Assert.That(after.Elapsed,Is.EqualTo(before.Elapsed));
            Assert.That(after.Brightness,Is.EqualTo(before.Brightness));
            Assert.That(LanternRoom.SwayDegrees(Get<float>(app,"playTime")),Is.EqualTo(swayBefore));
            Assert.That(LanternRoom.BlinkAmount(Get<float>(app,"playTime")),Is.EqualTo(blinkBefore));
            Assert.That(Get<float>(app,"cloudMood"),Is.EqualTo(0.6f));
        }

        [Test]
        public void CompletedGardenFinishesTheRoomFadeWithoutTrackedHands()
        {
            FillTo(Garden,19,0);
            Garden.Deliver(100f,false,new Vector2(500,700));
            Set(app,"phase",GardenPhase.Awakening);
            Set(app,"live",true);
            Set(app,"ready",false);
            Set(app,"playTime",102f);

            Invoke("Update");

            float time=Get<float>(app,"playTime");
            Assert.That(time,Is.GreaterThan(102f));
            Assert.That(Garden.IsComplete,Is.True);
            Assert.That(Get<bool>(app,"ready"),Is.False);
            for(int i=0;i<Garden.LightCount;i++)
            {
                LanternRoom.LightFlight light=LanternRoom.Evaluate(Garden,i,time);
                Assert.That(light.Started,Is.True);
                Assert.That(light.Arrived,Is.True);
                Assert.That(light.Brightness,Is.EqualTo(1f).Within(0.0001f));
            }
        }

        [Test]
        public void SourceCropAnchorsMatchTheMeasuredLanternAndKeepTheFaceInTheGlass()
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

            Assert.That(LanternRoom.CentralBody.Contains(LanternRoom.CentralGlass),Is.True);
            Assert.That(LanternRoom.CentralRing.y,Is.LessThan(LanternRoom.CentralGlass.y));
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
        public void BothSpiritsSendTheirLightToTheMovingCentralGlass()
        {
            var garden=new GardenProgress();
            garden.Deliver(100f,false,new Vector2(500,700));
            garden.Deliver(100f,true,new Vector2(900,700));
            foreach(float time in new[]{0f,LanternRoom.SwayPeriod*0.25f,LanternRoom.SwayPeriod*0.75f})
            foreach(float progress in new[]{0f,0.5f,0.95f,1f})
            {
                Vector2 glass=LanternRoom.CentralTransform(time,progress).MultiplyPoint3x4(
                    new Vector3(LanternRoom.CentralGlass.x,LanternRoom.CentralGlass.y,0));
                Assert.That(Vector2.Distance(LanternRoom.CentralGlassAt(time,progress),glass),Is.LessThan(0.0001f));
                for(int index=0;index<2;index++)
                {
                    Vector2 source=garden.LightSource(index);
                    Assert.That(Vector2.Distance(LanternRoom.TravelPoint(garden,index,0,time,progress),source),
                        Is.LessThan(0.0001f),"The light must leave the recorded mouth, even while the lamp moves.");
                    Assert.That(Vector2.Distance(LanternRoom.TravelPoint(garden,index,1,time,progress),glass),
                        Is.LessThan(0.0001f),"Both foods feed the central lantern, including at peak sway and final lift.");
                }
            }
        }

        [Test]
        public void FinalLanternLiftIsVisibleAndKeepsItsBodyClearOfTheHudAndFood()
        {
            foreach(float time in new[]{0f,LanternRoom.SwayPeriod*0.25f,LanternRoom.SwayPeriod*0.75f})
            {
                Vector2 resting=LanternRoom.CentralGlassAt(time,0);
                Vector2 risen=LanternRoom.CentralGlassAt(time,1);
                Assert.That(resting.y-risen.y,Is.InRange(10f,18f),"The final lift should be visible without moving into the HUD.");
                foreach(float progress in new[]{0.5f,19f/GardenProgress.Goal})
                    Assert.That(Vector2.Distance(resting,LanternRoom.CentralGlassAt(time,progress)),Is.LessThan(0.0001f),
                        "The midpoint wakes the lamp; its final lift waits for the twentieth arrival.");
                Matrix4x4 transform=LanternRoom.CentralTransform(time,1);
                foreach(Vector2 corner in new[]{LanternRoom.CentralBody.min,LanternRoom.CentralBody.max,
                    new Vector2(LanternRoom.CentralBody.xMax,LanternRoom.CentralBody.y),
                    new Vector2(LanternRoom.CentralBody.x,LanternRoom.CentralBody.yMax)})
                {
                    Vector2 moved=transform.MultiplyPoint3x4(new Vector3(corner.x,corner.y,0));
                    Assert.That(moved.x,Is.InRange(610f,830f));
                    Assert.That(moved.y,Is.InRange(20f,220f));
                }
            }
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
        public void RoomRevealFollowsArrivalWithDelaysAndStaysLit()
        {
            var garden=GardenAt(GardenProgress.Goal,100f);
            float duringCascade=101.4f;
            float primary=RoomAwakening.Reveal(garden,duringCascade,0);
            float first=RoomAwakening.Reveal(garden,duringCascade,0.16f);
            float second=RoomAwakening.Reveal(garden,duringCascade,0.32f);
            Assert.That(primary,Is.GreaterThan(first));
            Assert.That(first,Is.GreaterThan(0));
            Assert.That(second,Is.Zero,"The last lamp must wait for its turn.");
            Assert.That(RoomAwakening.Reveal(garden,1000f,0),Is.EqualTo(1));
            Assert.That(RoomAwakening.Reveal(garden,1000f,0.16f),Is.EqualTo(1));
            Assert.That(RoomAwakening.Reveal(garden,1000f,0.32f),Is.EqualTo(1));
        }

        [Test]
        public void RoomDecorationsResetEvenWhenTheSessionClockContinues()
        {
            FillTo(Garden,GardenProgress.Goal,100f);
            Set(app,"playTime",500f);
            Set(app,"phase",GardenPhase.Awakening);
            Assert.That(RoomAwakening.Reveal(Garden,500f,0.32f),Is.EqualTo(1));
            Invoke("ResetRound");
            float time=Get<float>(app,"playTime");
            Assert.That(time,Is.EqualTo(500f));
            Assert.That(RoomAwakening.Reveal(Garden,time,0.32f),Is.Zero);
            Assert.That(RoomAwakening.ArrivalPulse(Garden,0,time),Is.Zero);
            Assert.That(RoomAwakening.ArrivalPulse(Garden,2,time),Is.Zero);

            FillTo(Garden,GardenProgress.Goal,time);
            Assert.That(RoomAwakening.Reveal(Garden,time,0.32f),Is.Zero,
                "The next round must earn its own reveal delay.");
            Assert.That(RoomAwakening.Reveal(Garden,time+5f,0.32f),Is.EqualTo(1));
        }

        [Test]
        public void ArrivalPulseEndsWhileTheEarnedRoomLightRemains()
        {
            var garden=GardenAt(GardenProgress.Goal,100f);
            Assert.That(RoomAwakening.ArrivalPulse(garden,0,100.8f),Is.Zero);
            Assert.That(RoomAwakening.ArrivalPulse(garden,0,101.775f),Is.GreaterThan(0.99f));
            Assert.That(RoomAwakening.ArrivalPulse(garden,0,102.5f),Is.Zero);
            Assert.That(RoomAwakening.Reveal(garden,102.5f,0),Is.EqualTo(1));
        }

        [Test]
        public void TenthArrivedLightFullyWakesTheLanternWithoutCompletingTheRound()
        {
            var garden=new GardenProgress();
            Assert.That(LanternRoom.WakeAmount(garden,100),Is.Zero);
            FillTo(garden,6,100);
            Assert.That(LanternRoom.WakeAmount(garden,100.9f),Is.Zero,
                "Food still in flight must not wake the lantern early.");
            float early=LanternRoom.WakeAmount(garden,102);
            Assert.That(early,Is.GreaterThan(0).And.LessThan(1));
            FillTo(garden,9,103);
            float ninth=LanternRoom.WakeAmount(garden,105);
            Assert.That(ninth,Is.GreaterThan(early).And.LessThan(1));
            garden.Deliver(110,false,new Vector2(500,700));
            Assert.That(LanternRoom.WakeAmount(garden,110.9f),Is.EqualTo(ninth),
                "The tenth light must still reach the room before it contributes.");
            Assert.That(LanternRoom.WakeAmount(garden,111.4f),Is.GreaterThan(ninth).And.LessThan(1));
            Assert.That(LanternRoom.WakeAmount(garden,112),Is.EqualTo(1));
            Assert.That(garden.IsComplete,Is.False,"Waking up is not the end of the round.");
            FillTo(garden,19,120);
            Assert.That(LanternRoom.WakeAmount(garden,125),Is.EqualTo(1));
            garden.Reset();
            Assert.That(LanternRoom.WakeAmount(garden,200),Is.Zero);
        }

        [Test]
        public void RoomLampsBrightenOutwardOnlyAfterLightReachesTheCentralLantern()
        {
            var garden=GardenAt(8,100);
            Assert.That(RoomAwakening.PairBrightness(garden,100.9f,0),Is.Zero,
                "A travelling light must not light the room ahead of its arrival.");
            Assert.That(RoomAwakening.PairBrightness(garden,103,0),Is.GreaterThan(0).And.LessThan(1));
            Assert.That(RoomAwakening.PairBrightness(garden,103,1),Is.Zero);
            float firstPair=RoomAwakening.PairBrightness(garden,103,0);
            FillTo(garden,15,110);
            Assert.That(RoomAwakening.PairBrightness(garden,110.9f,0),Is.EqualTo(firstPair));
            Assert.That(RoomAwakening.PairBrightness(garden,113,0),Is.EqualTo(1));
            Assert.That(RoomAwakening.PairBrightness(garden,113,1),Is.GreaterThan(0).And.LessThan(1));
            FillTo(garden,GardenProgress.Goal,120);
            Assert.That(RoomAwakening.PairBrightness(garden,123,0),Is.EqualTo(1));
            Assert.That(RoomAwakening.PairBrightness(garden,123,1),Is.EqualTo(1));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void EitherSpiritCanLightTheWholeRoomAndReplayClearsIt(bool right)
        {
            var garden=new GardenProgress();
            var previous=new float[RoomAwakening.PairCount];
            for(int count=1;count<=GardenProgress.Goal;count++)
            {
                garden.Deliver(100+count*3,right,new Vector2(right ? 900 : 500,700));
                for(int pair=0;pair<RoomAwakening.PairCount;pair++)
                {
                    float light=RoomAwakening.PairBrightness(garden,103+count*3,pair);
                    Assert.That(light,Is.InRange(previous[pair],1f));
                    previous[pair]=light;
                }
            }
            Assert.That(previous,Is.All.EqualTo(1f));
            garden.Reset();
            for(int pair=0;pair<RoomAwakening.PairCount;pair++)
                Assert.That(RoomAwakening.PairBrightness(garden,1000,pair),Is.Zero);
        }

        [Test]
        public void RoomFixturesStayAtTheWindowEdgesAndClearTheGameplayAndControls()
        {
            Assert.That(RoomAwakening.LeftFixtures,Has.Length.EqualTo(2));
            Assert.That(RoomAwakening.RightFixtures,Has.Length.EqualTo(2));
            Rect playWindow=new Rect(420,220,600,430);
            for(int pair=0;pair<RoomAwakening.PairCount;pair++)
            {
                var left=RoomAwakening.LeftFixtures[pair];
                var right=RoomAwakening.RightFixtures[pair];
                Assert.That(right.Glass.x,Is.EqualTo(1440-left.Glass.x).Within(0.001f));
                Assert.That(right.Glass.y,Is.EqualTo(left.Glass.y));
                foreach(var fixture in new[]{left,right})
                {
                    Assert.That(fixture.Body.width,Is.GreaterThan(25));
                    Assert.That(fixture.Body.height,Is.GreaterThan(35));
                    Assert.That(fixture.Body.xMin,Is.GreaterThanOrEqualTo(0));
                    Assert.That(fixture.Body.xMax,Is.LessThanOrEqualTo(1440));
                    Assert.That(fixture.Body.yMin,Is.GreaterThan(150),"Fixtures must clear the top controls.");
                    Assert.That(fixture.Body.yMax,Is.LessThan(600),"Fixtures must clear the spirits below.");
                    Assert.That(fixture.Body.Overlaps(playWindow),Is.False,
                        "Room decorations must not enter the falling-food and seesaw area.");
                    Assert.That(Vector2.Distance(LanternRoom.GlassPoint(fixture.Body),fixture.Glass),Is.LessThan(0.001f));
                    Assert.That(fixture.Anchor.y,Is.LessThan(LanternRoom.RingPoint(fixture.Body).y));
                }
            }
            Assert.That(RoomAwakening.LeftFixtures[0].Body.Overlaps(RoomAwakening.LeftFixtures[1].Body),Is.False);
            Assert.That(RoomAwakening.RightFixtures[0].Body.Overlaps(RoomAwakening.RightFixtures[1].Body),Is.False);
        }

        [Test]
        public void InvalidRoomProgressNeverLightsAnUnearnedPair()
        {
            foreach(float progress in new[]{float.NaN,float.PositiveInfinity,float.NegativeInfinity,-1f})
                Assert.That(RoomAwakening.PairBrightness(progress,0),Is.Zero);
            Assert.That(RoomAwakening.PairBrightness(1f,-1),Is.Zero);
            Assert.That(RoomAwakening.PairBrightness(1f,RoomAwakening.PairCount),Is.Zero);
        }

        [TestCase("paused")]
        [TestCase("settingsOpen")]
        public void ExplicitPauseFreezesTheCurrentRoomPair(string pauseField)
        {
            FillTo(Garden,8,100);
            Set(app,"phase",GardenPhase.Playing);
            Set(app,"playTime",101.6f);
            Set(app,pauseField,true);
            float reveal=RoomAwakening.PairBrightness(Garden,Get<float>(app,"playTime"),0);
            Assert.That(reveal,Is.GreaterThan(0).And.LessThan(1));
            Invoke("Update");
            Assert.That(RoomAwakening.PairBrightness(Garden,Get<float>(app,"playTime"),0),Is.EqualTo(reveal));
        }

        [Test]
        public void IntroBreathingDoesNotAdvanceTheRoundAndExplicitPauseFreezesIt()
        {
            Set(app,"phase",GardenPhase.Intro);
            Set(app,"introLanternTime",1f);
            float gameTime=Get<float>(app,"playTime");
            Invoke("Update");
            float visualTime=Get<float>(app,"introLanternTime");
            Assert.That(visualTime,Is.GreaterThan(1));
            Assert.That(Get<float>(app,"playTime"),Is.EqualTo(gameTime));
            Assert.That(Garden.LightCount,Is.Zero);
            float breath=LanternRoom.SleepBreath(gameTime+visualTime);
            Set(app,"settingsOpen",true);
            Invoke("Update");
            Assert.That(Get<float>(app,"introLanternTime"),Is.EqualTo(visualTime));
            Set(app,"settingsOpen",false);
            Set(app,"paused",true);
            Invoke("Update");
            Assert.That(Get<float>(app,"introLanternTime"),Is.EqualTo(visualTime));
            Assert.That(LanternRoom.SleepBreath(gameTime+Get<float>(app,"introLanternTime")),Is.EqualTo(breath));
        }

        GardenProgress Garden => Get<GardenProgress>(app,"garden");

        static GardenProgress GardenAt(int count,float time)
        {
            var garden=new GardenProgress();
            FillTo(garden,count,time);
            return garden;
        }

        static void FillTo(GardenProgress garden,int count,float time,bool right=false)
        {
            if(count<0||count>GardenProgress.MaxLights)
                throw new ArgumentOutOfRangeException(nameof(count),"Tests may fill at most one goal.");
            while(garden.DeliveredCount<count)
                garden.Deliver(time,right,new Vector2(right ? 900f : 500f,700f));
        }

        static void AssertTravelerCount(GardenProgress garden,float time,int expectedCount,int expectedIndex)
        {
            int count=0,index=-1;
            for(int i=0;i<garden.LightCount;i++)
            {
                LanternRoom.LightFlight light=LanternRoom.Evaluate(garden,i,time);
                if(!light.Started||light.Arrived)continue;
                count++;
                index=i;
            }
            Assert.That(count,Is.EqualTo(expectedCount));
            Assert.That(index,Is.EqualTo(expectedIndex));
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

        void AddDelivery(bool correct,float startedAt,bool right=false)
        {
            Type type=typeof(OverheadApp).GetNestedType("Delivery",BindingFlags.NonPublic);
            object delivery=Activator.CreateInstance(type,true);
            Set(delivery,"Correct",correct);
            Set(delivery,"StartedAt",startedAt);
            Set(delivery,"Right",right);
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
