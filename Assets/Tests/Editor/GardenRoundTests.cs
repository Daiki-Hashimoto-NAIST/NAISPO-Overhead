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
        }

        [TearDown]
        public void TearDown()
        {
            if(host!=null)UnityEngine.Object.DestroyImmediate(host);
        }

        [Test]
        public void ThirtiethArrivalClearsFoodThatCouldScoreOrMissInTheSameFrame()
        {
            FillGarden(29);
            Set(app,"misses",4);
            AddDelivery(true,0);
            AddDelivery(false,0);
            // Delivery processing walks backwards; this correct arrival reaches the goal first.
            AddDelivery(true,0);
            AddNested("tokens","Token");
            var impact=AddNested("impacts","Impact");
            Set(impact,"StartedAt",100f);
            Set(app,"draggingGate",true);

            Invoke("UpdateFeedback",0.02f);

            Assert.That(Garden.DeliveredCount,Is.EqualTo(30));
            Assert.That(Garden.IsComplete,Is.True);
            Assert.That(Get<int>(app,"misses"),Is.EqualTo(4),"Surplus food must not change the ending.");
            Assert.That(List("tokens"),Is.Empty);
            Assert.That(List("deliveries"),Is.Empty);
            Assert.That(List("impacts"),Is.Empty);
            Assert.That(Get<bool>(app,"draggingGate"),Is.False);
            Assert.That(Get<float>(app,"celebrationAt"),Is.EqualTo(100f));
            Assert.That(Garden.BloomStartedAt(2),Is.EqualTo(100f));
        }

        [Test]
        public void CompletedGardenStillProcessesItsEndingWithoutTrackedHands()
        {
            FillGarden(30);
            Set(app,"live",true);
            Set(app,"ready",false);
            Set(app,"celebrationAt",99f);
            Set(app,"draggingGate",true);
            var impact=AddNested("impacts","Impact");
            Set(impact,"StartedAt",0f);
            sounds.SetPaused(true);
            float expectedTime=100f+Mathf.Min(Time.deltaTime,0.05f);

            Invoke("Update");

            Assert.That(Get<bool>(app,"ready"),Is.False,"The test must retain unavailable tracking.");
            Assert.That(Get<float>(app,"playTime"),Is.EqualTo(expectedTime).Within(0.0001f));
            Assert.That(List("impacts"),Is.Empty,"Ending feedback must run even with no tracking.");
            Assert.That(Get<bool>(sounds,"paused"),Is.False);
            Assert.That(Get<bool>(app,"draggingGate"),Is.False);
            Assert.That(Get<FoodDropSchedule>(app,"dropSchedule").SpawnedCount,Is.Zero);
            Assert.That(Garden.DeliveredCount,Is.EqualTo(30));
        }

        [TestCase("paused")]
        [TestCase("settingsOpen")]
        public void ExplicitPauseStopsTheCompletedGardenAndItsSounds(string pauseField)
        {
            FillGarden(30);
            Set(app,"celebrationAt",99f);
            Set(app,pauseField,true);
            var impact=AddNested("impacts","Impact");
            Set(impact,"StartedAt",0f);

            Invoke("Update");

            Assert.That(Get<float>(app,"playTime"),Is.EqualTo(100f));
            Assert.That(List("impacts").Count,Is.EqualTo(1),"Feedback must also remain paused.");
            Assert.That(Get<bool>(sounds,"paused"),Is.True);
            Assert.That(Garden.DeliveredCount,Is.EqualTo(30));
        }

        [Test]
        public void RestartClearsTheFinishedGardenAndAcceptsNewDeliveries()
        {
            FillGarden(30);
            Set(app,"misses",7);
            Set(app,"celebrationAt",99f);
            Set(app,"angle",50f);
            Set(app,"targetAngle",45f);
            Set(app,"hasAngle",true);
            Set(app,"draggingGate",true);
            Set(app,"cloudMood",1f);
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

            Invoke("ResetRound");

            Assert.That(Garden.DeliveredCount,Is.Zero);
            Assert.That(Garden.IsComplete,Is.False);
            Assert.That(Garden.BloomCount,Is.Zero);
            for(int stage=0;stage<GardenProgress.StageCount;stage++)
                Assert.That(Garden.BloomStartedAt(stage),Is.EqualTo(float.NegativeInfinity));
            Assert.That(Get<int>(app,"misses"),Is.Zero);
            Assert.That(List("tokens"),Is.Empty);
            Assert.That(List("deliveries"),Is.Empty);
            Assert.That(List("impacts"),Is.Empty);
            Assert.That(Get<float>(app,"angle"),Is.Zero);
            Assert.That(Get<float>(app,"targetAngle"),Is.Zero);
            Assert.That(Get<bool>(app,"hasAngle"),Is.False);
            Assert.That(Get<bool>(app,"draggingGate"),Is.False);
            Assert.That(Get<float>(app,"cloudMood"),Is.Zero);
            Assert.That(schedule.SpawnedCount,Is.Zero);
            Assert.That(schedule.HasWarning,Is.False);
            Assert.That(schedule.Upcoming.Sequence,Is.Zero);
            Assert.That(typeof(OverheadApp).GetProperty("Celebrating",Members).GetValue(app),Is.False);
            foreach(string side in new[]{"leftMonster","rightMonster"})
            {
                object monster=Get<object>(app,side);
                Assert.That(Get<int>(monster,"Expression"),Is.Zero);
                Assert.That(Get<float>(monster,"Attention"),Is.Zero);
                Assert.That(Get<float>(monster,"LookDirection"),Is.Zero);
            }

            AddDelivery(true,0);
            Invoke("UpdateFeedback",0.02f);
            Assert.That(Garden.DeliveredCount,Is.EqualTo(1),"Restart must allow a fresh round.");
            Assert.That(Garden.BloomCount,Is.Zero);
            Assert.That(List("deliveries"),Is.Empty);
        }

        GardenProgress Garden => Get<GardenProgress>(app,"garden");

        void FillGarden(int count)
        {
            for(int i=0;i<count;i++)Garden.Deliver(i);
        }

        void AddDelivery(bool correct,float startedAt)
        {
            object delivery=AddNested("deliveries","Delivery");
            Set(delivery,"Correct",correct);
            Set(delivery,"StartedAt",startedAt);
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
