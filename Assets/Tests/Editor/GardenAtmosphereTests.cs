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
        public void EveryNewBloomWarmsGraduallyWithoutResettingEarlierWarmth()
        {
            for(int milestone=10;milestone<=30;milestone+=10)
            {
                var garden=new GardenProgress();
                FillTo(garden,milestone-1,0);
                float before=GardenAtmosphere.Warmth(garden,100);
                garden.Deliver(100);

                Assert.That(GardenAtmosphere.Warmth(garden,100),Is.EqualTo(before),
                    "Earning a bloom must not make the background jump to its final color.");
                float previous=before;
                for(int step=1;step<=16;step++)
                {
                    float value=GardenAtmosphere.Warmth(garden,100+step*0.25f);
                    Assert.That(value,Is.InRange(previous,1f));
                    previous=value;
                }
                Assert.That(previous,Is.GreaterThan(before));
                Assert.That(GardenAtmosphere.Warmth(garden,10000),Is.EqualTo(previous));
            }
        }

        [Test]
        public void UnearnedBloomTimesDoNotProduceWindOrInvalidWarmth()
        {
            foreach(int deliveries in new[]{0,9,10,19})
            {
                var garden=new GardenProgress();
                FillTo(garden,deliveries,5);
                foreach(float time in new[]{0f,5f,10000f})
                {
                    Assert.That(GardenAtmosphere.Warmth(garden,time),Is.InRange(0f,1f));
                    foreach(float x in new[]{0f,720f,1440f})
                        Assert.That(GardenAtmosphere.BreezeAngle(garden,time,x),Is.Zero);
                }
            }
        }

        [Test]
        public void GustsTravelLeftToRightAndLeavePeriodsOfCompleteCalm()
        {
            var garden=new GardenProgress();
            FillTo(garden,20,100);
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
            FillTo(Garden,20,95);
            FillTo(Garden,30,100);
            Set(app,"playTime",100.75f);
            Set(app,"celebrationAt",100f);
            float warmth=GardenAtmosphere.Warmth(Garden,100.75f);
            float breeze=GardenAtmosphere.BreezeAngle(Garden,100.75f,0);
            Assert.That(warmth,Is.GreaterThan(2f/3).And.LessThan(1));
            Assert.That(breeze,Is.GreaterThan(0));
            Set(app,pauseField,true);

            for(int frame=0;frame<4;frame++)Invoke("Update");

            float stoppedTime=Get<float>(app,"playTime");
            Assert.That(stoppedTime,Is.EqualTo(100.75f));
            Assert.That(GardenAtmosphere.Warmth(Garden,stoppedTime),Is.EqualTo(warmth));
            Assert.That(GardenAtmosphere.BreezeAngle(Garden,stoppedTime,0),Is.EqualTo(breeze));
        }

        [Test]
        public void RestartClearsTheAtmosphereEvenThoughTheSessionClockContinues()
        {
            FillTo(Garden,20,95);
            Assert.That(GardenAtmosphere.Warmth(Garden,100),Is.GreaterThan(0));
            Assert.That(GardenAtmosphere.BreezeAngle(Garden,100,0),Is.GreaterThan(0));

            Invoke("ResetRound");

            float time=Get<float>(app,"playTime");
            Assert.That(time,Is.EqualTo(100f));
            Assert.That(GardenAtmosphere.Warmth(Garden,time),Is.Zero);
            Assert.That(GardenAtmosphere.BreezeAngle(Garden,time,0),Is.Zero);
            FillTo(Garden,20,time);
            Assert.That(GardenAtmosphere.Warmth(Garden,time),Is.Zero);
            Assert.That(GardenAtmosphere.BreezeAngle(Garden,time,0),Is.Zero,
                "The next round must earn its own breeze delay.");
            Assert.That(GardenAtmosphere.BreezeAngle(Garden,time+5,0),Is.GreaterThan(0));
        }

        [TestCase(10,false)]
        [TestCase(20,false)]
        [TestCase(30,true)]
        public void FinalDeliveryKeepsCelebratingAfterAnOrdinaryMilestoneHasFinished(int milestone,bool longerEnding)
        {
            FillTo(Garden,milestone-1,95);
            Type type=typeof(OverheadApp).GetNestedType("Delivery",BindingFlags.NonPublic);
            object delivery=Activator.CreateInstance(type,true);
            Set(delivery,"Correct",true);
            Set(delivery,"StartedAt",99f);
            Get<IList>(app,"deliveries").Add(delivery);

            Invoke("UpdateFeedback",0.02f);

            Assert.That(Garden.DeliveredCount,Is.EqualTo(milestone));
            Assert.That(Celebrating,Is.True);
            Set(app,"playTime",102.9f);
            Assert.That(Celebrating,Is.EqualTo(longerEnding));
            Set(app,"playTime",103.5f);
            Assert.That(Celebrating,Is.False,"The ending must eventually reveal its replay button.");
        }

        GardenProgress Garden => Get<GardenProgress>(app,"garden");
        bool Celebrating => (bool)typeof(OverheadApp).GetProperty("Celebrating",Members).GetValue(app);

        static void FillTo(GardenProgress garden,int count,float time)
        {
            while(garden.DeliveredCount<count)garden.Deliver(time);
        }

        void Invoke(string method,params object[] arguments) =>
            typeof(OverheadApp).GetMethod(method,Members).Invoke(app,arguments);

        static T Get<T>(object instance,string field) =>
            (T)instance.GetType().GetField(field,Members).GetValue(instance);

        static void Set(object instance,string field,object value) =>
            instance.GetType().GetField(field,Members).SetValue(instance,value);
    }
}
