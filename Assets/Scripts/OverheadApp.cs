using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Naispo.Overhead
{
    public sealed partial class OverheadApp : MonoBehaviour
    {
        readonly DepthAnalyzer analyzer=new DepthAnalyzer();
        readonly List<Token> tokens=new List<Token>();
        readonly FoodDropSchedule dropSchedule=new FoodDropSchedule();
        readonly GardenProgress garden=new GardenProgress();
        // One shared source-pixel crop per spirit keeps all expressions at the same
        // scale, including the few extra edge pixels in the supplied variants.
        readonly Monster leftMonster=new Monster("Left",new Rect(34,156,956,1250));
        readonly Monster rightMonster=new Monster("Right",new Rect(29,194,1135,1024));
        OverheadDepthSource source;
        GameAudio sounds;
        Texture2D dot, preview, strawberry, fish;
        Color32[] previewPixels;
        bool previewMirrored;
        bool live, table, front, mirror=true, invert, ready, hasAngle, paused, settingsOpen, draggingGate;
        float minHeight=300,maxHeight=2200,angle,targetAngle,lastFrameAt=-100,playTime;
        float minDistance=700,maxDistance=3500;
        FrontPose frontPose;
        HandLineResult handLine;
        int score => garden.DeliveredCount;
        int misses;
        const float PivotX=505,PivotY=530,HalfArm=170,FoodSize=56;
        static readonly Vector2 Pivot=new Vector2(PivotX,PivotY);
        static readonly Rect StrawberryCrop=new Rect(236,127,783,1005),FishCrop=new Rect(49,355,1171,526);
        class Token
        {
            public Vector2 Position,Velocity;
            public bool IsFish,HasTouchedGate;
            public float Rotation,SpinSpeed;
            public float HitAt=-100,HitAngle;
        }
        sealed class Monster
        {
            public readonly string Side;
            public readonly Rect Crop;
            public readonly Texture2D[] Faces=new Texture2D[3];
            public int Expression;
            public float ReactedAt=-100;
            public float LookDirection,Attention;
            public Monster(string side,Rect crop) { Side=side;Crop=crop; }
            public void Load()
            {
                Texture2D normal=Resources.Load<Texture2D>("Enchanted/"+Side+"Normal");
                if(normal==null)Debug.LogError("Missing enchanted spirit texture: "+Side+"Normal");
                Faces[0]=normal;
                // Unprovided expressions retain the normal smile; never use old-theme art.
                Faces[1]=Resources.Load<Texture2D>("Enchanted/"+Side+"Happy") ?? normal;
                Faces[2]=Resources.Load<Texture2D>("Enchanted/"+Side+"Puzzled") ?? normal;
            }
            public void React(bool correct,float time) { Expression=correct ? 1 : 2;ReactedAt=time; }
            public void Reset() { Expression=0;ReactedAt=-100;LookDirection=Attention=0; }
        }

        void Awake()
        {
            Application.targetFrameRate=60;
            source=GetComponent<OverheadDepthSource>();
            sounds=gameObject.AddComponent<GameAudio>();
            LoadEnchantedArt();
            dot=new Texture2D(64,64,TextureFormat.RGBA32,false);
            for(int y=0;y<64;y++)for(int x=0;x<64;x++)
            {
                float alpha=Mathf.Clamp01(31-Vector2.Distance(new Vector2(x+0.5f,y+0.5f),new Vector2(32,32)));
                dot.SetPixel(x,y,new Color(1,1,1,alpha));
            }
            dot.Apply();
            InitializeArt();
        }
        void LoadEnchantedArt()
        {
            background=Resources.Load<Texture2D>("Enchanted/Background");
            branch=Resources.Load<Texture2D>("Enchanted/Branch");
            strawberry=Resources.Load<Texture2D>("Enchanted/Strawberry");
            fish=Resources.Load<Texture2D>("Enchanted/Fish");
            lantern=Resources.Load<Texture2D>("Enchanted/Lantern");
            if(background==null)Debug.LogError("Missing enchanted texture: Background");
            if(branch==null)Debug.LogError("Missing enchanted texture: Branch");
            if(strawberry==null)Debug.LogError("Missing enchanted texture: Strawberry");
            if(fish==null)Debug.LogError("Missing enchanted texture: Fish");
            if(lantern==null)Debug.LogError("Missing enchanted texture: Lantern");
            leftMonster.Load();rightMonster.Load();
        }
        void Update()
        {
            float dt=Mathf.Min(Time.deltaTime,0.05f);
            var keyboard=Keyboard.current;
            if(keyboard!=null&&keyboard.escapeKey.wasPressedThisFrame)
            {
                if(settingsOpen)settingsOpen=false;
                else paused=!paused;
                draggingGate=false;
            }
            if(live && source.TryTake(out var depth,out int w,out int h,out var pose))
            {
                if(lastFrameAt<0) Debug.Log($"[Overhead] Live depth ready: {w} x {h}");
                lastFrameAt=Time.unscaledTime;
                if(front)
                {
                    frontPose=pose;
                    handLine=FrontHandControl.Evaluate(pose,minDistance,maxDistance,mirror,invert);
                    ready=handLine.Valid;
                    if(ready)targetAngle=handLine.AngleDegrees;
                }
                else
                {
                    analyzer.Process(depth,w,h,minHeight,maxHeight);
                    ready=analyzer.Calibrated && analyzer.Result.Valid;
                    if(ready)
                    {
                        float raw=analyzer.Result.AngleDegrees*(mirror ? -1 : 1)*(invert ? -1 : 1);
                        targetAngle=SorterRules.NearestAxis(raw,hasAngle ? targetAngle : 0);
                        hasAngle=true;
                    }
                }
                UpdatePreview(depth,w,h);
            }
            if(live && (Time.unscaledTime-lastFrameAt>0.5f || !source.Running)) ready=false;
            if(!live)ready=true;
            sounds.SetPaused(settingsOpen||paused||(!ready&&!Celebrating&&!garden.IsComplete));
            // Depth capture and calibration continue while the game is paused.
            if(settingsOpen||paused) { draggingGate=false;return; }
            // The finished garden stays alive even when the player lowers their hands.
            if(garden.IsComplete)
            {
                draggingGate=false;
                playTime+=dt;
                UpdateFeedback(dt);
                return;
            }
            if(!live)
            {
                ready=true;
                var kb=Keyboard.current;
                float direction=0;
                if(kb!=null) { if(kb.aKey.isPressed||kb.leftArrowKey.isPressed)direction--; if(kb.dKey.isPressed||kb.rightArrowKey.isPressed)direction++; }
                targetAngle=Mathf.Clamp(targetAngle+direction*70*dt,-65,65);
                if(Mouse.current!=null)
                {
                    Vector2 mouse=Mouse.current.position.ReadValue();
                    mouse=GameLayout.ScreenToCanvas(new Vector2(mouse.x,Screen.height-mouse.y),Screen.width,Screen.height);
                    if(Mouse.current.leftButton.wasPressedThisFrame)draggingGate=GameLayout.PlayArea.Contains(mouse);
                    if(!Mouse.current.leftButton.isPressed)draggingGate=false;
                    mouse.x-=GameLayout.StageOffsetX;
                    if(draggingGate)
                        targetAngle=Mathf.Clamp((mouse.x-PivotX)/5,-65,65);
                }
            }
            // A happy player may lower their hands during the party; finish its animation.
            if(!ready&&!Celebrating) return;
            playTime+=dt;
            UpdateFeedback(dt);
            if(Celebrating||garden.IsComplete||!ready)return;
            float previousAngle=angle;
            angle=Mathf.Lerp(angle,targetAngle,1-Mathf.Exp(-10*dt));
            if(dropSchedule.Advance(dt,tokens.Count+deliveries.Count,out var drop))
            {
                tokens.Add(new Token {
                    Position=new Vector2(PivotX+drop.XOffset,250),Velocity=new Vector2(0,85),IsFish=drop.IsFish,
                    Rotation=Random.Range(-35f,35f),SpinSpeed=Random.Range(50f,100f)*(Random.value<0.5f ? -1 : 1)
                });
            }
            for(int i=tokens.Count-1;i>=0;i--)
            {
                var token=tokens[i];
                bool bounced=FoodMotion.Advance(ref token.Position,ref token.Velocity,
                    Pivot,previousAngle,angle,HalfArm,FoodSize/2,dt,out var impact);
                token.Rotation=Mathf.Repeat(token.Rotation+token.SpinSpeed*dt,360);
                token.SpinSpeed*=Mathf.Exp(-0.18f*dt);
                if(bounced)
                {
                    token.HasTouchedGate=true;
                    float spinDirection=Mathf.Abs(token.Velocity.x)>1 ? Mathf.Sign(token.Velocity.x) : Mathf.Sign(token.SpinSpeed);
                    token.SpinSpeed=spinDirection*(160+Mathf.Abs(token.Velocity.x)*0.45f);
                    if(playTime-token.HitAt>0.16f)
                    {
                        token.HitAt=playTime;token.HitAngle=impact.AngleDegrees;
                        impacts.Add(new Impact { Position=impact.Point,StartedAt=playTime });
                        sounds.PlayBounce(Mathf.InverseLerp(165,845,impact.Point.x));
                    }
                }
                int exit=FoodDeliveryRules.DepartedSide(token.HasTouchedGate,token.Position,token.Velocity,
                    Pivot,HalfArm,FoodSize/2,FoodMotion.GateHalfThickness);
                if(exit==0)exit=SorterRules.ExitSide(token.Position);
                if(exit!=0)
                {
                    if(exit==3)
                    {
                        misses++;
                        ReactCloud(false);
                    }
                    else deliveries.Add(new Delivery {
                        Start=token.Position,IsFish=token.IsFish,Right=exit==2,
                        Correct=(exit==1&&!token.IsFish)||(exit==2&&token.IsFish),
                        StartedAt=playTime,Rotation=token.Rotation
                    });
                    tokens.RemoveAt(i);
                }
            }
        }
        void UpdatePreview(ushort[] depth,int w,int h)
        {
            if(preview==null||preview.width!=w||preview.height!=h)
            {
                if(preview!=null)Destroy(preview);
                preview=new Texture2D(w,h,TextureFormat.RGBA32,false) { filterMode=FilterMode.Point };
                previewPixels=new Color32[w*h];
            }
            for(int y=0;y<h;y++)for(int x=0;x<w;x++)
            {
                int index=y*w+x;
                float value=depth[index]==0 ? 0 : Mathf.Clamp01(1-depth[index]/5000f);
                Color c=new Color(0.04f+value*0.12f,0.06f+value*0.24f,0.09f+value*0.34f);
                if(!front&&analyzer.Mask[index]!=0)c=new Color(0.98f,0.70f,0.29f);
                previewPixels[(h-1-y)*w+(mirror ? w-1-x : x)]=c;
            }
            preview.SetPixels32(previewPixels);preview.Apply(false,false);previewMirrored=mirror;
        }
        void ResetRound()
        {
            tokens.Clear();misses=0;garden.Reset();
            draggingGate=false;angle=targetAngle=0;hasAngle=false;
            dropSchedule.Reset();ResetFeedback();
            leftMonster.Reset();rightMonster.Reset();
        }
        void Connect()
        {
            source.StopCapture();
            live=true;ready=false;hasAngle=false;lastFrameAt=-100;draggingGate=false;
            frontPose=default;handLine=default;
            if(preview!=null)Destroy(preview);
            preview=null;
            source.StartCapture(front);
            if(!front)analyzer.BeginCalibration(30);
            ResetRound();
        }
        void SetCameraMode(bool useFront)
        {
            if(front==useFront)return;
            front=useFront;hasAngle=false;angle=targetAngle=0;frontPose=default;handLine=default;
            if(live)Connect();
        }
        void Calibrate() { analyzer.BeginCalibration(30);ready=false;hasAngle=false;ResetRound(); }
        void OnDestroy()
        {
            // The enchanted textures are Resources-owned; never destroy them here.
            DestroyMeadowArt();
            if(dot!=null)Destroy(dot);if(preview!=null)Destroy(preview);if(paper!=null)Destroy(paper);
            if(warmPaper!=null)Destroy(warmPaper);if(glow!=null)Destroy(glow);
            if(heartIcon!=null)Destroy(heartIcon);if(tangleIcon!=null)Destroy(tangleIcon);if(sparkleIcon!=null)Destroy(sparkleIcon);
            if(magicStarIcon!=null)Destroy(magicStarIcon);
        }
    }

    public static class SorterRules
    {
        public static float NearestAxis(float angle,float previous) => previous+Mathf.Repeat(angle-previous+90,180)-90;
        public static bool CrossesGate(Vector2 from,Vector2 to,Vector2 center,float angle,float halfLength,float radius)
        {
            return GateCollision.SweepCircle(from,to,center,angle,angle,halfLength,radius,0,out _);
        }
        public static Vector2 LaunchVelocity(float angle)
        {
            // An axis has no front/back. Fold full rotations into equivalent gate tilts.
            float folded=Mathf.Repeat(angle+90,180)-90;
            return new Vector2(Mathf.Sin(folded*Mathf.Deg2Rad)*430,-100);
        }
        public static int ExitSide(Vector2 position)
        {
            if(position.x<165)return 1;
            if(position.x>845)return 2;
            return position.y>825 ? 3 : 0;
        }
    }
}
