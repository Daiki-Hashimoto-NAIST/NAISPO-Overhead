using NUnit.Framework;
using UnityEngine;

namespace Naispo.Overhead.Tests
{
    public sealed class FoodMotionTests
    {
        [Test]
        public void ImpactEffectReceivesThePaddleSurfaceInsteadOfTheFoodCenter()
        {
            Vector2 position=new Vector2(0,-45),velocity=new Vector2(0,500);
            Assert.That(FoodMotion.Advance(ref position,ref velocity,Vector2.zero,0,0,170,28,0.04f,out var hit),Is.True);
            Assert.That(hit.Point.y,Is.EqualTo(-FoodMotion.GateHalfThickness).Within(0.01f));
            Assert.That(hit.Normal,Is.EqualTo(Vector2.down));
            Assert.That(FoodMotion.Advance(ref position,ref velocity,Vector2.zero,0,0,170,28,0,out var empty),Is.False);
            Assert.That(empty.Point,Is.EqualTo(Vector2.zero));
        }

        [TestCase(15)]
        [TestCase(30)]
        [TestCase(60)]
        [TestCase(144)]
        public void FallingFoodBouncesAtDifferentFrameRates(int fps)
        {
            Vector2 position=new Vector2(0,-345),velocity=new Vector2(0,85);
            bool bounced=false;
            for(int frame=0;frame<fps*3;frame++)
            {
                if(!FoodMotion.Advance(ref position,ref velocity,Vector2.zero,35,35,170,28,1f/fps))continue;
                bounced=true;
                Assert.That(velocity.x,Is.GreaterThan(0));
                Assert.That(velocity.y,Is.LessThan(0));
                Assert.That(SurfaceDistance(position,35),Is.GreaterThanOrEqualTo(37.9f));
                break;
            }
            Assert.That(bounced,Is.True);
        }

        [TestCase(-65f,-105f)]
        [TestCase(-65f,0f)]
        [TestCase(-65f,105f)]
        [TestCase(0f,-105f)]
        [TestCase(0f,0f)]
        [TestCase(0f,105f)]
        [TestCase(65f,-105f)]
        [TestCase(65f,0f)]
        [TestCase(65f,105f)]
        public void EntireSpawnRangeCanHitEvenAtMaximumTilt(float angle,float x)
        {
            Vector2 position=new Vector2(x,-345),velocity=new Vector2(0,85);
            bool bounced=false;
            for(int frame=0;frame<240;frame++)
                if(FoodMotion.Advance(ref position,ref velocity,Vector2.zero,angle,angle,170,28,1f/60))
                { bounced=true;break; }
            Assert.That(bounced,Is.True);
        }

        [Test]
        public void TurningPaddleCatchesFoodBeforeItCanPassThrough()
        {
            Vector2 position=new Vector2(100,-70),velocity=Vector2.zero;
            Assert.That(FoodMotion.Advance(ref position,ref velocity,Vector2.zero,0,-45,170,28,0.05f),Is.True);
            Assert.That(SurfaceDistance(position,-45),Is.GreaterThanOrEqualTo(37.9f));
        }

        [TestCase(65f,-105f)]
        [TestCase(-65f,105f)]
        public void FoodLeavesTheRoundedTipInsteadOfStickingAfterABounce(float angle,float x)
        {
            Vector2 position=new Vector2(x,-345),velocity=new Vector2(0,85);
            bool bounced=false;
            for(int frame=0;frame<240;frame++)
                if(FoodMotion.Advance(ref position,ref velocity,Vector2.zero,angle,angle,170,28,1f/60))
                { bounced=true;break; }
            Assert.That(bounced,Is.True);
            Vector2 firstContact=position;
            for(int frame=0;frame<60;frame++)
                FoodMotion.Advance(ref position,ref velocity,Vector2.zero,angle,angle,170,28,1f/60);
            Assert.That(Vector2.Distance(position,firstContact),Is.GreaterThan(50));
            Assert.That((position.x-firstContact.x)*Mathf.Sign(angle),Is.GreaterThan(20));
            Assert.That(SurfaceDistance(position,angle),Is.GreaterThan(38));
        }

        [Test]
        public void ContactRemainsEnabledImmediatelyAfterABounce()
        {
            Vector2 position=new Vector2(100,-70),velocity=Vector2.zero;
            Assert.That(FoodMotion.Advance(ref position,ref velocity,Vector2.zero,0,-45,170,28,0.05f),Is.True);
            Assert.That(FoodMotion.Advance(ref position,ref velocity,Vector2.zero,-45,-60,170,28,0.05f),Is.True);
            Assert.That(SurfaceDistance(position,-60),Is.GreaterThanOrEqualTo(37.9f));
        }

        [Test]
        public void PausedStepDoesNotMoveFoodOrCreateAnImpact()
        {
            Vector2 position=new Vector2(100,-70),velocity=new Vector2(10,50);
            Assert.That(FoodMotion.Advance(ref position,ref velocity,Vector2.zero,0,-45,170,28,0),Is.False);
            Assert.That(position,Is.EqualTo(new Vector2(100,-70)));
            Assert.That(velocity,Is.EqualTo(new Vector2(10,50)));
        }

        static float SurfaceDistance(Vector2 point,float angle)
        {
            float radians=angle*Mathf.Deg2Rad;
            Vector2 axis=new Vector2(Mathf.Cos(radians),Mathf.Sin(radians));
            return Vector2.Distance(point,axis*Mathf.Clamp(Vector2.Dot(point,axis),-170,170));
        }
    }
}
