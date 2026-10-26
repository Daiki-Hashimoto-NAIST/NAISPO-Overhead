using UnityEngine;

namespace Naispo.Overhead
{
    public sealed partial class OverheadApp
    {
        // Completion keeps the whole room breathing slowly without a screen-wide flash.
        float FinaleBreath => garden.IsComplete ? Mathf.Sin(playTime*Mathf.PI*2f/6f)*0.02f : 0f;

        // The room lights are presentation only: every value comes from the milestone
        // timestamps already recorded by GardenProgress and the shared session clock.
        void DrawLanternRoom()
        {
            Matrix4x4 originalMatrix=GUI.matrix;
            Color originalColor=GUI.color;
            try
            {
                DrawRoomAwakening();
                DrawLanternSpills();
                DrawSideLantern(false);
                DrawSideLantern(true);
                DrawSatelliteLanterns();
                DrawArchLanterns();
                DrawCentralLantern();
                DrawLanternTravelers();
            }
            finally
            {
                GUI.matrix=originalMatrix;
                GUI.color=originalColor;
            }
        }

        void DrawLanternSpills()
        {
            LanternRoom.MilestoneLight left=LanternRoom.Evaluate(garden,0,playTime);
            if(left.Brightness>0)
                LanternSpill(LanternRoom.LeftGlass,174,210,left.Brightness);
            LanternRoom.MilestoneLight right=LanternRoom.Evaluate(garden,1,playTime);
            if(right.Brightness>0)
                LanternSpill(LanternRoom.RightGlass,174,210,right.Brightness);
            LanternRoom.MilestoneLight arch=LanternRoom.Evaluate(garden,2,playTime);
            if(arch.Brightness<=0)return;
            for(int i=0;i<LanternRoom.ArchGlasses.Length;i++)
                LanternSpill(LanternRoom.ArchGlasses[i],32,32,arch.Brightness);
        }

        void LanternSpill(Vector2 glass,float width,float height,float brightness)
        {
            Glow(new Rect(glass.x-width*0.5f,glass.y-height*0.5f,width,height),
                new Color(candle.r,candle.g,candle.b,0.10f*Mathf.Clamp01(brightness)));
        }

        void DrawSideLantern(bool right)
        {
            int stage=right ? 1 : 0;
            LanternRoom.MilestoneLight light=LanternRoom.Evaluate(garden,stage,playTime);
            Rect body=right ? LanternRoom.RightBody : LanternRoom.LeftBody;
            Vector2 outer=right ? LanternRoom.RightBracketOuter : LanternRoom.LeftBracketOuter;
            Vector2 inner=right ? LanternRoom.RightBracketInner : LanternRoom.LeftBracketInner;
            Vector2 ring=LanternRoom.RingPoint(body);
            Matrix4x4 originalMatrix=GUI.matrix;
            Color originalColor=GUI.color;
            try
            {
                GUI.matrix=originalMatrix;
                GUI.color=Color.white;
                // A short wall plate, bracket and chain make the hanging body read as fixed.
                Rounded(new Rect(outer.x-5,outer.y-7,10,14),4,Rgb(0x493C35));
                Capsule(outer,inner,2.6f,Rgb(0x92764B,0.90f));
                Capsule(inner,ring,1.4f,Rgb(0x92764B,0.75f));
                GUI.color=LanternRoom.Tint(light.Brightness);
                DrawLanternBody(body);
            }
            finally
            {
                GUI.matrix=originalMatrix;
                GUI.color=originalColor;
            }
        }

        void DrawArchLanterns()
        {
            LanternRoom.MilestoneLight light=LanternRoom.Evaluate(garden,2,playTime);
            Color tint=LanternRoom.Tint(light.Brightness);
            Matrix4x4 originalMatrix=GUI.matrix;
            Color originalColor=GUI.color;
            try
            {
                GUI.matrix=originalMatrix;
                for(int i=0;i<LanternRoom.ArchAnchors.Length;i++)
                {
                    Vector2 anchor=LanternRoom.ArchAnchors[i];
                    Rect body=LanternRoom.ArchBodies[i];
                    Vector2 ring=LanternRoom.RingPoint(body);
                    GUI.color=Color.white;
                    Rounded(new Rect(anchor.x-3,anchor.y-2,6,5),2,Rgb(0x493C35));
                    Capsule(anchor,ring,1.1f,Rgb(0x92764B,0.80f));
                    GUI.color=tint;
                    DrawLanternBody(body);
                }
            }
            finally
            {
                GUI.matrix=originalMatrix;
                GUI.color=originalColor;
            }
        }

        void DrawCentralLantern()
        {
            Matrix4x4 originalMatrix=GUI.matrix;
            Color originalColor=GUI.color;
            try
            {
                // Chain, fitting, body, ring, face and glow all share this one sway.
                GUI.matrix=originalMatrix*LanternRoom.CentralTransform(playTime);
                GUI.color=Color.white;
                Vector2 glass=LanternRoom.CentralGlass;
                Glow(new Rect(glass.x-48,glass.y-54,96,108),
                    new Color(candle.r,candle.g,candle.b,0.08f));
                Rounded(new Rect(LanternRoom.Ceiling.x-6,LanternRoom.Ceiling.y-3,12,6),2,Rgb(0x493C35));
                Capsule(LanternRoom.Ceiling,LanternRoom.CentralRing,2.2f,Rgb(0x6B5A45,0.95f));
                GUI.color=LanternRoom.LitTint;
                DrawLanternBody(LanternRoom.CentralBody);
                DrawCentralLanternFace();
            }
            finally
            {
                GUI.matrix=originalMatrix;
                GUI.color=originalColor;
            }
        }

        void DrawCentralLanternFace()
        {
            Vector2 glass=LanternRoom.CentralGlass;
            float mood=Mathf.Clamp(cloudMood,-1f,1f);
            float happy=Mathf.Max(0,mood),sad=Mathf.Max(0,-mood);
            float blink=LanternRoom.BlinkAmount(playTime);
            Color face=Rgb(0x59433E);
            // The same small brown face as the spirits: round eyes and a soft mouth.
            for(int side=-1;side<=1;side+=2)
            {
                Vector2 eye=new Vector2(glass.x+side*12,glass.y-7-happy*1.2f+sad*1.4f);
                if(blink>0.5f)
                    Capsule(eye+new Vector2(-3.2f,0),eye+new Vector2(3.2f,0),2.4f,face);
                else
                    Circle(eye,3.1f-happy*0.5f,face);
            }
            FaceStroke(new Vector2(glass.x,glass.y+10+happy*0.5f-sad*1.0f),
                8.5f+happy*1.5f,2.2f+mood*4.2f,0,2.4f,face);
        }

        void DrawLanternTravelers()
        {
            Matrix4x4 originalMatrix=GUI.matrix;
            Color originalColor=GUI.color;
            try
            {
                GUI.matrix=originalMatrix;
                GUI.color=Color.white;
                for(int stage=0;stage<GardenProgress.StageCount;stage++)
                {
                    LanternRoom.MilestoneLight light=LanternRoom.Evaluate(garden,stage,playTime);
                    if(!light.Started||light.Arrived)continue;
                    Vector2 point=LanternRoom.TravelPoint(stage,light.TravelT,light.StartedAt);
                    // One core and one halo, not a trail or a particle burst.
                    Glow(new Rect(point.x-14,point.y-14,28,28),
                        new Color(candle.r,candle.g,candle.b,0.18f));
                    Circle(point,3.8f,Rgb(0xFFF3CC,0.95f));
                }
            }
            finally
            {
                GUI.matrix=originalMatrix;
                GUI.color=originalColor;
            }
        }

        void DrawLanternBody(Rect body)
        {
            if(lantern==null)
            {
                // A missing texture must leave a readable, opaque fixture without logging
                // once per IMGUI pass.
                GUI.DrawTexture(body,Texture2D.whiteTexture);
                return;
            }
            GUI.DrawTextureWithTexCoords(body,lantern,
                CropUv(LanternRoom.Crop,lantern.width,lantern.height),true);
        }
    }

    /// <summary>
    /// Pure lantern-room geometry and timing. Drawing and tests read the same values,
    /// and every state is derived from GardenProgress plus the caller's session clock.
    /// </summary>
    public static class LanternRoom
    {
        public const float FlightDuration=1.10f;
        public const float FadeDuration=0.65f;
        public const float SwayAmplitude=0.65f;
        public const float SwayPeriod=6.8f;
        public const float BlinkPeriod=4.6f;
        public const float BlinkDuration=0.13f;
        public const float SideHeight=96f;
        public const float SideWidth=81.583f;
        public const float ArchHeight=28f;
        public const float ArchWidth=23.795f;

        public static readonly Rect Crop=new Rect(145,57,962,1132);
        public static readonly Vector2 SourceGlass=new Vector2(625,768);
        public static readonly Vector2 SourceRing=new Vector2(625,79);
        public static readonly Vector2 Ceiling=new Vector2(720,24);
        public static readonly Color OffTint=new Color(0.24f,0.27f,0.26f,1f);
        public static readonly Color LitTint=Color.white;

        public static readonly Vector2 CentralGlass=new Vector2(720,151.007f);
        public static readonly Rect CentralBody=BodyFromGlass(CentralGlass,139.371f,164f);
        public static readonly Vector2 CentralRing=RingPoint(CentralBody);

        public static readonly Vector2 LeftGlass=new Vector2(206,400.297f);
        public static readonly Vector2 RightGlass=new Vector2(1234,400.297f);
        public static readonly Rect LeftBody=BodyFromGlass(LeftGlass,SideWidth,SideHeight);
        public static readonly Rect RightBody=BodyFromGlass(RightGlass,SideWidth,SideHeight);
        public static readonly Vector2 LeftBracketOuter=new Vector2(110,324);
        public static readonly Vector2 LeftBracketInner=new Vector2(206,324);
        public static readonly Vector2 RightBracketOuter=new Vector2(1330,324);
        public static readonly Vector2 RightBracketInner=new Vector2(1234,324);

        public static readonly Vector2[] ArchAnchors=BuildArchAnchors();
        public static readonly Rect[] ArchBodies=BuildArchBodies();
        public static readonly Vector2[] ArchGlasses=BuildArchGlasses();
        public static readonly Vector2 ArchGlassTarget=ArchGlasses[2];

        static readonly Vector2 LeftControl=new Vector2(470,190);
        static readonly Vector2 RightControl=new Vector2(970,190);
        static readonly Vector2 ArchControl=new Vector2(650,100);

        public static bool IsFinite(float value) => !float.IsNaN(value)&&!float.IsInfinity(value);

        public static float SwayDegrees(float playTime)
        {
            if(!IsFinite(playTime))return 0;
            return SwayAmplitude*Mathf.Sin(Mathf.PI*2f*playTime/SwayPeriod);
        }

        public static Matrix4x4 CentralTransform(float playTime) =>
            GameLayout.RotationAround(Ceiling,SwayDegrees(playTime));

        public static float BlinkAmount(float playTime)
        {
            if(!IsFinite(playTime))return 0;
            float phase=Mathf.Repeat(playTime,BlinkPeriod);
            if(phase>=BlinkDuration)return 0;
            return Mathf.Sin(phase/BlinkDuration*Mathf.PI);
        }

        public static Color Tint(float brightness) =>
            Color.Lerp(OffTint,Color.white,Mathf.Clamp01(brightness));

        public static MilestoneLight Evaluate(GardenProgress garden,int stage,float playTime)
        {
            if(garden==null||stage<0||stage>=GardenProgress.StageCount)return default;
            float startedAt=garden.BloomStartedAt(stage);
            // Check both operands before subtraction so NaN or infinity can never leak in.
            if(!IsFinite(startedAt)||!IsFinite(playTime))return default;
            float elapsed=playTime-startedAt;
            if(elapsed<0)return default;
            // Judge arrival against an absolute deadline on the game clock. Comparing
            // elapsed>=FlightDuration can miss an arrival exactly at the scheduled time
            // because the subtraction rounds (101.1f-100f == 1.09999847f < 1.10f).
            // The same deadline drives the fade so brightness stays 0 at arrival.
            float arrivalAt=startedAt+FlightDuration;
            bool hasDeadline=IsFinite(arrivalAt);
            bool arrived=hasDeadline ? playTime>=arrivalAt : elapsed>=FlightDuration;
            float fadeElapsed=hasDeadline ? playTime-arrivalAt : elapsed-FlightDuration;
            float travel=arrived ? 1 : Mathf.Clamp01(elapsed/FlightDuration);
            float brightness=arrived
                ? Mathf.SmoothStep(0,1,Mathf.Clamp01(fadeElapsed/FadeDuration))
                : 0;
            return new MilestoneLight(stage,startedAt,true,arrived,elapsed,travel,brightness);
        }

        public static Vector2 TravelPoint(int stage,float t,float startedAt)
        {
            Vector2 start=CentralTransform(startedAt)
                .MultiplyPoint3x4(new Vector3(CentralGlass.x,CentralGlass.y,0));
            Vector2 control=stage==0 ? LeftControl : stage==1 ? RightControl : ArchControl;
            Vector2 end=stage==0 ? LeftGlass : stage==1 ? RightGlass : ArchGlassTarget;
            float u=Mathf.Clamp01(t),v=1-u;
            return v*v*start+2*v*u*control+u*u*end;
        }

        public static Vector2 SourcePoint(Vector2 source,Rect body)
        {
            return new Vector2(body.x+(source.x-Crop.x)/Crop.width*body.width,
                body.y+(source.y-Crop.y)/Crop.height*body.height);
        }

        public static Vector2 GlassPoint(Rect body) => SourcePoint(SourceGlass,body);
        public static Vector2 RingPoint(Rect body) => SourcePoint(SourceRing,body);

        static Rect BodyFromGlass(Vector2 glass,float width,float height) =>
            new Rect(glass.x-(SourceGlass.x-Crop.x)/Crop.width*width,
                glass.y-(SourceGlass.y-Crop.y)/Crop.height*height,width,height);

        static Rect BodyFromRing(Vector2 ring,float width,float height) =>
            new Rect(ring.x-(SourceRing.x-Crop.x)/Crop.width*width,
                ring.y-(SourceRing.y-Crop.y)/Crop.height*height,width,height);

        static Vector2[] BuildArchAnchors()
        {
            float[] degrees={200,220,240,300,320,340};
            var anchors=new Vector2[degrees.Length];
            for(int i=0;i<degrees.Length;i++)
            {
                float radians=degrees[i]*Mathf.Deg2Rad;
                anchors[i]=new Vector2(720+232*Mathf.Cos(radians),190+70*Mathf.Sin(radians));
            }
            return anchors;
        }

        static Rect[] BuildArchBodies()
        {
            var bodies=new Rect[ArchAnchors.Length];
            for(int i=0;i<bodies.Length;i++)
                bodies[i]=BodyFromRing(ArchAnchors[i]+new Vector2(0,8),ArchWidth,ArchHeight);
            return bodies;
        }

        static Vector2[] BuildArchGlasses()
        {
            var glasses=new Vector2[ArchBodies.Length];
            for(int i=0;i<glasses.Length;i++)glasses[i]=GlassPoint(ArchBodies[i]);
            return glasses;
        }

        public readonly struct MilestoneLight
        {
            public readonly int Stage;
            public readonly float StartedAt,Elapsed,TravelT,Brightness;
            public readonly bool Started,Arrived;

            public MilestoneLight(int stage,float startedAt,bool started,bool arrived,
                float elapsed,float travelT,float brightness)
            {
                Stage=stage;StartedAt=startedAt;Started=started;Arrived=arrived;
                Elapsed=elapsed;TravelT=travelT;Brightness=brightness;
            }
        }
    }
}
