using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Naispo.Overhead
{
    public sealed class OverheadApp : MonoBehaviour
    {
        readonly DepthAnalyzer analyzer=new DepthAnalyzer();
        readonly List<Token> tokens=new List<Token>();
        readonly Color ink=new Color(0.89f,0.94f,0.98f), muted=new Color(0.5f,0.62f,0.72f);
        readonly Color coral=new Color(1,0.39f,0.32f), cyan=new Color(0.18f,0.81f,0.94f);
        // Use one crop for all expressions of each character, with a little outline padding.
        readonly Monster leftMonster=new Monster("Left",new Rect(177,232,901,866));
        readonly Monster rightMonster=new Monster("Right",new Rect(190,134,875,1031));
        OverheadDepthSource source;
        Texture2D dot, preview, strawberry, fish;
        Color32[] previewPixels;
        GUIStyle label, button;
        bool live, table, mirror=true, invert, ready, hasAngle;
        float minHeight=300,maxHeight=2200,angle,targetAngle,spawnTimer,feedbackTime,lastFrameAt=-100;
        int score,misses,streak,spawnCount;
        string feedback="Strawberries left. Fish right.";
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
            public void React(bool correct) { Expression=correct ? 1 : 2;ReactedAt=Time.time; }
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
        }
        void Update()
        {
            float dt=Mathf.Min(Time.deltaTime,0.05f);
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
            if(!live)
            {
                ready=true;
                var kb=Keyboard.current;
                float direction=0;
                if(kb!=null) { if(kb.aKey.isPressed||kb.leftArrowKey.isPressed)direction--; if(kb.dKey.isPressed||kb.rightArrowKey.isPressed)direction++; }
                targetAngle=Mathf.Clamp(targetAngle+direction*70*dt,-65,65);
                if(Mouse.current!=null && Mouse.current.leftButton.isPressed)
                {
                    Vector2 mouse=Mouse.current.position.ReadValue();
                    mouse=new Vector2(mouse.x*1440/Screen.width,(Screen.height-mouse.y)*900/Screen.height);
                    if(mouse.x>40&&mouse.x<970&&mouse.y>140&&mouse.y<830)
                        targetAngle=Mathf.Clamp((mouse.x-PivotX)/5,-65,65);
                }
            }
            angle=Mathf.Lerp(angle,targetAngle,1-Mathf.Exp(-10*dt));
            if(!ready) return;
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
                    if(correct) { score++; streak++; feedback="YUM!  +1"; }
                    else { misses++; streak=0; feedback=exit==3 ? "Dropped. Try a bigger tilt." : "Oops! Try the other monster."; }
                    if(exit==1)leftMonster.React(correct);
                    if(exit==2)rightMonster.React(correct);
                    feedbackTime=Time.time;
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
            feedback="Strawberries left. Fish right.";feedbackTime=Time.time;
        }
        void Connect()
        {
            live=true;ready=false;hasAngle=false;lastFrameAt=-100;
            source.StartCapture();
            analyzer.BeginCalibration(30);
            ResetRound();
        }
        void Calibrate() { analyzer.BeginCalibration(30);ready=false;hasAngle=false;ResetRound(); }
        void Styles()
        {
            if(label!=null)return;
            label=new GUIStyle(GUI.skin.label) { fontSize=18,wordWrap=true,normal={textColor=ink} };
            button=new GUIStyle(GUI.skin.button) { fontSize=16,padding=new RectOffset(12,12,8,8) };
        }
        void OnGUI()
        {
            Styles();
            var old=GUI.matrix;
            GUI.matrix=Matrix4x4.Scale(new Vector3(Screen.width/1440f,Screen.height/900f,1));
            Fill(new Rect(0,0,1440,900),new Color(0.025f,0.04f,0.07f));
            Text(new Rect(40,25,900,28),"NAISPO  /  OVERHEAD LAB",16,cyan);
            Text(new Rect(40,57,800,58),"Turn. Catch. Feed!",40,ink);
            Text(new Rect(1030,48,340,28),live ? "LIVE DEPTH  /  AZURE KINECT" : "DEMO  /  NO SENSOR NEEDED",16,live ? cyan : coral);
            Fill(new Rect(40,140,930,690),new Color(0.045f,0.065f,0.10f));
            // Keep the destinations visible while depth calibration is paused.
            for(int i=0;i<8;i++)Fill(new Rect(90+i*115,165,1,640),new Color(0.07f,0.1f,0.14f));
            Fill(new Rect(56,320,146,490),new Color(0.14f,0.075f,0.09f));
            Fill(new Rect(808,320,146,490),new Color(0.035f,0.12f,0.16f));
            DrawMonster(leftMonster,165,coral);
            DrawMonster(rightMonster,845,cyan);
            DrawFavorite(165,false,coral);
            DrawFavorite(845,true,cyan);
            Text(new Rect(70,165,200,34),"SORTED  "+score,23,ink);
            Text(new Rect(70,203,280,30),$"MISSED  {misses}    STREAK  {streak}",15,muted);
            Text(new Rect(650,165,290,30),ready ? "GATE ACTIVE" : "PAUSED / WAITING FOR AXIS",15,ready ? cyan : coral);
            float displayAngle=Mathf.Repeat(angle+90,180)-90;
            Vector2 axis=new Vector2(Mathf.Cos(displayAngle*Mathf.Deg2Rad),Mathf.Sin(displayAngle*Mathf.Deg2Rad));
            Line(Pivot-axis*HalfArm,Pivot+axis*HalfArm,12,ready ? ink : muted);
            Circle(Pivot,24,new Color(0.11f,0.18f,0.25f));
            Circle(Pivot,7,cyan);
            Circle(Pivot-axis*HalfArm,13,coral);Circle(Pivot+axis*HalfArm,13,cyan);
            foreach(var token in tokens)
            {
                DrawFood(token.Position,token.IsFish,FoodSize,token.Rotation);
            }
            Text(new Rect(280,760,490,38),!ready ? "Clear the view to calibrate, then spread your arms." : Time.time-feedbackTime<2.5f ? feedback : "Strawberries left. Fish right.",17,ink);
            Text(new Rect(40,849,940,30),live ? "The gate follows the axis of your arms. Lost tracking pauses the game." : "A / D or arrow keys to rotate. You can also hold and drag inside the game.",16,muted);
            DrawControlPanel();
            GUI.matrix=old;
        }
        void DrawFavorite(float centerX,bool isFish,Color accent)
        {
            Fill(new Rect(centerX-60,740,120,70),new Color(accent.r,accent.g,accent.b,0.12f));
            DrawFood(new Vector2(centerX,775),isFish,FoodSize);
        }
        void DrawFood(Vector2 center,bool isFish,float size,float rotation=0)
        {
            Texture2D texture=isFish ? fish : strawberry;
            if(texture==null) { Circle(center,size/2,isFish ? cyan : coral);return; }
            Rect crop=isFish ? FishCrop : StrawberryCrop;
            float scale=size/Mathf.Max(crop.width,crop.height);
            var rect=new Rect(center.x-crop.width*scale/2,center.y-crop.height*scale/2,crop.width*scale,crop.height*scale);
            var uv=new Rect(crop.x/texture.width,1-crop.yMax/texture.height,crop.width/texture.width,crop.height/texture.height);
            Color previous=GUI.color;
            Matrix4x4 previousMatrix=GUI.matrix;
            GUI.color=Color.white;
            GUIUtility.RotateAroundPivot(rotation,center);
            GUI.DrawTextureWithTexCoords(rect,texture,uv,true);
            GUI.matrix=previousMatrix;
            GUI.color=previous;
        }
        void DrawMonster(Monster monster,float centerX,Color accent)
        {
            float elapsed=Time.time-monster.ReactedAt;
            int expression=elapsed<1.6f ? monster.Expression : 0;
            Texture2D face=monster.Faces[expression];
            if(face==null)face=monster.Faces[0];
            if(face==null)return;
            float motion=Mathf.Clamp01(1-elapsed/0.6f);
            float bounce=expression==1 ? Mathf.Abs(Mathf.Sin(elapsed*14))*12*motion : 0;
            float wiggle=expression==2 ? Mathf.Sin(elapsed*24)*5*motion : 0;
            Color previous=GUI.color;
            GUI.color=new Color(accent.r,accent.g,accent.b,0.14f);
            GUI.DrawTexture(new Rect(centerX-108,710,216,30),dot);
            GUI.color=Color.white;
            Rect crop=monster.Crop;
            const float width=248;
            float height=width*crop.height/crop.width;
            // Texture UVs start at the bottom; source crop coordinates start at the top.
            var uv=new Rect(crop.x/face.width,1-crop.yMax/face.height,crop.width/face.width,crop.height/face.height);
            GUI.DrawTextureWithTexCoords(new Rect(centerX-width/2+wiggle,730-height-bounce,width,height),face,uv,true);
            GUI.color=previous;
        }
        void DrawControlPanel()
        {
            Fill(new Rect(996,140,404,690),new Color(0.06f,0.085f,0.12f));
            Text(new Rect(1018,155,260,28),"INPUT & CALIBRATION",16,muted);
            Text(new Rect(1300,155,85,28),live ? $"{source.Fps:F0} FPS" : "DEMO",16,cyan);
            if(GUI.Button(new Rect(1018,195,176,38),"Connect Kinect",button))Connect();
            if(GUI.Button(new Rect(1204,195,176,38),"Use demo",button)) { source.StopCapture();live=false;ready=true;targetAngle=0;ResetRound(); }
            var box=new Rect(1018,248,362,252);
            Fill(box,new Color(0.025f,0.04f,0.07f));
            if(live&&preview!=null)
            {
                // Keep image pixels square. Coordinates are shared by image and overlay.
                Rect fitted=Fit(box,preview.width,preview.height);
                GUI.DrawTexture(fitted,preview);
                var r=analyzer.Result;
                if(r.Valid)
                {
                    float x=mirror ? preview.width-1-r.CenterX : r.CenterX;
                    Vector2 p=new Vector2(fitted.x+x/preview.width*fitted.width,fitted.y+r.CenterY/preview.height*fitted.height);
                    float a=r.AngleDegrees*Mathf.Deg2Rad;
                    Vector2 d=new Vector2(Mathf.Cos(a)*(mirror ? -1 : 1),Mathf.Sin(a))*65;
                    Line(p-d,p+d,3,cyan);Circle(p,5,ink);
                }
            }
            else
            {
                Text(new Rect(1050,285,310,30),"OVERHEAD DEPTH PREVIEW",15,muted);
                Vector2 p=new Vector2(1199,396);
                Vector2 d=new Vector2(Mathf.Cos(angle*Mathf.Deg2Rad),Mathf.Sin(angle*Mathf.Deg2Rad));
                Circle(p,28,new Color(0.17f,0.26f,0.34f));Line(p-d*110,p+d*110,14,cyan);
                Text(new Rect(1070,460,290,26),"Illustration / no camera input",13,muted);
            }
            string status=!live ? "Demo ready. Connect when the camera is mounted." : !source.Running ? source.Status : analyzer.CalibrationFramesRemaining>0 ? "CALIBRATING: keep the floor / table empty" : !analyzer.Calibrated ? "No valid background. Clear view and recalibrate." : !ready ? "No clear arm axis. Spread arms and check height band." : $"Axis {angle:F0} deg  |  {source.Fps:F0} FPS  |  confidence {analyzer.Result.Confidence:P0}";
            Text(new Rect(1018,512,362,53),status,15,ink);
            if(GUI.Button(new Rect(1018,574,176,34),table ? "Preset: TABLE" : "Preset: FLOOR",button))
            { table=!table;minHeight=table ? 20 : 300;maxHeight=table ? 250 : 2200;if(live)Calibrate(); }
            GUI.enabled=live&&source.Running;
            if(GUI.Button(new Rect(1204,574,176,34),"Calibrate empty",button))Calibrate();
            GUI.enabled=true;
            Text(new Rect(1018,621,362,25),$"Height above background: {minHeight:F0} - {maxHeight:F0} mm",14,muted);
            minHeight=GUI.HorizontalSlider(new Rect(1018,657,362,18),minHeight,10,1500);
            maxHeight=GUI.HorizontalSlider(new Rect(1018,690,362,18),maxHeight,Mathf.Max(minHeight+10,50),2500);
            mirror=GUI.Toggle(new Rect(1018,723,160,26),mirror,"Mirror depth");
            invert=GUI.Toggle(new Rect(1204,723,176,26),invert,"Invert rotation");
            if(GUI.Button(new Rect(1018,771,362,36),"Reset score",button))ResetRound();
            Text(new Rect(1018,849,380,32),"Prototype 01 / depth axis, not hand recognition",14,muted);
        }
        static Rect Fit(Rect box,int width,int height)
        {
            float scale=Mathf.Min(box.width/width,box.height/height);
            return new Rect(box.center.x-width*scale/2,box.center.y-height*scale/2,width*scale,height*scale);
        }
        void Text(Rect rect,string text,int size,Color color) { label.fontSize=size;label.normal.textColor=color;GUI.Label(rect,text,label); }
        static void Fill(Rect r,Color color) { Color old=GUI.color;GUI.color=color;GUI.DrawTexture(r,Texture2D.whiteTexture);GUI.color=old; }
        void Circle(Vector2 p,float radius,Color color) { Color old=GUI.color;GUI.color=color;GUI.DrawTexture(new Rect(p.x-radius,p.y-radius,radius*2,radius*2),dot);GUI.color=old; }
        static void Line(Vector2 from,Vector2 to,float width,Color color)
        {
            var old=GUI.matrix;Vector2 delta=to-from;
            GUIUtility.RotateAroundPivot(Mathf.Atan2(delta.y,delta.x)*Mathf.Rad2Deg,from);
            Fill(new Rect(from.x,from.y-width/2,delta.magnitude,width),color);GUI.matrix=old;
        }
        void OnDestroy() { if(dot!=null)Destroy(dot);if(preview!=null)Destroy(preview); }
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
