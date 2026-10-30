using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Naispo.Overhead.Tests
{
    public sealed class FoodDeliveryRoundTests
    {
        const BindingFlags Members=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
        GameObject host;
        OverheadApp app;

        [SetUp]
        public void SetUp()
        {
            // Run the real round methods without loading artwork, connecting a camera,
            // or playing audio. These positions remain safe for any capped Update delta.
            host=new GameObject("Food delivery round test");
            host.SetActive(false);
            app=host.AddComponent<OverheadApp>();
            Set(app,"source",host.AddComponent<OverheadDepthSource>());
            Set(app,"sounds",host.AddComponent<GameAudio>());
            Set(app,"playTime",100f);
            // Update only moves and scores food while the round is playing.
            Set(app,"phase",GardenPhase.Playing);
        }

        [TearDown]
        public void TearDown()
        {
            if(host!=null)UnityEngine.Object.DestroyImmediate(host);
        }

        [TestCase(false,false,true)]
        [TestCase(false,true,false)]
        [TestCase(true,true,true)]
        [TestCase(true,false,false)]
        public void WeakDeparturesReachTheNearMonsterAndKeepItsFoodPreference(bool right,bool isFish,bool correct)
        {
            AddFood(right ? 730 : 280,650,right ? 10 : -10,true,isFish);

            Invoke("Update");

            Assert.That(List("tokens"),Is.Empty);
            Assert.That(List("deliveries").Count,Is.EqualTo(1));
            object delivery=List("deliveries")[0];
            Assert.That(Get<bool>(delivery,"Right"),Is.EqualTo(right));
            Assert.That(Get<bool>(delivery,"IsFish"),Is.EqualTo(isFish));
            Assert.That(Get<bool>(delivery,"Correct"),Is.EqualTo(correct));
            Assert.That(SorterRules.ExitSide(Get<Vector2>(delivery,"Start")),Is.Zero,
                "Delivery must start before the food reaches the old side or bottom boundary.");
            Assert.That(Garden.DeliveredCount,Is.Zero,"Food should count only after reaching the mouth.");
            Assert.That(Get<int>(app,"misses"),Is.Zero);

            FinishDelivery();

            Assert.That(List("deliveries"),Is.Empty);
            Assert.That(Garden.DeliveredCount,Is.EqualTo(correct ? 1 : 0));
            Assert.That(Get<int>(app,"misses"),Is.EqualTo(correct ? 0 : 1));
            Assert.That(Get<int>(Get<object>(app,right ? "rightMonster" : "leftMonster"),"Expression"),
                Is.EqualTo(correct ? 1 : 2));
            Assert.That(Get<int>(Get<object>(app,right ? "leftMonster" : "rightMonster"),"Expression"),Is.Zero);
        }

        [Test]
        public void UntouchedFoodIsNotRescuedAndStillMissesAtTheBottom()
        {
            object token=AddFood(280,650,-10,false,false);

            Invoke("Update");

            Assert.That(List("tokens").Count,Is.EqualTo(1));
            Assert.That(List("deliveries"),Is.Empty);
            Assert.That(Get<int>(app,"misses"),Is.Zero);

            Set(token,"Position",new Vector2(280,850));
            Invoke("Update");

            Assert.That(List("tokens"),Is.Empty);
            Assert.That(List("deliveries"),Is.Empty);
            Assert.That(Get<int>(app,"misses"),Is.EqualTo(1));
            Assert.That(Garden.DeliveredCount,Is.Zero);
        }

        [Test]
        public void TouchedFoodWithinThePaddlesReachStaysAvailableToHitAgain()
        {
            AddFood(585,650,0,true,false);

            Invoke("Update");

            Assert.That(List("tokens").Count,Is.EqualTo(1));
            Assert.That(List("deliveries"),Is.Empty);
            Assert.That(Get<int>(app,"misses"),Is.Zero);
            Assert.That(Garden.DeliveredCount,Is.Zero);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void AContactedDepartureIsRescuedBeforeTheBottomMissRule(bool right)
        {
            AddFood(right ? 730 : 280,850,right ? 10 : -10,true,right);

            Invoke("Update");

            Assert.That(List("tokens"),Is.Empty);
            Assert.That(List("deliveries").Count,Is.EqualTo(1));
            Assert.That(Get<int>(app,"misses"),Is.Zero);
            FinishDelivery();
            Assert.That(Garden.DeliveredCount,Is.EqualTo(1));
            Assert.That(Get<int>(app,"misses"),Is.Zero);
        }

        GardenProgress Garden => Get<GardenProgress>(app,"garden");
        IList List(string field) => Get<IList>(app,field);

        object AddFood(float x,float y,float horizontalSpeed,bool hasTouchedGate,bool isFish)
        {
            Type type=typeof(OverheadApp).GetNestedType("Token",BindingFlags.NonPublic);
            object token=Activator.CreateInstance(type,true);
            Set(token,"Position",new Vector2(x,y));
            Set(token,"Velocity",new Vector2(horizontalSpeed,20));
            Set(token,"HasTouchedGate",hasTouchedGate);
            Set(token,"IsFish",isFish);
            List("tokens").Add(token);
            return token;
        }

        void FinishDelivery()
        {
            Set(app,"playTime",Get<float>(app,"playTime")+1);
            Invoke("UpdateFeedback",0.02f);
        }

        void Invoke(string method,params object[] arguments) =>
            typeof(OverheadApp).GetMethod(method,Members).Invoke(app,arguments);

        static T Get<T>(object instance,string field) =>
            (T)instance.GetType().GetField(field,Members).GetValue(instance);

        static void Set(object instance,string field,object value) =>
            instance.GetType().GetField(field,Members).SetValue(instance,value);
    }
}
