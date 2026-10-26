using UnityEngine;

namespace Naispo.Overhead
{
    public sealed partial class OverheadApp
    {
        static Color Rgb(int value,float alpha=1) => new Color(((value>>16)&255)/255f,((value>>8)&255)/255f,(value&255)/255f,alpha);
        static Color WithAlpha(Color color,float alpha) => new Color(color.r,color.g,color.b,color.a*alpha);
        readonly Color ink=Rgb(0x59433E),muted=Rgb(0x847B67),coral=Rgb(0xEF8875),cyan=Rgb(0x66BABB);
        readonly Color cream=Rgb(0xFFFDF3),outline=Rgb(0xDFD5B7),leaf=Rgb(0x729C74);
        readonly Color nightTop=Rgb(0x102D38),nightBottom=Rgb(0x193F43);
        readonly Color nightWood=Rgb(0x493C35),oldGold=Rgb(0x92764B);
        readonly Color moonlight=Rgb(0xA8D4CE),candle=Rgb(0xE9B875);
        readonly Color nightLeaf=Rgb(0x38685B),nightShadow=Rgb(0x0B2229);
        static readonly Rect BranchCrop=new Rect(56,305,2062,113);
        static readonly Vector2 BranchAnchor=new Vector2(1086,362);
        static readonly float BranchScale=360/BranchCrop.width;
        Texture2D paper,warmPaper,heartIcon,tangleIcon,sparkleIcon,magicStarIcon,glow,background,branch,lantern;
        Font roundFont;
        GUIStyle label,button,sliderThumb;

        void InitializeArt()
        {
            roundFont=Resources.Load<Font>("Fonts/RoundedJapanese");
            if(roundFont==null)Debug.LogError("Missing rounded Japanese font");
            paper=new Texture2D(8,128,TextureFormat.RGBA32,false) { wrapMode=TextureWrapMode.Clamp };
            warmPaper=new Texture2D(8,128,TextureFormat.RGBA32,false) { wrapMode=TextureWrapMode.Clamp };
            for(int y=0;y<128;y++)for(int x=0;x<8;x++)
            {
                paper.SetPixel(x,y,Color.Lerp(nightBottom,nightTop,y/127f));
                // A faint candle wash gathers near the floor and leaves the teal night intact.
                warmPaper.SetPixel(x,y,new Color(candle.r,candle.g,candle.b,Mathf.Lerp(0.16f,0f,y/127f)));
            }
            paper.Apply();
            warmPaper.Apply(false,true);
            // One shared radial falloff keeps every soft light free of a hard rim.
            glow=new Texture2D(64,64,TextureFormat.RGBA32,false)
            {
                name="NAISPO Glow",
                wrapMode=TextureWrapMode.Clamp,
                filterMode=FilterMode.Bilinear,
                hideFlags=HideFlags.DontSave
            };
            for(int y=0;y<64;y++)for(int x=0;x<64;x++)
            {
                float distance=Vector2.Distance(new Vector2(x+0.5f,y+0.5f),new Vector2(32,32))/32f;
                float alpha=Mathf.Clamp01(1-distance);
                alpha=alpha*alpha*(3-2*alpha);
                glow.SetPixel(x,y,new Color(1,1,1,alpha));
            }
            glow.Apply(false,true);
            heartIcon=GameIconArt.CreateHeart();
            tangleIcon=GameIconArt.CreateTangle();
            sparkleIcon=GameIconArt.CreateSparkle();
            magicStarIcon=GameIconArt.CreateMagicStar();
        }
        void Styles()
        {
            if(label!=null)return;
            label=new GUIStyle(GUI.skin.label) { font=roundFont,fontSize=20,wordWrap=true,normal={textColor=ink},padding=new RectOffset(0,0,0,0) };
            button=new GUIStyle(GUIStyle.none) { font=roundFont,fontSize=18,alignment=TextAnchor.MiddleCenter,normal={textColor=ink} };
            sliderThumb=new GUIStyle(GUIStyle.none) { fixedWidth=24,fixedHeight=24 };
        }
        void OnGUI()
        {
            Styles();
            Matrix4x4 original=GUI.matrix;
            Fill(new Rect(0,0,Screen.width,Screen.height),nightShadow);
            GUI.matrix=GameLayout.Matrix(Screen.width,Screen.height);
            DrawLandscape();
            bool modal=settingsOpen||paused;
            GUI.enabled=!modal;
            DrawHeader();
            GUI.enabled=true;
            Matrix4x4 canvas=GUI.matrix;
            GUI.matrix=canvas*Matrix4x4.Translate(new Vector3(GameLayout.StageOffsetX,0,0));
            DrawPlayfield();
            GUI.matrix=canvas;
            if(garden.IsComplete&&!Celebrating)
            {
                GUI.matrix=canvas*Matrix4x4.Translate(new Vector3(GameLayout.StageOffsetX,0,0));
                DrawGardenComplete();
                GUI.matrix=canvas;
            }
            if(settingsOpen)DrawSettings();
            else if(paused)DrawPause();
            GUI.matrix=original;
        }
        void DrawLandscape()
        {
            float warmth=GardenAtmosphere.Warmth(garden,playTime);
            GUI.DrawTexture(new Rect(0,0,1440,900),paper);
            if(background!=null)
                GUI.DrawTexture(Fit(new Rect(0,0,1440,900),background.width,background.height),
                    background,ScaleMode.StretchToFill,true);
            Color originalColor=GUI.color;
            try
            {
                GUI.color=new Color(1,1,1,warmth);
                GUI.DrawTexture(new Rect(0,0,1440,900),warmPaper);
            }
            finally { GUI.color=originalColor; }
            // The room lights sit above the painted room and below the HUD and food.
            DrawLanternRoom();
        }

        void DrawGreenhouseBack(float warmth)
        {
            // A dark floor and a shallow pool of moonlight build depth without lighting
            // the central dropping corridor.
            Oval(new Rect(-160,520,1760,540),new Color(nightShadow.r,nightShadow.g,nightShadow.b,0.55f));
            Oval(new Rect(150,690,1140,320),Rgb(0x0E2A31,0.38f));
            for(int i=0;i<3;i++)
                Capsule(new Vector2(250,762+i*52),new Vector2(1190,762+i*52),2.5f,Rgb(0x2D5355,0.16f));

            // The old gold window sits above the playfield, with an arch from y=120 to y=190.
            Rounded(new Rect(470,120,500,130),26,oldGold);
            Rounded(new Rect(474,124,492,122),24,Rgb(0x0D262E));
            Arc(new Vector2(720,190),232,70,180,360,18,4,Rgb(0x92764B,0.90f));
            Oval(new Rect(500,128,440,110),Rgb(0x2F6A6B,0.38f+warmth*0.06f));
            Oval(new Rect(560,140,320,86),Rgb(0x3C7B78,0.24f+warmth*0.05f));
            Glow(new Rect(540,190,360,330),new Color(moonlight.r,moonlight.g,moonlight.b,0.045f));
            Fill(new Rect(718,126,4,122),Rgb(0x92764B,0.50f));
            Fill(new Rect(478,184,484,3),Rgb(0x92764B,0.42f));
            // A small moon in the clear right pane, away from the drop corridor and cloud.
            Vector2 moon=new Vector2(862,154);
            Glow(new Rect(moon.x-22,moon.y-22,44,44),new Color(moonlight.r,moonlight.g,moonlight.b,0.10f));
            Circle(moon,10,Rgb(0xF6EBCB,0.94f));
        }

        void DrawGreenhouseFrame(float warmth)
        {
            // Uneven wooden beams and a thin antique-gold edge frame the whole stage.
            Capsule(new Vector2(64,150),new Vector2(720,112),16,nightWood);
            Capsule(new Vector2(720,112),new Vector2(1376,150),16,nightWood);
            Capsule(new Vector2(64,150),new Vector2(720,112),2.5f,Rgb(0x92764B,0.55f));
            Capsule(new Vector2(720,112),new Vector2(1376,150),2.5f,Rgb(0x92764B,0.55f));
            Capsule(new Vector2(82,190),new Vector2(718,148),7,Rgb(0x3B3029,0.90f));
            Capsule(new Vector2(722,148),new Vector2(1358,190),7,Rgb(0x3B3029,0.90f));

            DrawPost(78,34,116,900);
            DrawPost(1328,34,116,900);
            Capsule(new Vector2(110,214),new Vector2(240,304),7,nightWood);
            Capsule(new Vector2(110,214),new Vector2(240,304),1.6f,Rgb(0x92764B,0.35f));
            Capsule(new Vector2(1330,214),new Vector2(1200,304),7,nightWood);
            Capsule(new Vector2(1330,214),new Vector2(1200,304),1.6f,Rgb(0x92764B,0.35f));

            DrawVines();
            DrawLantern(206,300,warmth);
            DrawLantern(1234,300,warmth);
        }

        void DrawPost(float x,float width,float top,float bottom)
        {
            Fill(new Rect(x,top,width,bottom-top),nightWood);
            Fill(new Rect(x+4,top,3,bottom-top),Rgb(0x5C4C41,0.50f));
            Fill(new Rect(x+width-8,top,2,bottom-top),Rgb(0x2C2420,0.72f));
            Fill(new Rect(x-4,top,3,bottom-top),Rgb(0x92764B,0.32f));
            Fill(new Rect(x+width+1,top,2,bottom-top),Rgb(0x92764B,0.20f));
            // Only a few uneven grain lines, not a striped texture.
            Fill(new Rect(x+13,top+90,2,260),Rgb(0x3D322C,0.55f));
            Fill(new Rect(x+20,top+430,2,300),Rgb(0x3D322C,0.45f));
        }

        void DrawVines()
        {
            Color vine=Rgb(0x38685B,0.85f);
            Vector2 leftA=new Vector2(100,250),leftB=new Vector2(134,354),
                leftC=new Vector2(104,450),leftD=new Vector2(140,552);
            Capsule(leftA,leftB,3.2f,vine);
            Capsule(leftB,leftC,3.0f,vine);
            Capsule(leftC,leftD,2.6f,vine);
            VineLeaf(Vector2.Lerp(leftA,leftB,0.55f),18,10,-22,vine);
            VineLeaf(Vector2.Lerp(leftB,leftC,0.25f),16,9,196,vine);
            VineLeaf(Vector2.Lerp(leftC,leftD,0.235f),16,9,-18,vine);

            Vector2 rightA=new Vector2(1340,250),rightB=new Vector2(1306,354),
                rightC=new Vector2(1336,450),rightD=new Vector2(1300,552);
            Capsule(rightA,rightB,3.2f,vine);
            Capsule(rightB,rightC,3.0f,vine);
            Capsule(rightC,rightD,2.6f,vine);
            VineLeaf(Vector2.Lerp(rightA,rightB,0.55f),18,10,202,vine);
            VineLeaf(Vector2.Lerp(rightB,rightC,0.25f),16,9,-15,vine);
            VineLeaf(Vector2.Lerp(rightC,rightD,0.235f),16,9,195,vine);

            Vector2 topA=new Vector2(542,146),topB=new Vector2(632,124),topC=new Vector2(712,118);
            Capsule(topA,topB,2.6f,vine);
            Capsule(topB,topC,2.4f,vine);
            VineLeaf(Vector2.Lerp(topA,topB,0.65f),18,9,-60,vine);
            VineLeaf(Vector2.Lerp(topB,topC,0.68f),15,8,-35,vine);
        }

        void VineLeaf(Vector2 attachment,float length,float width,float angle,Color color)
        {
            Matrix4x4 original=GUI.matrix;
            try
            {
                GUI.matrix=original*GameLayout.RotationAround(attachment,angle);
                Oval(new Rect(attachment.x-2,attachment.y-width/2,length+2,width),color);
            }
            finally { GUI.matrix=original; }
        }

        void DrawLantern(float x,float y,float warmth)
        {
            float glow=Mathf.Clamp01(0.08f+warmth*0.17f+FinaleBreath);
            float flicker=1+Mathf.Sin(playTime*2.1f+x)*0.02f;
            float halo=Mathf.Min(0.26f,glow*flicker);
            Color originalColor=GUI.color;
            try
            {
                Glow(new Rect(x-78,y-78,156,156),new Color(candle.r,candle.g,candle.b,halo));
                Capsule(new Vector2(x,112),new Vector2(x,y-32),2,Rgb(0x92764B,0.55f));
                Rounded(new Rect(x-15,y-28,30,16),6,nightWood);
                Rounded(new Rect(x-12,y-14,24,30),7,Rgb(0x3A2C22));
                Circle(new Vector2(x,y),9,new Color(candle.r,candle.g,candle.b,Mathf.Clamp01(0.55f+glow)));
                Circle(new Vector2(x,y),4,Rgb(0xFFF3CC,0.90f));
                Rounded(new Rect(x-15,y-28,30,4),2,Rgb(0x92764B,0.60f));
            }
            finally { GUI.color=originalColor; }
        }

        void DrawGreenhouseForeground()
        {
            Color stem=Rgb(0x38685B,0.90f),darkLeaf=Rgb(0x1E3C36,0.85f);
            Capsule(new Vector2(18,902),new Vector2(94,772),8,stem);
            Capsule(new Vector2(94,772),new Vector2(150,690),5,stem);
            Oval(new Rect(64,758,74,30),darkLeaf);
            Oval(new Rect(112,684,52,24),darkLeaf);
            Oval(new Rect(20,842,58,26),darkLeaf);

            Capsule(new Vector2(1422,902),new Vector2(1346,772),8,stem);
            Capsule(new Vector2(1346,772),new Vector2(1290,690),5,stem);
            Oval(new Rect(1302,758,74,30),darkLeaf);
            Oval(new Rect(1276,684,52,24),darkLeaf);
            Oval(new Rect(1362,842,58,26),darkLeaf);
        }

        void Arc(Vector2 center,float radiusX,float radiusY,float startDegrees,float endDegrees,
            int segments,float width,Color color)
        {
            float previousAngle=startDegrees*Mathf.Deg2Rad;
            Vector2 previous=center+new Vector2(Mathf.Cos(previousAngle)*radiusX,Mathf.Sin(previousAngle)*radiusY);
            for(int i=1;i<=segments;i++)
            {
                float angle=Mathf.Lerp(startDegrees,endDegrees,i/(float)segments)*Mathf.Deg2Rad;
                Vector2 point=center+new Vector2(Mathf.Cos(angle)*radiusX,Mathf.Sin(angle)*radiusY);
                Capsule(previous,point,width,color);
                previous=point;
            }
        }

        void DrawHeader()
        {
            Text(new Rect(64,53,180,35),"NAISPO",22,Rgb(0xBFD8CF));
            Card(new Rect(960,46,196,64),cream,22);
            float celebrationPulse=Celebrating ? Mathf.Sin((playTime-celebrationAt)*6)*3 : 0;
            Sparkle(new Vector2(997,78),18+celebrationPulse,Rgb(0xE6B450));
            Text(new Rect(1027,50,119,56),$"{score} / {GardenProgress.Goal}",28,ink,TextAnchor.MiddleLeft);
            if(SoftButton(new Rect(1180,46,116,64),"ひとやすみ",cream)) { paused=true;draggingGate=false; }
            if(SoftButton(new Rect(1308,46,91,64),"せってい",cream)) { settingsOpen=true;draggingGate=false; }
        }
        void DrawPlayfield()
        {
            DrawDropWarning();
            DrawMonster(leftMonster,165,coral,false);
            DrawMonster(rightMonster,845,cyan,true);
            DrawFavorite(165,false);
            DrawFavorite(845,true);
            DrawStickGlow();
            if(garden.IsComplete)return;
            float displayAngle=Mathf.Repeat(angle+90,180)-90;
            if(branch==null)
            {
                Vector2 axis=new Vector2(Mathf.Cos(displayAngle*Mathf.Deg2Rad),Mathf.Sin(displayAngle*Mathf.Deg2Rad));
                Capsule(Pivot-axis*HalfArm,Pivot+axis*HalfArm,20,Rgb(0x493C35));
            }
            else
            {
                Color oldColor=GUI.color;
                Matrix4x4 oldMatrix=GUI.matrix;
                try
                {
                    GUI.color=Color.white;
                    GUI.matrix=oldMatrix*BranchTransform(Pivot,displayAngle);
                    GUI.DrawTextureWithTexCoords(BranchRect(Pivot),branch,
                        CropUv(BranchCrop,branch.width,branch.height),true);
                }
                finally { GUI.matrix=oldMatrix;GUI.color=oldColor; }
            }
            foreach(var token in tokens)DrawAnimatedFood(token);
            DrawFoodFeedback();
            if(!ready&&!Celebrating)
            {
                string prompt=source.Starting||(!front&&analyzer.CalibrationFramesRemaining>0)
                    ? "じゅんび しているよ…" : front ? FrontPrompt() : "りょうてを ひろげてみよう";
                Rounded(new Rect(315,353,380,67),24,cream);
                Text(new Rect(330,364,350,43),prompt,21,muted,TextAnchor.MiddleCenter);
            }
        }
        void DrawStickGlow()
        {
            // Show only the strongest recent contact so overlapping hits never add up to white.
            float strongest=0;
            Vector2 position=default;
            foreach(var impact in impacts)
            {
                float elapsed=playTime-impact.StartedAt;
                if(elapsed<0||elapsed>0.30f)continue;
                float strength=1-elapsed/0.30f;
                if(strength<=strongest)continue;
                strongest=strength;
                position=impact.Position;
            }
            if(strongest<=0)return;
            float radius=24+12*(1-strongest);
            Color originalColor=GUI.color;
            try
            {
                Glow(new Rect(position.x-radius,position.y-radius,radius*2,radius*2),
                    new Color(moonlight.r,moonlight.g,moonlight.b,0.30f*strongest));
            }
            finally { GUI.color=originalColor; }
        }

        void DrawGardenComplete()
        {
            Card(new Rect(PivotX-205,348,410,188),cream,30);
            Text(new Rect(PivotX-185,372,370,48),"あかりが ともった！",30,ink,TextAnchor.MiddleCenter);
            bool enabled=GUI.enabled;
            GUI.enabled=enabled&&!settingsOpen&&!paused;
            if(SoftButton(new Rect(PivotX-114,448,228,58),"もういちど",Rgb(0xD6E7CD)))ResetRound();
            GUI.enabled=enabled;
        }
        void DrawFavorite(float centerX,bool isFish)
        {
            Oval(new Rect(centerX-49,755,98,59),cream);
            DrawFood(new Vector2(centerX,783),isFish,49);
        }
        void DrawFood(Vector2 center,bool isFish,float size,float rotation=0)
        {
            Texture2D texture=isFish ? fish : strawberry;
            if(texture==null) { Circle(center,size/2,isFish ? cyan : coral);return; }
            Rect crop=isFish ? FishCrop : StrawberryCrop;
            float scale=size/Mathf.Max(crop.width,crop.height);
            Rect rect=new Rect(center.x-crop.width*scale/2,center.y-crop.height*scale/2,crop.width*scale,crop.height*scale);
            Rect uv=CropUv(crop,texture.width,texture.height);
            Color old=GUI.color;Matrix4x4 matrix=GUI.matrix;GUI.color=Color.white;
            GUI.matrix=GUI.matrix*GameLayout.RotationAround(center,rotation);
            GUI.DrawTextureWithTexCoords(rect,texture,uv,true);
            GUI.matrix=matrix;GUI.color=old;
        }
        string ControlHint() => live
            ? front ? "りょうてを はなして、\nてを じょうげに うごかそう" : "りょうてを ひろげて、\nからだを ゆっくり まわそう"
            : "マウスを おしたまま、ひだり・みぎへ\nA / D ・ ← / → でも あそべるよ";
        void DrawPause()
        {
            Fill(new Rect(0,0,1440,900),Rgb(0x4C5145,0.34f));
            Card(new Rect(470,290,500,310),cream,36);
            Text(new Rect(500,325,440,64),"ちょっと ひとやすみ",31,ink,TextAnchor.MiddleCenter);
            Text(new Rect(500,397,440,68),ControlHint(),19,muted,TextAnchor.MiddleCenter);
            if(SoftButton(new Rect(577,481,286,61),"つづける",Rgb(0xE8BE6C)))paused=false;
        }
        void DrawSettings()
        {
            Fill(new Rect(0,0,1440,900),Rgb(0x4C5145,0.34f));
            Card(new Rect(260,88,920,740),cream,32);
            Text(new Rect(298,113,610,52),"あそびの せってい",30,ink);
            if(SoftButton(new Rect(992,113,126,48),sounds.Muted ? "おと：オフ" : "おと：オン",Rgb(0xF1E7D1)))sounds.Muted=!sounds.Muted;
            if(SoftButton(new Rect(300,213,182,45),"マウス・キー",!live ? Rgb(0xD6E7CD) : Rgb(0xF3EAD7)))
            { source.StopCapture();live=false;ready=true;targetAngle=0;ResetRound(); }
            if(SoftButton(new Rect(495,213,182,45),"Kinect をつなぐ",live ? Rgb(0xD6E7CD) : Rgb(0xF3EAD7)))Connect();
            Rect box=new Rect(300,292,377,246);
            Rounded(box,20,Rgb(0xE5E9DC));
            if(live&&preview!=null)
            {
                Rect fitted=Fit(new Rect(312,304,353,222),preview.width,preview.height);
                GUI.DrawTexture(fitted,preview);
                var r=analyzer.Result;
                if(front&&handLine.Valid&&ready)
                {
                    Vector2 left=PreviewPoint(frontPose.LeftPixel,fitted);
                    Vector2 right=PreviewPoint(frontPose.RightPixel,fitted);
                    Capsule(left,right,4,cream);
                    Circle(left,9,coral);Circle(right,9,cyan);
                }
                else if(!front&&r.Valid)
                {
                    float x=previewMirrored ? preview.width-1-r.CenterX : r.CenterX;
                    Vector2 p=new Vector2(fitted.x+x/preview.width*fitted.width,fitted.y+r.CenterY/preview.height*fitted.height);
                    float a=r.AngleDegrees*Mathf.Deg2Rad;
                    Vector2 d=new Vector2(Mathf.Cos(a)*(previewMirrored ? -1 : 1),Mathf.Sin(a))*65;
                    Capsule(p-d,p+d,3,cyan);Circle(p,5,cream);
                }
            }
            else
            {
                Circle(new Vector2(488,374),22,leaf);
                Capsule(new Vector2(488,401),new Vector2(488,458),12,leaf);
                Capsule(new Vector2(405,416),new Vector2(572,416),12,leaf);
            }
            string status=!live ? front ? "正面に立って、両手を 20 cm 以上\n離してから Kinect をつないでね。" : "人や手が映らないようにしてから\nKinect をつないでね。" : source.Starting ? "Kinect をじゅんびしています…" : !source.Running ? "Kinect をつなぎ直してください。" : front ? FrontStatus() : analyzer.CalibrationFramesRemaining>0 ? "背景をおぼえています。\nカメラの前から はなれて まってね。" : !analyzer.Calibrated ? "背景を読み取れませんでした。\nまわりを空けて、とりなおしてください。" : !ready ? "手をひろげてみよう。\n映らないときは、高さを調整してください。" : "からだの向きが みえています。";
            Text(new Rect(302,559,372,103),status,18,ink);
            if(live&&!source.Running)Text(new Rect(302,661,372,58),source.Status,12,muted);
            Text(new Rect(724,171,398,35),"カメラの むき",21,ink);
            if(SoftButton(new Rect(724,213,190,45),"みおろし",!front ? Rgb(0xD6E7CD) : Rgb(0xF3EAD7)))SetCameraMode(false);
            if(SoftButton(new Rect(928,213,190,45),"しょうめん",front ? Rgb(0xD6E7CD) : Rgb(0xF3EAD7)))SetCameraMode(true);
            if(front)
            {
                Text(new Rect(724,342,394,31),"カメラから手までの距離",18,ink);
                minDistance=SoftSlider(new Rect(724,407,394,22),minDistance,500,4000);
                Text(new Rect(724,378,394,26),$"ちかいところ  {minDistance/1000:F2} m",16,muted);
                maxDistance=Mathf.Max(maxDistance,minDistance+100);
                maxDistance=SoftSlider(new Rect(724,476,394,22),maxDistance,Mathf.Max(minDistance+100,1000),5000);
                Text(new Rect(724,447,394,26),$"とおいところ  {maxDistance/1000:F2} m",16,muted);
            }
            else
            {
                if(SoftButton(new Rect(724,275,190,43),"ゆか・からだ",!table ? Rgb(0xD6E7CD) : Rgb(0xF3EAD7)))SetPreset(false);
                if(SoftButton(new Rect(928,275,190,43),"つくえ・て",table ? Rgb(0xD6E7CD) : Rgb(0xF3EAD7)))SetPreset(true);
                Text(new Rect(724,342,394,31),"背景からの高さ",18,ink);
                minHeight=SoftSlider(new Rect(724,407,394,22),minHeight,10,1500);
                Text(new Rect(724,378,394,26),$"ひくいところ  {minHeight:F0} mm",16,muted);
                maxHeight=Mathf.Max(maxHeight,minHeight+10);
                maxHeight=SoftSlider(new Rect(724,476,394,22),maxHeight,Mathf.Max(minHeight+10,50),2500);
                Text(new Rect(724,447,394,26),$"たかいところ  {maxHeight:F0} mm",16,muted);
            }
            GUI.enabled=live&&source.Running&&!source.Starting;
            if(SoftButton(new Rect(724,525,394,50),front ? "あそぶ人を えらびなおす" : "背景を とりなおす",Rgb(0xE8BE6C)))
            { if(front)Connect();else Calibrate(); }
            GUI.enabled=true;
            if(!front&&live&&source.Running)
                Text(new Rect(724,588,394,50),"人や手を映さず、床や机だけにしてから\n「背景を とりなおす」を押してください。",15,muted);
            if(SoftButton(new Rect(724,652,190,43),mirror ? "左右反転：オン" : "左右反転：オフ",Rgb(0xF3EAD7)))mirror=!mirror;
            if(SoftButton(new Rect(928,652,190,43),invert ? "回転反転：オン" : "回転反転：オフ",Rgb(0xF3EAD7)))invert=!invert;
            if(SoftButton(new Rect(300,744,206,48),"はじめから",Rgb(0xF3EAD7)))ResetRound();
            if(SoftButton(new Rect(865,738,253,59),"あそびに もどる",Rgb(0xD6E7CD))) { settingsOpen=false;paused=false;draggingGate=false; }
        }
        Vector2 PreviewPoint(Vector2 point,Rect fitted)
        {
            float x=previewMirrored ? preview.width-1-point.x : point.x;
            return new Vector2(fitted.x+x/preview.width*fitted.width,fitted.y+point.y/preview.height*fitted.height);
        }
        string FrontPrompt()
        {
            if(handLine.Problem==FrontPoseProblem.HandsTooClose)return "りょうてを すこし はなしてね";
            if(handLine.Problem==FrontPoseProblem.OutOfRange)return "せっていの きょりを たしかめよう";
            return "りょうてが みえるように してね";
        }
        string FrontStatus()
        {
            if(ready)return $"両手が みえています。\n手まで {handLine.DistanceMm/1000:F2} m";
            if(Time.unscaledTime-lastFrameAt>0.5f)return "新しい骨格データを待っています。\n両手が見えるように立ってください。";
            if(handLine.Problem==FrontPoseProblem.OutOfRange)return "手が設定した距離の範囲外です。\n右の距離を調整するか、立つ位置を変えてね。";
            if(handLine.Problem==FrontPoseProblem.HandsTooClose)return "両手を左右に 20 cm 以上離してね。\nかさなると、ゲームはひとやすみします。";
            if(handLine.Problem==FrontPoseProblem.LowConfidence)return "両手がはっきり見えるまで ひとやすみ。\n手を体の横に出してみよう。";
            return "正面に立って、両手を左右にひろげてね。\n交代したときは「えらびなおす」を押してね。";
        }
        void SetPreset(bool onTable)
        {
            if(table==onTable)return;
            table=onTable;minHeight=table ? 20 : 300;maxHeight=table ? 250 : 2200;
            if(live)Calibrate();
        }
        float SoftSlider(Rect rect,float value,float minimum,float maximum)
        {
            // Retain IMGUI's keyboard/focus handling, but draw a larger friendly thumb.
            float result=GUI.HorizontalSlider(rect,value,minimum,maximum,GUIStyle.none,sliderThumb);
            Rounded(new Rect(rect.x,rect.y+8,rect.width,7),3,outline);
            float t=Mathf.InverseLerp(minimum,maximum,result);
            float thumbX=rect.x+12+(rect.width-24)*t;
            Rounded(new Rect(rect.x,rect.y+8,thumbX-rect.x,7),3,leaf);
            Circle(new Vector2(thumbX,rect.y+11),12,leaf);
            Circle(new Vector2(thumbX,rect.y+11),6,cream);
            return result;
        }
        bool SoftButton(Rect rect,string text,Color fill)
        {
            bool hover=GUI.enabled&&rect.Contains(Event.current.mousePosition);
            Color color=GUI.enabled ? fill : Color.Lerp(fill,cream,0.65f);
            Rounded(new Rect(rect.x,rect.y+4,rect.width,rect.height),18,Rgb(0xD4C6A7));
            Rounded(rect,18,hover ? Color.Lerp(color,Color.white,0.22f) : color);
            return GUI.Button(rect,text,button);
        }
        void Card(Rect rect,Color color,float radius)
        {
            Rounded(new Rect(rect.x,rect.y+5,rect.width,rect.height),radius,Rgb(0xDCD1B6));
            Rounded(rect,radius,outline);
            Rounded(new Rect(rect.x+2,rect.y+2,rect.width-4,rect.height-4),radius-2,color);
        }
        void Rounded(Rect rect,float radius,Color color)
        {
            radius=Mathf.Min(radius,Mathf.Min(rect.width,rect.height)/2);
            Fill(new Rect(rect.x+radius,rect.y,rect.width-radius*2,rect.height),color);
            Fill(new Rect(rect.x,rect.y+radius,radius,rect.height-radius*2),color);
            Fill(new Rect(rect.xMax-radius,rect.y+radius,radius,rect.height-radius*2),color);
            Color old=GUI.color;GUI.color=color;
            GUI.DrawTextureWithTexCoords(new Rect(rect.x,rect.y,radius,radius),dot,new Rect(0,0.5f,0.5f,0.5f));
            GUI.DrawTextureWithTexCoords(new Rect(rect.xMax-radius,rect.y,radius,radius),dot,new Rect(0.5f,0.5f,0.5f,0.5f));
            GUI.DrawTextureWithTexCoords(new Rect(rect.x,rect.yMax-radius,radius,radius),dot,new Rect(0,0,0.5f,0.5f));
            GUI.DrawTextureWithTexCoords(new Rect(rect.xMax-radius,rect.yMax-radius,radius,radius),dot,new Rect(0.5f,0,0.5f,0.5f));
            GUI.color=old;
        }
        // A shared morphing stroke keeps the lantern's mouth from splitting into parts.
        void FaceStroke(Vector2 center,float halfWidth,float bend,float tilt,float width,Color color)
        {
            Vector2 previous=center+new Vector2(-halfWidth,-tilt);
            for(int i=1;i<=12;i++)
            {
                float t=i/6f-1;
                Vector2 point=center+new Vector2(t*halfWidth,bend*(1-t*t)+tilt*t);
                Capsule(previous,point,width,color);
                previous=point;
            }
        }
        void Sparkle(Vector2 p,float radius,Color color)
        {
            DrawIcon(sparkleIcon,p,Vector2.one*radius*2,color);
        }
        static void DrawIcon(Texture2D texture,Vector2 center,Vector2 size,Color color)
        {
            if(texture==null||size.x<=0||size.y<=0||color.a<=0)return;
            Color previous=GUI.color;
            try
            {
                GUI.color=color;
                GUI.DrawTexture(new Rect(center-size/2,size),texture,ScaleMode.StretchToFill,true);
            }
            finally { GUI.color=previous; }
        }
        void Text(Rect rect,string text,int size,Color color,TextAnchor alignment=TextAnchor.UpperLeft)
        { label.fontSize=size;label.normal.textColor=color;label.alignment=alignment;GUI.Label(rect,text,label); }
        static void Fill(Rect rect,Color color)
        { Color old=GUI.color;GUI.color=color;GUI.DrawTexture(rect,Texture2D.whiteTexture);GUI.color=old; }
        void Oval(Rect rect,Color color)
        { Color old=GUI.color;GUI.color=color;GUI.DrawTexture(rect,dot);GUI.color=old; }
        void Glow(Rect rect,Color color)
        {
            if(glow==null||rect.width<=0||rect.height<=0||color.a<=0)return;
            Color old=GUI.color;
            try
            {
                GUI.color=color;
                GUI.DrawTexture(rect,glow,ScaleMode.StretchToFill,true);
            }
            finally { GUI.color=old; }
        }
        void Circle(Vector2 p,float radius,Color color) => Oval(new Rect(p.x-radius,p.y-radius,radius*2,radius*2),color);
        void Capsule(Vector2 from,Vector2 to,float width,Color color)
        {
            Matrix4x4 old=GUI.matrix;Vector2 delta=to-from;
            GUI.matrix=GUI.matrix*GameLayout.RotationAround(from,Mathf.Atan2(delta.y,delta.x)*Mathf.Rad2Deg);
            Fill(new Rect(from.x,from.y-width/2,delta.magnitude,width),color);
            GUI.matrix=old;Circle(from,width/2,color);Circle(to,width/2,color);
        }
        static Rect CropUv(Rect crop,float sourceWidth,float sourceHeight)
        {
            return new Rect(crop.x/sourceWidth,1-crop.yMax/sourceHeight,
                crop.width/sourceWidth,crop.height/sourceHeight);
        }
        static Rect BranchRect(Vector2 pivot)
        {
            return new Rect(pivot.x+(BranchCrop.x-BranchAnchor.x)*BranchScale,
                pivot.y+(BranchCrop.y-BranchAnchor.y)*BranchScale,
                360,BranchCrop.height*BranchScale);
        }
        static Vector2 BranchAnchorPoint(Rect rect)
        {
            return new Vector2(rect.x+(BranchAnchor.x-BranchCrop.x)*BranchScale,
                rect.y+(BranchAnchor.y-BranchCrop.y)*BranchScale);
        }
        static Matrix4x4 BranchTransform(Vector2 pivot,float angle) =>
            GameLayout.RotationAround(pivot,angle);
        static Rect Fit(Rect box,int width,int height)
        {
            float scale=Mathf.Min(box.width/width,box.height/height);
            return new Rect(box.center.x-width*scale/2,box.center.y-height*scale/2,width*scale,height*scale);
        }
    }
}
