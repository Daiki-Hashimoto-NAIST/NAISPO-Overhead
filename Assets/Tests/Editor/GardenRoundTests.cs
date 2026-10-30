using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Naispo.Overhead.Tests
{
    public sealed class GardenRoundTests
    {
        const BindingFlags Members=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
        GameObject host;
        OverheadApp app;
        GameAudio sounds;

        [SetUp]
        public void SetUp()
        {
            // Inactive components exercise the actual round methods without loading artwork,
            // opening a Kinect, playing audio, or advancing frames through the Editor.
            host=new GameObject("Garden round test");
            host.SetActive(false);
            app=host.AddComponent<OverheadApp>();
            var source=host.AddComponent<OverheadDepthSource>();
            sounds=host.AddComponent<GameAudio>();
            Set(app,"source",source);
            Set(app,"sounds",sounds);
            Set(app,"playTime",100f);
            // Feedback and food only run while the round is playing; intro tests opt out below.
            Set(app,"phase",GardenPhase.Playing);
        }

        [TearDown]
        public void TearDown()
        {
            if(host!=null)UnityEngine.Object.DestroyImmediate(host);
        }

        [Test]
        public void TwentiethCorrectProcessedFirstClearsSurplusWithoutLateMiss()
        {
            FillGarden(19);
            Set(app,"misses",4);
            // The backwards loop must reach this correct arrival before the later wrong one.
            AddDelivery(false,0);
            AddDelivery(true,0);
            AddNested("tokens","Token");
            var impact=AddNested("impacts","Impact");
            Set(impact,"StartedAt",100f);
            Set(app,"draggingGate",true);

            Invoke("UpdateFeedback",0.02f);

            Assert.That(Garden.DeliveredCount,Is.EqualTo(GardenProgress.Goal));
            Assert.That(Garden.LightCount,Is.EqualTo(GardenProgress.Goal));
            Assert.That(Garden.IsComplete,Is.True);
            Assert.That(Get<int>(app,"misses"),Is.EqualTo(4),"Surplus food must not change the ending.");
            Assert.That(List("tokens"),Is.Empty);
            Assert.That(List("deliveries"),Is.Empty);
            Assert.That(List("impacts"),Is.Empty);
            Assert.That(Get<bool>(app,"draggingGate"),Is.False);
            Assert.That(Get<GardenPhase>(app,"phase"),Is.EqualTo(GardenPhase.Awakening));
            Assert.That(Garden.FinalArrivalAt,
                Is.EqualTo(100f+GardenProgress.LightFlightDuration).Within(0.0001f));
            Assert.That(Get<bool>(app,"finalSoundPlayed"),Is.False,
                "The blessing waits for the final light to reach the wall.");
        }

        [Test]
        public void TwoSameFrameCorrectBelowGoalEachGetALight()
        {
            AddDelivery(true,0,false);
            AddDelivery(true,0,true);

            Invoke("UpdateFeedback",0.02f);

            Assert.That(Garden.DeliveredCount,Is.EqualTo(2));
            Assert.That(Garden.LightCount,Is.EqualTo(2));
            Assert.That(Garden.LightAt(0).Time,Is.EqualTo(100f));
            Assert.That(Garden.LightAt(1).Time,Is.EqualTo(100f));
            Assert.That(Garden.LightIsRight(0),Is.True);
            Assert.That(Garden.LightIsRight(1),Is.False);
            Assert.That(Garden.LightSource(0),Is.Not.EqualTo(Garden.LightSource(1)));
            Assert.That(Garden.FinalArrivalAt,Is.EqualTo(float.NegativeInfinity));
        }

        [Test]
        public void WrongDeliveryAddsNoLight()
        {
            AddDelivery(false,0);

            Invoke("UpdateFeedback",0.02f);

            Assert.That(Garden.DeliveredCount,Is.Zero);
            Assert.That(Garden.LightCount,Is.Zero);
            Assert.That(Get<int>(app,"misses"),Is.EqualTo(1));
            Assert.That(Garden.FinalArrivalAt,Is.EqualTo(float.NegativeInfinity));
        }

        [Test]
        public void TenthSuccessDoesNotRestartTheWarningOrCelebrate()
        {
            FillGarden(9);
            var schedule=Get<FoodDropSchedule>(app,"dropSchedule");
            schedule.Advance(1f,0,out _);
            schedule.Advance(0.3f,0,out _);
            float warningBefore=schedule.WarningProgress;
            Assert.That(schedule.HasWarning,Is.True);

            AddDelivery(true,0);
            Invoke("UpdateFeedback",0.02f);

            Assert.That(Garden.DeliveredCount,Is.EqualTo(10));
            Assert.That(Get<GardenPhase>(app,"phase"),Is.EqualTo(GardenPhase.Playing));
            Assert.That(Celebrating,Is.False,"The tenth success is not a milestone.");
            Assert.That(schedule.WarningProgress,Is.EqualTo(warningBefore).Within(0.0001f),
                "A success must not restart the pending warning.");

            var token=AddNested("tokens","Token");
            Set(token,"Position",new Vector2(600,300));
            Set(token,"Velocity",new Vector2(0,20));
            float timeBefore=Get<float>(app,"playTime");

            Invoke("Update");

            Assert.That(Get<float>(app,"playTime"),Is.GreaterThan(timeBefore));
            Assert.That(schedule.WarningProgress,Is.GreaterThan(warningBefore),
                "The next frame must advance the drop schedule.");
            Assert.That(Get<Vector2>(token,"Position").y,Is.GreaterThan(300f),
                "The next frame must advance falling food.");
        }

        [Test]
        public void IntroFreezesTheClockAndQueuedFood()
        {
            Set(app,"phase",GardenPhase.Intro);
            var token=AddNested("tokens","Token");
            Set(token,"Position",new Vector2(600,300));
            Set(token,"Velocity",new Vector2(0,20));
            var schedule=Get<FoodDropSchedule>(app,"dropSchedule");
            schedule.Advance(1f,0,out _);
            schedule.Advance(0.2f,0,out _);
            float warning=schedule.WarningProgress;
            float time=Get<float>(app,"playTime");

            Invoke("Update");

            Assert.That(Get<float>(app,"playTime"),Is.EqualTo(time));
            Assert.That(Get<Vector2>(token,"Position"),Is.EqualTo(new Vector2(600,300)));
            Assert.That(schedule.WarningProgress,Is.EqualTo(warning));
            Assert.That(Get<GardenPhase>(app,"phase"),Is.EqualTo(GardenPhase.Intro));
        }

        [Test]
        public void SettingsKeepsAQueuedStartPending()
        {
            Set(app,"phase",GardenPhase.Intro);
            Set(app,"ready",true);
            Set(app,"angle",31f);
            Set(app,"targetAngle",31f);
            Set(app,"hasAngle",true);
            Set(app,"draggingGate",true);
            Set(app,"settingsOpen",true);
            Assert.That(app.RequestStart(),Is.True);

            Invoke("Update");

            Assert.That(Get<bool>(app,"startRequested"),Is.True);
            Assert.That(Get<GardenPhase>(app,"phase"),Is.EqualTo(GardenPhase.Intro));

            Set(app,"settingsOpen",false);
            Invoke("Update");

            Assert.That(Get<bool>(app,"startRequested"),Is.False);
            Assert.That(Get<GardenPhase>(app,"phase"),Is.EqualTo(GardenPhase.Playing));
            Assert.That(Get<bool>(app,"ready"),Is.True);
            Assert.That(Get<float>(app,"angle"),Is.EqualTo(31f));
            Assert.That(Get<bool>(app,"hasAngle"),Is.True);
            Assert.That(Get<bool>(app,"draggingGate"),Is.False);
        }

        [TestCase(true,false)]
        [TestCase(false,true)]
        [TestCase(false,false)]
        public void IntroKeysQueueAndConsumeOneStart(bool space,bool enter)
        {
            Set(app,"phase",GardenPhase.Intro);

            Invoke("HandleIntroKeys",space,enter);
            Assert.That(Get<bool>(app,"startRequested"),Is.EqualTo(space||enter));

            Invoke("Update");

            Assert.That(Get<GardenPhase>(app,"phase"),
                Is.EqualTo(space||enter ? GardenPhase.Playing : GardenPhase.Intro));
            if(space||enter)Assert.That(Get<bool>(app,"startRequested"),Is.False);
        }

        [Test]
        public void QueuedStartConsumesOnceWithoutResettingReadyAngleOrDragging()
        {
            Set(app,"phase",GardenPhase.Intro);
            Set(app,"ready",true);
            Set(app,"angle",27f);
            Set(app,"targetAngle",27f);
            Set(app,"hasAngle",true);
            Set(app,"draggingGate",true);
            Assert.That(app.RequestStart(),Is.True);
            Assert.That(app.RequestStart(),Is.True,"The queued request is idempotent.");

            Invoke("Update");

            Assert.That(Get<GardenPhase>(app,"phase"),Is.EqualTo(GardenPhase.Playing));
            Assert.That(Get<bool>(app,"startRequested"),Is.False);
            Assert.That(Get<bool>(app,"ready"),Is.True);
            Assert.That(Get<float>(app,"angle"),Is.EqualTo(27f));
            Assert.That(Get<bool>(app,"hasAngle"),Is.True);
            Assert.That(Get<bool>(app,"draggingGate"),Is.False);
            Assert.That(app.RequestStart(),Is.False,"A started round must ignore another start.");
        }

        [Test]
        public void RestartPlaysDirectlyAndResetReturnsToIntro()
        {
            FillGarden(GardenProgress.Goal);
            Set(app,"phase",GardenPhase.Complete);
            Set(app,"playTime",150f);
            Set(app,"angle",40f);
            Set(app,"targetAngle",40f);
            Set(app,"hasAngle",true);
            Set(app,"draggingGate",true);

            Invoke("RestartRound");

            Assert.That(Get<GardenPhase>(app,"phase"),Is.EqualTo(GardenPhase.Playing));
            Assert.That(Garden.DeliveredCount,Is.Zero);
            Assert.That(Get<float>(app,"playTime"),Is.EqualTo(150f));
            Assert.That(Get<bool>(app,"draggingGate"),Is.False);
            Assert.That(Get<float>(app,"angle"),Is.Zero);

            Invoke("ResetRound");

            Assert.That(Get<GardenPhase>(app,"phase"),Is.EqualTo(GardenPhase.Intro));
            Assert.That(Get<bool>(app,"startRequested"),Is.False);
        }

        [Test]
        public void LostTrackingFreezesPlayingButAwakeningStillAdvances()
        {
            Set(app,"phase",GardenPhase.Playing);
            Set(app,"live",true);
            Set(app,"ready",false);
            float playingTime=Get<float>(app,"playTime");

            Invoke("Update");

            Assert.That(Get<float>(app,"playTime"),Is.EqualTo(playingTime));
            Assert.That(Get<GardenPhase>(app,"phase"),Is.EqualTo(GardenPhase.Playing));

            FillGarden(GardenProgress.Goal,100f);
            Set(app,"phase",GardenPhase.Awakening);
            Set(app,"playTime",100f);
            Set(app,"ready",false);

            Invoke("Update");

            Assert.That(Get<float>(app,"playTime"),Is.GreaterThan(100f));
            Assert.That(Get<GardenPhase>(app,"phase"),Is.EqualTo(GardenPhase.Awakening));
        }

        [TestCase("paused")]
        [TestCase("settingsOpen")]
        public void ExplicitPauseAndSettingsFreezeTheEnding(string pauseField)
        {
            FillGarden(GardenProgress.Goal,100f);
            Set(app,"phase",GardenPhase.Awakening);
            Set(app,"playTime",Garden.FinalArrivalAt);
            Set(app,pauseField,true);
            var impact=AddNested("impacts","Impact");
            Set(impact,"StartedAt",0f);

            Invoke("Update");

            Assert.That(Get<float>(app,"playTime"),Is.EqualTo(Garden.FinalArrivalAt));
            Assert.That(Get<GardenPhase>(app,"phase"),Is.EqualTo(GardenPhase.Awakening));
            Assert.That(List("impacts").Count,Is.EqualTo(1),"Feedback must also remain paused.");
            Assert.That(Get<bool>(sounds,"paused"),Is.True);
            Assert.That(Get<bool>(app,"finalSoundPlayed"),Is.False);
        }

        [Test]
        public void ResetClearsRecordsExpressionsPendingStartFinaleAndSounds()
        {
            FillGarden(GardenProgress.Goal,100f);
            Set(app,"phase",GardenPhase.Complete);
            Set(app,"playTime",102f);
            Set(app,"misses",7);
            Set(app,"cloudMood",1f);
            Set(app,"startRequested",true);
            Set(app,"finalSoundPlayed",true);
            Set(app,"draggingGate",true);
            Set(app,"angle",50f);
            Set(app,"targetAngle",45f);
            Set(app,"hasAngle",true);
            AddNested("tokens","Token");
            AddNested("impacts","Impact");
            AddDelivery(false,0);
            foreach(string side in new[]{"leftMonster","rightMonster"})
            {
                object monster=Get<object>(app,side);
                Set(monster,"Expression",2);
                Set(monster,"ReactedAt",99f);
                Set(monster,"Attention",1f);
                Set(monster,"LookDirection",1f);
            }
            var schedule=Get<FoodDropSchedule>(app,"dropSchedule");
            schedule.Advance(1,0,out _);
            schedule.Advance(1,0,out _);
            Assert.That(schedule.SpawnedCount,Is.EqualTo(1));
            Set(sounds,"celebrationUntil",999f);

            Invoke("ResetRound");

            Assert.That(Get<GardenPhase>(app,"phase"),Is.EqualTo(GardenPhase.Intro));
            Assert.That(Garden.DeliveredCount,Is.Zero);
            Assert.That(Garden.LightCount,Is.Zero);
            Assert.That(Garden.IsComplete,Is.False);
            Assert.That(Garden.FinalArrivalAt,Is.EqualTo(float.NegativeInfinity));
            Assert.Throws<ArgumentOutOfRangeException>(()=>Garden.LightAt(0));
            Assert.That(Get<float>(app,"playTime"),Is.EqualTo(102f));
            Assert.That(Get<int>(app,"misses"),Is.Zero);
            Assert.That(Get<float>(app,"cloudMood"),Is.Zero);
            Assert.That(Get<bool>(app,"startRequested"),Is.False);
            Assert.That(Get<bool>(app,"finalSoundPlayed"),Is.False);
            Assert.That(Get<float>(sounds,"celebrationUntil"),Is.Zero);
            Assert.That(Get<bool>(app,"draggingGate"),Is.False);
            Assert.That(Get<float>(app,"angle"),Is.Zero);
            Assert.That(Get<float>(app,"targetAngle"),Is.Zero);
            Assert.That(Get<bool>(app,"hasAngle"),Is.False);
            Assert.That(List("tokens"),Is.Empty);
            Assert.That(List("deliveries"),Is.Empty);
            Assert.That(List("impacts"),Is.Empty);
            Assert.That(schedule.SpawnedCount,Is.Zero);
            Assert.That(schedule.HasWarning,Is.False);
            Assert.That(schedule.Upcoming.Sequence,Is.Zero);
            Assert.That(Celebrating,Is.False);
            foreach(string side in new[]{"leftMonster","rightMonster"})
            {
                object monster=Get<object>(app,side);
                Assert.That(Get<int>(monster,"Expression"),Is.Zero);
                Assert.That(Get<float>(monster,"Attention"),Is.Zero);
                Assert.That(Get<float>(monster,"LookDirection"),Is.Zero);
            }
        }

        GardenProgress Garden => Get<GardenProgress>(app,"garden");
        bool Celebrating => (bool)typeof(OverheadApp).GetProperty("Celebrating",Members).GetValue(app);

        void FillGarden(int count,float time=0f)
        {
            if(count<0||count>GardenProgress.MaxLights)
                throw new ArgumentOutOfRangeException(nameof(count),"Tests may fill at most one goal.");
            for(int i=0;i<count;i++)
                Garden.Deliver(time,false,new Vector2(500,700));
        }

        void AddDelivery(bool correct,float startedAt,bool right=false)
        {
            object delivery=AddNested("deliveries","Delivery");
            Set(delivery,"Correct",correct);
            Set(delivery,"StartedAt",startedAt);
            Set(delivery,"Right",right);
        }

        object AddNested(string collection,string nestedType)
        {
            Type type=typeof(OverheadApp).GetNestedType(nestedType,BindingFlags.NonPublic);
            object item=Activator.CreateInstance(type,true);
            List(collection).Add(item);
            return item;
        }

        IList List(string field) => Get<IList>(app,field);

        void Invoke(string method,params object[] arguments)
        {
            typeof(OverheadApp).GetMethod(method,Members).Invoke(app,arguments);
        }

        static T Get<T>(object instance,string field) =>
            (T)instance.GetType().GetField(field,Members).GetValue(instance);

        static T Static<T>(string method,params object[] arguments) =>
            (T)typeof(OverheadApp).GetMethod(method,BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic)
                .Invoke(null,arguments);

        static void Set(object instance,string field,object value) =>
            instance.GetType().GetField(field,Members).SetValue(instance,value);
    }
}
