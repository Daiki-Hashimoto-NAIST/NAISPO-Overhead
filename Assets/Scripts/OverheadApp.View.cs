using UnityEngine;

namespace Naispo.Overhead
{
    public sealed partial class OverheadApp
    {
        static Color Rgb(int value,float alpha=1) => new Color(((value>>16)&255)/255f,((value>>8)&255)/255f,(value&255)/255f,alpha);
        readonly Color ink=Rgb(0x59433E),muted=Rgb(0x847B67),coral=Rgb(0xEF8875),cyan=Rgb(0x66BABB);
        readonly Color cream=Rgb(0xFFFDF3),outline=Rgb(0xDFD5B7),leaf=Rgb(0x729C74);
        Texture2D paper,warmPaper,heartIcon,tangleIcon,sparkleIcon;
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
                paper.SetPixel(x,y,Color.Lerp(Rgb(0xEAF3DC),Rgb(0xFFF5DE),y/127f));
                warmPaper.SetPixel(x,y,Color.Lerp(Rgb(0xF1EFCF),Rgb(0xFFF0CB),y/127f));
            }
            paper.Apply();
            warmPaper.Apply(false,true);
            heartIcon=GameIconArt.CreateHeart();
            tangleIcon=GameIconArt.CreateTangle();
            sparkleIcon=GameIconArt.CreateSparkle();
            InitializeGardenArt();
            InitializeMeadowArt();
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
            Fill(new Rect(0,0,Screen.width,Screen.height),Rgb(0xE9E7CF));
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
            DrawGardenForeground();
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
            Color originalColor=GUI.color;
            GUI.color=new Color(1,1,1,warmth);
            GUI.DrawTexture(new Rect(0,0,1440,900),warmPaper);
            GUI.color=originalColor;
            Oval(new Rect(-190,490,630,470),Color.Lerp(Rgb(0xD8E8C5),Rgb(0xE0EBC5),warmth));
            Oval(new Rect(1070,510,630,430),Color.Lerp(Rgb(0xCDE4D1),Rgb(0xD8E8CF),warmth));
            Oval(new Rect(-210,650,730,310),Color.Lerp(Rgb(0xC5DCA9),Rgb(0xD3E2AC),warmth));
            Oval(new Rect(1010,692,650,280),Color.Lerp(Rgb(0xB9D6BC),Rgb(0xC9DDBA),warmth));
            DrawMeadowColors();
            Oval(new Rect(170,660,1100,330),Color.Lerp(Rgb(0xF5E8C7),Rgb(0xFFF0CC),warmth));
            Cloud(124,263,0.7f,Rgb(0xFFFCF0));
            Cloud(1310,230,0.85f,Rgb(0xFFFCF0));
            Cloud(1200,373,0.45f,Rgb(0xF8FAEB));
            // Every stem belongs to a complete flower, with leaves joined below its head.
            Flower(92,742,0.8f,Rgb(0xE9AF94));Flower(1340,730,0.66f,Rgb(0xEFCA73));
            Flower(180,797,0.45f,Rgb(0xE6B9BC));Flower(1246,810,0.78f,Rgb(0xFFF7DD));
            DrawGardenFlowers();
        }
        void DrawHeader()
        {
            Text(new Rect(64,53,180,35),"NAISPO",22,Rgb(0x9A967D));
            Card(new Rect(960,46,196,64),cream,22);
            float celebrationPulse=Celebrating ? Mathf.Sin((playTime-celebrationAt)*6)*3 : 0;
            Sparkle(new Vector2(997,78),18+celebrationPulse,Rgb(0xE6B450));
            Text(new Rect(1027,50,119,56),$"{score} / {GardenProgress.Goal}",28,ink,TextAnchor.MiddleLeft);
            if(SoftButton(new Rect(1180,46,116,64),"ひとやすみ",cream)) { paused=true;draggingGate=false; }
            if(SoftButton(new Rect(1308,46,91,64),"せってい",cream)) { settingsOpen=true;draggingGate=false; }
        }
        void DrawPlayfield()
        {
            // Stretch the cloud to cover all three food drop regions.
            Cloud(PivotX-45,209,1.08f,cream);
            Cloud(PivotX+45,209,1.08f,cream);
            DrawCloudFace();
            DrawDropWarning();
            Oval(new Rect(20,704,290,70),Rgb(0xECCAB2));
            Oval(new Rect(700,704,290,70),Rgb(0xBFDAD0));
            DrawMonster(leftMonster,165,coral,false);
            DrawMonster(rightMonster,845,cyan,true);
            DrawFavorite(165,false);
            DrawFavorite(845,true);
            if(garden.IsComplete)return;
            float displayAngle=Mathf.Repeat(angle+90,180)-90;
            Vector2 axis=new Vector2(Mathf.Cos(displayAngle*Mathf.Deg2Rad),Mathf.Sin(displayAngle*Mathf.Deg2Rad));
            // Rounded wooden paddle, with strawberry and fish colored ends.
            Vector2 from=Pivot-axis*HalfArm,to=Pivot+axis*HalfArm;
            Capsule(from+Vector2.up*5,to+Vector2.up*5,20,Rgb(0xC9B38D));
            Capsule(from,to,20,ready ? Rgb(0xAA7755) : Rgb(0xB8AC91));
            Capsule(from+new Vector2(0,-3),to+new Vector2(0,-3),10,Rgb(0xD9A979));
            Circle(from,18,Rgb(0xFFF6E5));Circle(to,18,Rgb(0xFFF6E5));
            Circle(from,13,coral);Circle(to,13,cyan);
            Circle(Pivot+new Vector2(0,4),28,Rgb(0xBA9C76));
            Circle(Pivot,26,cream);Circle(Pivot,17,Rgb(0xE9B95F));
            Circle(Pivot,6,Rgb(0xFFF1C5));
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
        void DrawGardenComplete()
        {
            Card(new Rect(PivotX-205,348,410,188),cream,30);
            Text(new Rect(PivotX-185,372,370,48),"おにわが できた！",30,ink,TextAnchor.MiddleCenter);
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
            Rect uv=new Rect(crop.x/texture.width,1-crop.yMax/texture.height,crop.width/texture.width,crop.height/texture.height);
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
        void Cloud(float x,float y,float scale,Color color)
        {
            Oval(new Rect(x-86*scale,y-13*scale,172*scale,48*scale),color);
            Circle(new Vector2(x-42*scale,y-7*scale),31*scale,color);
            Circle(new Vector2(x+2*scale,y-18*scale),42*scale,color);
            Circle(new Vector2(x+45*scale,y-3*scale),28*scale,color);
        }
        void DrawCloudFace()
        {
            float happy=Mathf.Max(0,cloudMood),sad=Mathf.Max(0,-cloudMood);
            float amount=Mathf.Abs(cloudMood);
            // Morph the same opaque strokes, so no separate face parts fade or pop apart.
            for(int side=-1;side<=1;side+=2)
                CloudFaceStroke(new Vector2(PivotX+side*22,202+sad*1.5f),
                    happy*4.5f+sad*2,-happy*2.5f,side*sad*1.5f,
                    Mathf.Lerp(6,3.4f,amount),Rgb(0xB2AA98));
            CloudFaceStroke(new Vector2(PivotX,215.5f-cloudMood*3),
                9+happy*2,cloudMood*7,0,Mathf.Lerp(5,3.4f,amount),Rgb(0xD6C4AA));
        }
        void CloudFaceStroke(Vector2 center,float halfWidth,float bend,float tilt,float width,Color color)
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
        void Flower(float x,float y,float size,Color color)
        {
            Vector2 head=new Vector2(x,y),basePoint=head+new Vector2(0,54*size);
            Matrix4x4 original=GUI.matrix;
            try
            {
                GUI.matrix=original*GameLayout.RotationAround(basePoint,GardenAtmosphere.BreezeAngle(garden,playTime,x));
                float bend=x<720 ? 3*size : -3*size;
                Vector2 middle=head+new Vector2(bend,30*size);
                Capsule(basePoint,middle,3.5f*size,leaf);
                Capsule(middle,head,3.5f*size,leaf);
                // Every part sways together around the same root.
                FlowerLeaf(Vector2.Lerp(head,middle,0.83f),22*size,10*size,-28);
                FlowerLeaf(Vector2.Lerp(middle,basePoint,0.35f),24*size,11*size,208);
                for(int i=0;i<5;i++)
                {
                    float a=i*Mathf.PI*2/5-Mathf.PI/2;
                    Circle(head+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*10*size,8*size,color);
                }
                Circle(new Vector2(x,y),6*size,Rgb(0xFFF3CA));
            }
            finally { GUI.matrix=original; }
        }
        void FlowerLeaf(Vector2 attachment,float length,float width,float angle)
        {
            Matrix4x4 original=GUI.matrix;
            try
            {
                GUI.matrix=original*GameLayout.RotationAround(attachment,angle);
                Oval(new Rect(attachment.x-2,attachment.y-width/2,length+2,width),Rgb(0x89AF86));
            }
            finally { GUI.matrix=original; }
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
        void Circle(Vector2 p,float radius,Color color) => Oval(new Rect(p.x-radius,p.y-radius,radius*2,radius*2),color);
        void Capsule(Vector2 from,Vector2 to,float width,Color color)
        {
            Matrix4x4 old=GUI.matrix;Vector2 delta=to-from;
            GUI.matrix=GUI.matrix*GameLayout.RotationAround(from,Mathf.Atan2(delta.y,delta.x)*Mathf.Rad2Deg);
            Fill(new Rect(from.x,from.y-width/2,delta.magnitude,width),color);
            GUI.matrix=old;Circle(from,width/2,color);Circle(to,width/2,color);
        }
        static Rect Fit(Rect box,int width,int height)
        {
            float scale=Mathf.Min(box.width/width,box.height/height);
            return new Rect(box.center.x-width*scale/2,box.center.y-height*scale/2,width*scale,height*scale);
        }
    }
}
