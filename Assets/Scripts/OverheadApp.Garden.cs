using UnityEngine;

namespace Naispo.Overhead
{
    public sealed partial class OverheadApp
    {
        Texture2D sleepBubbleIcon;
        static readonly Vector2[] ButterflyAnchors={
            new Vector2(505,214),new Vector2(932,198),new Vector2(382,238)
        };
        static readonly Color[] ButterflyColors={
            Rgb(0xE79AAE),Rgb(0xF3D074),Rgb(0xB8A1D6)
        };

        // Completion keeps the whole room breathing slowly without a screen-wide flash.
        float FinaleBreath => FinaleStarted ? Mathf.Sin(playTime*Mathf.PI*2f/6f)*0.02f : 0f;

        // The room lights are presentation only: every value comes from the delivery records
        // and the shared session clock.
        void DrawLanternRoom()
        {
            Matrix4x4 originalMatrix=GUI.matrix;
            Color originalColor=GUI.color;
            try
            {
                DrawRoomFixtures();
                DrawCentralLantern();
                DrawLanternTravelers();
                if(FinaleStarted)DrawButterflies();
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
                float light=GardenAtmosphere.Progress(garden,playTime);
                Matrix4x4 pose=LanternRoom.CentralTransform(LanternTime,light);
                // The ceiling fitting stays fixed while the chain follows the moving
                // ring. Body, face and halo all share the same sway and final lift.
                GUI.matrix=originalMatrix;
                GUI.color=Color.white;
                Rounded(new Rect(LanternRoom.Ceiling.x-6,LanternRoom.Ceiling.y-3,12,6),2,Rgb(0x493C35));
                Vector2 ring=pose.MultiplyPoint3x4(LanternRoom.CentralRing);
                Capsule(LanternRoom.Ceiling,ring,2.2f,Rgb(0x6B5A45,0.95f));
                GUI.matrix=originalMatrix*pose;
                GUI.color=Color.white;
                Vector2 glass=LanternRoom.CentralGlass;
                Glow(new Rect(glass.x-65,glass.y-72,130,144),
                    new Color(candle.r,candle.g,candle.b,0.035f+light*0.23f));
                // Dim at the intro and warm continuously as successes reach the room.
                GUI.color=LanternRoom.Tint(0.23f+0.77f*light);
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
            float wake=LanternRoom.WakeAmount(garden,playTime);
            float mood=Mathf.Clamp(cloudMood,-1f,1f)*wake;
            float happy=Mathf.Max(0,mood),sad=Mathf.Max(0,-mood);
            float drowsy=Mathf.Clamp01((1-wake)/0.35f);
            float blink=LanternRoom.BlinkAmount(LanternTime,drowsy);
            Color face=Rgb(0x59433E);
            // The first half of the round wakes the lantern; it watches the remaining
            // deliveries with open eyes while the room continues to brighten.
            for(int side=-1;side<=1;side+=2)
            {
                Vector2 eye=new Vector2(glass.x+side*12,glass.y-7-happy*1.2f+sad*1.4f);
                if(wake<0.06f||blink>0.5f)
                    Capsule(eye+new Vector2(-3.2f,0),eye+new Vector2(3.2f,0),2.4f,face);
                else
                {
                    float height=Mathf.Lerp(2.4f,6.2f-happy,wake*wake);
                    Oval(new Rect(eye.x-3.1f,eye.y-height*0.5f,6.2f,height),face);
                    if(drowsy>0)
                    {
                        // A wider, drooping upper lid reads as sleepy even in a small window.
                        Vector2 lid=eye-new Vector2(0,height*0.30f);
                        Capsule(lid-new Vector2(4.6f,0),lid+new Vector2(4.6f,0),
                            1.5f,WithAlpha(face,drowsy));
                    }
                }
            }
            FaceStroke(new Vector2(glass.x,glass.y+10+happy*0.5f-sad*1.0f),
                Mathf.Lerp(6,8.5f,wake)+happy*1.5f,Mathf.Lerp(0.4f,2.2f,wake)+mood*4.2f,0,2.4f,face);

            float bubble=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(0.12f,0.50f,wake));
            if(bubble>0)
            {
                float breath=LanternRoom.SleepBreath(LanternTime);
                float size=Mathf.Lerp(22,30,breath);
                DrawIcon(sleepBubbleIcon,glass+new Vector2(23,12),Vector2.one*size,
                    Rgb(0xD4F1EF,bubble*0.90f));
            }
        }

        void DrawLanternTravelers()
        {
            Matrix4x4 originalMatrix=GUI.matrix;
            Color originalColor=GUI.color;
            try
            {
                GUI.matrix=originalMatrix;
                GUI.color=Color.white;
                float progress=GardenAtmosphere.Progress(garden,playTime);
                for(int i=0;i<garden.LightCount;i++)
                {
                    LanternRoom.LightFlight light=LanternRoom.Evaluate(garden,i,playTime);
                    if(!light.Started)continue;
                    if(!light.Arrived)
                    {
                        Vector2 point=LanternRoom.TravelPoint(light.Source,light.Right,light.TravelT,
                            LanternTime,progress);
                        // One core and one halo, not a trail or a particle burst.
                        Glow(new Rect(point.x-14,point.y-14,28,28),
                            new Color(candle.r,candle.g,candle.b,0.18f));
                        Circle(point,3.8f,Rgb(0xFFF3CC,0.95f));
                        continue;
                    }
                    float pulse=LanternRoom.ArrivalPulse(garden,i,playTime);
                    if(pulse<=0)continue;
                    Vector2 origin=LanternRoom.CentralGlassAt(LanternTime,progress);
                    float age=Mathf.Max(0,light.Elapsed-LanternRoom.FlightDuration);
                    float radius=Mathf.Lerp(12,47,Mathf.Clamp01(age/1.35f));
                    Arc(origin,radius,radius*1.05f,0,360,28,1.6f,Rgb(0xFFE7A9,pulse*0.30f));
                    Glow(new Rect(origin.x-57,origin.y-65,114,130),Rgb(0xFFD181,pulse*0.19f));
                }
            }
            finally
            {
                GUI.matrix=originalMatrix;
                GUI.color=originalColor;
            }
        }

        // Three soft native-shape butterflies appear at the windows only for the finale.
        // Their anchors stay above and outside the central falling-food corridor.
        void DrawButterflies()
        {
            for(int i=0;i<ButterflyAnchors.Length;i++)
            {
                Vector2 anchor=ButterflyAnchors[i];
                float phase=playTime*(0.7f+i*0.13f)+i*1.7f;
                float driftX=Mathf.Sin(phase*0.8f)*7f;
                float driftY=Mathf.Cos(phase*0.62f)*4f;
                float wing=3.5f+Mathf.Abs(Mathf.Cos(phase*2.1f))*4.5f;
                Vector2 center=new Vector2(anchor.x+driftX,anchor.y+driftY);
                Color wingColor=WithAlpha(ButterflyColors[i],0.58f);
                Oval(new Rect(center.x-wing*2,center.y-wing,wing*2,wing*2),wingColor);
                Oval(new Rect(center.x+0.5f,center.y-wing,wing*2,wing*2),wingColor);
                Capsule(center-new Vector2(0,3),center+new Vector2(0,3),1.4f,Rgb(0x6B5A45,0.72f));
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
        public const float FlightDuration=GardenProgress.LightFlightDuration;
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

        public static bool IsFinite(float value) => !float.IsNaN(value)&&!float.IsInfinity(value);

        public static float SwayDegrees(float playTime)
        {
            if(!IsFinite(playTime))return 0;
            return SwayAmplitude*Mathf.Sin(Mathf.PI*2f*playTime/SwayPeriod);
        }

        public static Matrix4x4 CentralTransform(float visualTime,float arrivedProgress=0)
        {
            float progress=IsFinite(arrivedProgress) ? Mathf.Clamp01(arrivedProgress) : 0;
            float awake=Mathf.SmoothStep(0,1,Mathf.InverseLerp(0.95f,1,progress));
            float bob=IsFinite(visualTime) ? Mathf.Sin(visualTime*Mathf.PI*2f/5.6f)*2f : 0;
            return Matrix4x4.Translate(new Vector3(0,(-14+0.8f*bob)*awake,0))*
                GameLayout.RotationAround(Ceiling,SwayDegrees(visualTime));
        }

        public static Vector2 CentralGlassAt(float visualTime,float arrivedProgress=0) =>
            CentralTransform(visualTime,arrivedProgress).MultiplyPoint3x4(CentralGlass);

        public static float BlinkAmount(float playTime,float drowsiness=0)
        {
            if(!IsFinite(playTime))return 0;
            float duration=Mathf.Lerp(BlinkDuration,0.52f,Mathf.Clamp01(drowsiness));
            float phase=Mathf.Repeat(playTime,BlinkPeriod);
            if(phase>=duration)return 0;
            return Mathf.Sin(phase/duration*Mathf.PI);
        }

        public static Color Tint(float brightness) =>
            Color.Lerp(OffTint,Color.white,Mathf.Clamp01(brightness));

        public static float WakeAmount(GardenProgress garden,float playTime)
        {
            if(garden==null||!IsFinite(playTime))return 0;
            float progress=GardenAtmosphere.Progress(garden,playTime);
            return Mathf.SmoothStep(0,1,Mathf.InverseLerp(0.10f,0.50f,progress));
        }

        public static float SleepBreath(float time) =>
            IsFinite(time) ? 0.5f-0.5f*Mathf.Cos(time*Mathf.PI*2f/3.8f) : 0.5f;

        public static LightFlight Evaluate(GardenProgress garden,int index,float playTime)
        {
            if(garden==null||index<0||index>=garden.LightCount)return default;
            var record=garden.LightAt(index);
            if(!IsFinite(record.Time)||!IsFinite(playTime))return default;
            float elapsed=playTime-record.Time;
            if(elapsed<0)return default;
            // Judge arrival against an absolute deadline on the game clock. Comparing
            // elapsed>=FlightDuration can miss an arrival exactly at the scheduled time
            // because the subtraction rounds (101.1f-100f == 1.09999847f < 1.10f).
            // The same deadline drives the fade so brightness stays 0 at arrival.
            float arrivalAt=record.Time+FlightDuration;
            bool hasDeadline=IsFinite(arrivalAt);
            bool arrived=hasDeadline ? playTime>=arrivalAt : elapsed>=FlightDuration;
            float fadeElapsed=hasDeadline ? playTime-arrivalAt : elapsed-FlightDuration;
            float travel=arrived ? 1 : Mathf.Clamp01(elapsed/FlightDuration);
            float brightness=arrived
                ? Mathf.SmoothStep(0,1,Mathf.Clamp01(fadeElapsed/FadeDuration))
                : 0;
            return new LightFlight(index,record.Time,record.Right,record.Source,true,arrived,
                elapsed,travel,brightness);
        }

        /// <summary>A cubic path from a spirit's mouth to the central lantern's current glass.</summary>
        public static Vector2 TravelPoint(Vector2 source,bool right,float t,
            float visualTime=0,float arrivedProgress=0)
        {
            Vector2 end=CentralGlassAt(visualTime,arrivedProgress);
            // Rise beside the playfield, then approach the lantern above the falling food.
            Vector2 control1=source+new Vector2(right ? 72f : -72f,-145f);
            Vector2 control2=end+new Vector2(right ? 220f : -220f,50f);
            float u=Mathf.Clamp01(t),v=1-u;
            return v*v*v*source+3*v*v*u*control1+3*v*u*u*control2+u*u*u*end;
        }

        public static Vector2 TravelPoint(GardenProgress garden,int index,float t,
            float visualTime=0,float arrivedProgress=0)
        {
            if(garden==null||index<0||index>=garden.LightCount)return default;
            var record=garden.LightAt(index);
            return TravelPoint(record.Source,record.Right,t,visualTime,arrivedProgress);
        }

        public static float ArrivalPulse(GardenProgress garden,int index,float playTime)
        {
            var light=Evaluate(garden,index,playTime);
            if(!light.Arrived)return 0;
            float elapsed=Mathf.Max(0,light.Elapsed-FlightDuration);
            if(elapsed>=1.35f)return 0;
            return Mathf.Sin(elapsed/1.35f*Mathf.PI);
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

        public readonly struct LightFlight
        {
            public readonly int Index;
            public readonly float StartedAt,Elapsed,TravelT,Brightness;
            public readonly bool Right,Started,Arrived;
            public readonly Vector2 Source;

            public LightFlight(int index,float startedAt,bool right,Vector2 source,
                bool started,bool arrived,float elapsed,float travelT,float brightness)
            {
                Index=index;StartedAt=startedAt;Right=right;Source=source;
                Started=started;Arrived=arrived;Elapsed=elapsed;TravelT=travelT;
                Brightness=brightness;
            }
        }
    }
}
