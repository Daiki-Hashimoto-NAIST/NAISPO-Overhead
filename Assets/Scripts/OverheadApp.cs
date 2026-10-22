using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Naispo.Overhead
{
    public sealed partial class OverheadApp : MonoBehaviour
    {
        readonly DepthAnalyzer analyzer=new DepthAnalyzer();
        readonly List<Token> tokens=new List<Token>();
        // Use one crop for all expressions of each character, with a little outline padding.
        readonly Monster leftMonster=new Monster("Left",new Rect(177,232,901,866));
        readonly Monster rightMonster=new Monster("Right",new Rect(190,134,875,1031));
        OverheadDepthSource source;
        Texture2D dot, preview, strawberry, fish;
        Color32[] previewPixels;
        bool live, table, mirror=true, invert, ready, hasAngle, paused, settingsOpen, draggingGate;
        float minHeight=300,maxHeight=2200,angle,targetAngle,spawnTimer,feedbackTime,lastFrameAt=-100,playTime;
        int score,misses,streak,spawnCount;
        string feedback="すきな ごはんを とどけよう！";
        const float PivotX=505,PivotY=530,HalfArm=170,FoodSize=56;
        static readonly Vector2 Pivot=new Vector2(PivotX,PivotY);
        static readonly Rect StrawberryCrop=new Rect(209,105,835,1051),FishCrop=new Rect(104,310,1062,646);
        class Token
        {
            public Vector2 Position,Previous,Velocity;
            public bool IsFish;
            public float Cooldown,Rotation,SpinSpeed;
        }
        sealed class Monster
        {
            public readonly string Side;
            public readonly Rect Crop;
            public readonly Texture2D[] Faces=new Texture2D[3];
            public int Expression;
            public float ReactedAt=-100;
            public Monster(string side,Rect crop) { Side=side;Crop=crop; }
            public void Load()
            {
                string[] names={"Normal","Satisfied","Confused"};
                for(int i=0;i<names.Length;i++)
                {
                    Faces[i]=Resources.Load<Texture2D>("Monsters/"+Side+names[i]);
                    if(Faces[i]==null) Debug.LogError("Missing monster texture: "+Side+names[i]);
                }
            }
            public void React(bool correct,float time) { Expression=correct ? 1 : 2;ReactedAt=time; }
            public void Reset() { Expression=0;ReactedAt=-100; }
        }

        void Awake()
        {
            Application.targetFrameRate=60;
            source=GetComponent<OverheadDepthSource>();
            leftMonster.Load();rightMonster.Load();
            strawberry=Resources.Load<Texture2D>("Food/Strawberry");
            fish=Resources.Load<Texture2D>("Food/Fish");
            if(strawberry==null||fish==null)Debug.LogError("Missing food texture: Strawberry or Fish");
            dot=new Texture2D(64,64,TextureFormat.RGBA32,false);
            for(int y=0;y<64;y++)for(int x=0;x<64;x++)
            {
                float alpha=Mathf.Clamp01(31-Vector2.Distance(new Vector2(x+0.5f,y+0.5f),new Vector2(32,32)));
                dot.SetPixel(x,y,new Color(1,1,1,alpha));
            }
            dot.Apply();
            InitializeArt();
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
            if(live && source.TryTake(out var depth,out int w,out int h))
            {
                if(lastFrameAt<0) Debug.Log($"[Overhead] Live depth ready: {w} x {h}");
                lastFrameAt=Time.unscaledTime;
                analyzer.Process(depth,w,h,minHeight,maxHeight);
                UpdatePreview(depth,w,h);
                ready=analyzer.Calibrated && analyzer.Result.Valid;
                if(ready)
                {
                    float raw=analyzer.Result.AngleDegrees*(mirror ? -1 : 1)*(invert ? -1 : 1);
                    targetAngle=SorterRules.NearestAxis(raw,hasAngle ? targetAngle : 0);
                    hasAngle=true;
                }
            }
            if(live && (Time.unscaledTime-lastFrameAt>0.5f || !source.Running)) ready=false;
            if(!live)ready=true;
            // Depth capture and calibration continue while the game is paused.
            if(settingsOpen||paused) { draggingGate=false;return; }
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
            angle=Mathf.Lerp(angle,targetAngle,1-Mathf.Exp(-10*dt));
            if(!ready) return;
            playTime+=dt;
            spawnTimer-=dt;
            if(spawnTimer<=0 && tokens.Count<7)
            {
                spawnTimer=1.8f;
                bool isFish=spawnCount++%2==1;
                tokens.Add(new Token {
                    Position=new Vector2(PivotX+Random.Range(-75f,75f),185),Velocity=new Vector2(0,85),IsFish=isFish,
                    Rotation=Random.Range(-35f,35f),SpinSpeed=Random.Range(50f,100f)*(Random.value<0.5f ? -1 : 1)
                });
            }
            for(int i=tokens.Count-1;i>=0;i--)
            {
                var token=tokens[i];
                token.Previous=token.Position;
                token.Velocity.y+=170*dt;
                token.Position+=token.Velocity*dt;
                token.Rotation=Mathf.Repeat(token.Rotation+token.SpinSpeed*dt,360);
                token.SpinSpeed*=Mathf.Exp(-0.18f*dt);
                token.Cooldown-=dt;
                // Keep a stable circular contact area while the illustration turns.
                float radius=FoodSize/2;
                if(token.Cooldown<=0 && SorterRules.CrossesGate(token.Previous,token.Position,Pivot,angle,HalfArm,radius))
                {
                    float rad=(Mathf.Repeat(angle+90,180)-90)*Mathf.Deg2Rad;
                    Vector2 normal=new Vector2(Mathf.Sin(rad),-Mathf.Cos(rad));
                    token.Position=token.Previous+normal*4;
                    token.Velocity=SorterRules.LaunchVelocity(angle);
                    float spinDirection=Mathf.Abs(token.Velocity.x)>1 ? Mathf.Sign(token.Velocity.x) : Mathf.Sign(token.SpinSpeed);
                    token.SpinSpeed=spinDirection*(160+Mathf.Abs(token.Velocity.x)*0.45f);
                    token.Cooldown=0.35f;
                }
                int exit=SorterRules.ExitSide(token.Position);
                if(exit!=0)
                {
                    bool correct=(exit==1&&!token.IsFish)||(exit==2&&token.IsFish);
                    if(correct) { score++; streak++; feedback=streak>=3 ? $"{streak}れんぞく！ すごい！" : "もぐもぐ！ おいしい！"; }
                    else { misses++; streak=0; feedback=exit==3 ? "もうすこし かたむけてみよう" : "すきな ごはんは あしもとを みてね"; }
                    if(exit==1)leftMonster.React(correct,playTime);
                    if(exit==2)rightMonster.React(correct,playTime);
                    feedbackTime=playTime;
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
                if(analyzer.Mask[index]!=0)c=new Color(0.98f,0.70f,0.29f);
                previewPixels[(h-1-y)*w+(mirror ? w-1-x : x)]=c;
            }
            preview.SetPixels32(previewPixels);preview.Apply(false,false);
        }
        void ResetRound()
        {
            tokens.Clear();score=misses=streak=spawnCount=0;spawnTimer=1;
            leftMonster.Reset();rightMonster.Reset();
            feedback="すきな ごはんを とどけよう！";feedbackTime=playTime;
        }
        void Connect()
        {
            live=true;ready=false;hasAngle=false;lastFrameAt=-100;
            source.StartCapture();
            analyzer.BeginCalibration(30);
            ResetRound();
        }
        void Calibrate() { analyzer.BeginCalibration(30);ready=false;hasAngle=false;ResetRound(); }
        void OnDestroy() { if(dot!=null)Destroy(dot);if(preview!=null)Destroy(preview);if(paper!=null)Destroy(paper); }
    }

    public static class SorterRules
    {
        public static float NearestAxis(float angle,float previous) => previous+Mathf.Repeat(angle-previous+90,180)-90;
        public static bool CrossesGate(Vector2 from,Vector2 to,Vector2 center,float angle,float halfLength,float radius)
        {
            float r=(Mathf.Repeat(angle+90,180)-90)*Mathf.Deg2Rad;
            Vector2 tangent=new Vector2(Mathf.Cos(r),Mathf.Sin(r)),normal=new Vector2(Mathf.Sin(r),-Mathf.Cos(r));
            float a=Vector2.Dot(from-center,normal)-radius,b=Vector2.Dot(to-center,normal)-radius;
            if(a<0||b>0||a<=b)return false;
            Vector2 contact=Vector2.Lerp(from,to,a/(a-b));
            return Mathf.Abs(Vector2.Dot(contact-center,tangent))<=halfLength+radius;
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
