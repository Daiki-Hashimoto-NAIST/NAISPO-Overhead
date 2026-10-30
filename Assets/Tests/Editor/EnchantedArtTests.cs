using System;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Naispo.Overhead.Tests
{
    public sealed class EnchantedArtTests
    {
        const BindingFlags Members=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
        GameObject host;
        OverheadApp app;

        [SetUp]
        public void SetUp()
        {
            host=new GameObject("Enchanted art test");
            host.SetActive(false);
            app=host.AddComponent<OverheadApp>();
        }

        [TearDown]
        public void TearDown()
        {
            if(host!=null)UnityEngine.Object.DestroyImmediate(host);
        }

        [Test]
        public void EnchantedTexturesLoadAtSourceSizeWithTheApprovedImporter()
        {
            Invoke("LoadEnchantedArt");

            AssertTexture("background","GlassRoomBackground",1586,992);
            AssertTexture("branch","Branch",2172,724);
            AssertTexture("strawberry","Strawberry",1254,1254);
            AssertTexture("fish","Fish",1254,1254);
            AssertTexture("lantern","Lantern",1254,1254);
            AssertSpiritTexture("leftMonster","LeftNormal",1024,1536);
            AssertSpiritTexture("leftMonster","LeftHappy",1024,1536,1);
            AssertSpiritTexture("rightMonster","RightNormal",1190,1322);
            AssertSpiritTexture("leftMonster","LeftPuzzled",1024,1536,2);
            AssertSpiritTexture("rightMonster","RightHappy",1190,1322,1);
            AssertSpiritTexture("rightMonster","RightPuzzled",1189,1323,2);

            foreach(string name in new[]{"GlassRoomBackground","Branch","LeftNormal","RightNormal",
                "LeftHappy","LeftPuzzled","RightHappy","RightPuzzled","Strawberry","Fish","Lantern"})
            {
                var importer=(TextureImporter)AssetImporter.GetAtPath("Assets/Resources/Enchanted/"+name+".png");
                Assert.That(importer,Is.Not.Null,name);
                Assert.That(importer.textureType,Is.EqualTo(TextureImporterType.Default),name);
                Assert.That(importer.sRGBTexture,Is.True,name);
                Assert.That(importer.mipmapEnabled,Is.False,name);
                Assert.That(importer.filterMode,Is.EqualTo(FilterMode.Bilinear),name);
                Assert.That(importer.wrapMode,Is.EqualTo(TextureWrapMode.Clamp),name);
                Assert.That(importer.npotScale,Is.EqualTo(TextureImporterNPOTScale.None),name);
                Assert.That(importer.textureCompression,Is.EqualTo(TextureImporterCompression.Uncompressed),name);
                Assert.That(importer.maxTextureSize,Is.EqualTo(4096),name);
                Assert.That(importer.alphaIsTransparency,Is.True,name);
            }
        }

        [Test]
        public void BothSpiritsLoadNormalHappyAndPuzzledExpressions()
        {
            Invoke("LoadEnchantedArt");

            AssertFaces("leftMonster","LeftNormal","LeftHappy","LeftPuzzled");
            AssertFaces("rightMonster","RightNormal","RightHappy","RightPuzzled");
        }

        [Test]
        public void LeftHappyKeepsItsLeafAndAlignsFeetAndMouthWithoutRescalingTheBody()
        {
            Rect reference=Get<Rect>(Get<object>(app,"leftMonster"),"Crop");
            const BindingFlags shared=BindingFlags.Static|BindingFlags.NonPublic;
            Rect source=(Rect)typeof(OverheadApp).GetField("LeftHappyCrop",shared).GetValue(null);
            Vector2 offset=(Vector2)typeof(OverheadApp).GetField("LeftHappyOffset",shared).GetValue(null);
            Rect sprite=Static<Rect>("MonsterFaceRect",reference,source,offset);
            float scale=Static<Vector2>("MonsterFit",reference).x/reference.width;

            // Measured visible bounds: (30,83)-(994,1393). Only three alpha=1 pixels lie outside.
            Assert.That(source.xMin,Is.LessThanOrEqualTo(30));
            Assert.That(source.yMin,Is.LessThanOrEqualTo(83));
            Assert.That(source.xMax,Is.GreaterThanOrEqualTo(994));
            Assert.That(source.yMax,Is.GreaterThanOrEqualTo(1393));
            Assert.That(sprite.width/source.width,Is.EqualTo(scale).Within(0.000001f));
            Assert.That(sprite.height/source.height,Is.EqualTo(scale).Within(0.000001f));
            Assert.That(sprite.y+(1394-source.y)*scale,Is.EqualTo(0).Within(0.3f),
                "Happy feet must remain on the normal drawing's baseline.");
            Vector2 happyMouth=sprite.position+(new Vector2(516,753)-source.position)*scale;
            Vector2 normalMouth=Static<Vector2>("MonsterMouthLocal",reference,new Vector2(519,765));
            Assert.That(Vector2.Distance(happyMouth,normalMouth),Is.LessThan(1f));

            Vector2 fit=Static<Vector2>("MonsterFit",reference);
            Rect normal=Static<Rect>("MonsterFaceRect",reference,reference,Vector2.zero);
            Assert.That(normal.x,Is.EqualTo(-fit.x/2).Within(0.0001f));
            Assert.That(normal.y,Is.EqualTo(-fit.y).Within(0.0001f));
            Assert.That(normal.width,Is.EqualTo(fit.x).Within(0.0001f));
            Assert.That(normal.height,Is.EqualTo(fit.y).Within(0.0001f));
        }

        [Test]
        public void CropUvKeepsThePaintedTopAboveTheBottomEdge()
        {
            // Left crop (34,157)-(990,1405) inside a 1024x1536 painting.
            Rect uv=Static<Rect>("CropUv",new Rect(34,157,956,1248),1024f,1536f);

            Assert.That(uv.x,Is.EqualTo(34f/1024f).Within(0.000001f));
            Assert.That(uv.y,Is.EqualTo(1f-1405f/1536f).Within(0.000001f));
            Assert.That(uv.width,Is.EqualTo(956f/1024f).Within(0.000001f));
            Assert.That(uv.height,Is.EqualTo(1248f/1536f).Within(0.000001f));
            Assert.That(uv.yMax,Is.EqualTo(1f-157f/1536f).Within(0.000001f),
                "The top of the crop must stay near the top of the texture.");
            Assert.That(uv.y,Is.LessThan(uv.yMax),"UVs must not be vertically flipped.");
        }

        [Test]
        public void BothSpiritsFitInside248By280AndKeepTheirMeasuredMouths()
        {
            Rect leftCrop=Get<Rect>(Get<object>(app,"leftMonster"),"Crop");
            Rect rightCrop=Get<Rect>(Get<object>(app,"rightMonster"),"Crop");
            Vector2 left=Static<Vector2>("MonsterFit",leftCrop);
            Vector2 right=Static<Vector2>("MonsterFit",rightCrop);

            Assert.That(left.x,Is.EqualTo(214.144f).Within(0.001f));
            Assert.That(left.y,Is.EqualTo(280f).Within(0.001f));
            Assert.That(right.x,Is.EqualTo(248f).Within(0.001f));
            Assert.That(right.y,Is.EqualTo(223.746f).Within(0.001f));

            Vector2 leftMouth=Static<Vector2>("MonsterMouthLocal",
                leftCrop,new Vector2(519,765));
            Vector2 rightMouth=Static<Vector2>("MonsterMouthLocal",
                rightCrop,new Vector2(604,775));
            // Source-pixel measurements fitted in single precision can differ by one float ULP.
            Assert.That(leftMouth.x,Is.EqualTo(1.568f).Within(0.0001f));
            Assert.That(leftMouth.y,Is.EqualTo(-143.584f).Within(0.0001f));
            Assert.That(rightMouth.x,Is.EqualTo(1.638767f).Within(0.0001f));
            Assert.That(rightMouth.y,Is.EqualTo(-96.796476f).Within(0.0001f));
        }

        [Test]
        public void MouthFollowsTheAnimatedPoseDuringAReaction()
        {
            object monster=Get<object>(app,"leftMonster");
            Set(monster,"Expression",1);
            Set(monster,"ReactedAt",9.5f);
            Set(monster,"Attention",0.6f);
            Set(monster,"LookDirection",0.4f);
            Set(app,"playTime",10f);

            object pose=Invoke<object>("GetMonsterPose",monster,false,165f);
            Matrix4x4 transform=Get<Matrix4x4>(pose,"Transform");
            Vector2 local=Static<Vector2>("MonsterMouthLocal",
                Get<Rect>(monster,"Crop"),new Vector2(519,765));
            Vector2 expected=transform.MultiplyPoint3x4(local);
            Vector2 actual=Invoke<Vector2>("MonsterMouthPosition",false);

            Assert.That(Vector2.Distance(actual,expected),Is.LessThan(0.00001f),
                "Reaction food must still arrive at the animated mouth.");
        }

        [Test]
        public void BackgroundFitsTheCanvasWithoutStretching()
        {
            Rect fitted=Static<Rect>("Fit",new Rect(0,0,1440,900),1586,992);

            Assert.That(fitted.height,Is.EqualTo(900f).Within(0.001f));
            Assert.That(fitted.width,Is.EqualTo(1586f*900f/992f).Within(0.001f));
            Assert.That(fitted.center.x,Is.EqualTo(720f).Within(0.001f));
            Assert.That(fitted.center.y,Is.EqualTo(450f).Within(0.001f));
        }

        [Test]
        public void BranchKeepsItsSourceAnchorSizeAtEveryTilt()
        {
            Vector2 pivot=new Vector2(505,530);
            Rect rect=Static<Rect>("BranchRect",pivot);

            Assert.That(rect.width,Is.EqualTo(360f).Within(0.0001f));
            Assert.That(rect.height,Is.EqualTo(113f*360f/2062f).Within(0.0001f));
            Assert.That(rect.height,Is.EqualTo(19.728f).Within(0.001f));

            Vector2 anchor=Static<Vector2>("BranchAnchorPoint",rect);
            foreach(float angle in new[]{0f,65f,-65f})
            {
                Matrix4x4 transform=Static<Matrix4x4>("BranchTransform",pivot,angle);
                Vector2 moved=transform.MultiplyPoint3x4(anchor);
                Assert.That(Vector2.Distance(moved,pivot),Is.LessThan(0.00001f),
                    "The painted centre stone must stay on the pivot at "+angle+" degrees.");
            }
        }

        void AssertTexture(string field,string name,int width,int height)
        {
            Texture2D texture=Get<Texture2D>(app,field);
            Assert.That(texture,Is.Not.Null,name);
            Assert.That(texture.width,Is.EqualTo(width),name);
            Assert.That(texture.height,Is.EqualTo(height),name);
        }

        void AssertSpiritTexture(string monsterField,string name,int width,int height,int expression=0)
        {
            object monster=Get<object>(app,monsterField);
            Texture2D texture=Get<Texture2D[]>(monster,"Faces")[expression];
            Assert.That(texture,Is.Not.Null,name);
            Assert.That(texture.width,Is.EqualTo(width),name);
            Assert.That(texture.height,Is.EqualTo(height),name);
        }

        void AssertFaces(string monsterField,params string[] names)
        {
            object monster=Get<object>(app,monsterField);
            var faces=Get<Texture2D[]>(monster,"Faces");
            Assert.That(faces,Has.Length.EqualTo(3));
            for(int expression=0;expression<3;expression++)
            {
                Texture2D expected=Resources.Load<Texture2D>("Enchanted/"+names[expression]);
                Assert.That(expected,Is.Not.Null,names[expression]);
                Assert.That(faces[expression],Is.SameAs(expected),names[expression]);
            }
        }

        object Invoke(string method,params object[] arguments) =>
            Invoke<object>(method,arguments);

        T Invoke<T>(string method,params object[] arguments) =>
            (T)typeof(OverheadApp).GetMethod(method,Members).Invoke(app,arguments);

        static T Static<T>(string method,params object[] arguments) =>
            (T)typeof(OverheadApp).GetMethod(method,BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic)
                .Invoke(null,arguments);

        static T Get<T>(object instance,string field) =>
            (T)instance.GetType().GetField(field,Members).GetValue(instance);

        static void Set(object instance,string field,object value) =>
            instance.GetType().GetField(field,Members).SetValue(instance,value);
    }
}
