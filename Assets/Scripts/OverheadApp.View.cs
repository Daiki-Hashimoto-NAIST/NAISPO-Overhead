using UnityEngine;

namespace Naispo.Overhead
{
    public sealed partial class OverheadApp
    {
        static Color Rgb(int value,float alpha=1) => new Color(((value>>16)&255)/255f,((value>>8)&255)/255f,(value&255)/255f,alpha);
        readonly Color ink=Rgb(0x59433E),muted=Rgb(0x847B67),coral=Rgb(0xEF8875),cyan=Rgb(0x66BABB);
        readonly Color cream=Rgb(0xFFFDF3),outline=Rgb(0xDFD5B7),leaf=Rgb(0x729C74);
        Texture2D paper;
        Font roundFont;
        GUIStyle label,button,sliderThumb;

        void InitializeArt()
        {
            roundFont=Resources.Load<Font>("Fonts/RoundedJapanese");
            if(roundFont==null)Debug.LogError("Missing rounded Japanese font");
            paper=new Texture2D(8,128,TextureFormat.RGBA32,false) { wrapMode=TextureWrapMode.Clamp };
            for(int y=0;y<128;y++)for(int x=0;x<8;x++)
                paper.SetPixel(x,y,Color.Lerp(Rgb(0xEAF3DC),Rgb(0xFFF5DE),y/127f));
            paper.Apply();
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
            DrawFooter();
            if(settingsOpen)DrawSettings();
            else if(paused)DrawPause();
            GUI.matrix=original;
        }
        void DrawLandscape()
        {
            GUI.DrawTexture(new Rect(0,0,1440,900),paper);
            Oval(new Rect(-190,490,630,470),Rgb(0xD8E8C5));
            Oval(new Rect(1070,510,630,430),Rgb(0xCDE4D1));
            Oval(new Rect(-210,650,730,310),Rgb(0xC5DCA9));
            Oval(new Rect(1010,692,650,280),Rgb(0xB9D6BC));
            Oval(new Rect(170,660,1100,330),Rgb(0xF5E8C7));
            Cloud(124,263,0.7f,Rgb(0xFFFCF0));
            Cloud(1310,230,0.85f,Rgb(0xFFFCF0));
            Cloud(1200,373,0.45f,Rgb(0xF8FAEB));
            // Quiet decorative plants stay outside the play area.
            Plant(98,748,1.1f);Plant(1317,734,1.2f);Plant(142,801,0.65f);Plant(1260,793,0.65f);
            Flower(76,692,1.0f,Rgb(0xE9AF94));Flower(1372,678,0.85f,Rgb(0xEFCA73));
            Flower(170,766,0.68f,Rgb(0xE6B9BC));Flower(1283,761,0.65f,Rgb(0xFFF7DD));
            for(int i=0;i<6;i++)
            {
                float x=70+i*47;
                Oval(new Rect(x,816+(i%2)*8,8,4),Rgb(0xC9C799));
                Oval(new Rect(1110+i*43,821-(i%3)*5,8,4),Rgb(0xB0C49F));
            }
        }
        void DrawHeader()
        {
            Text(new Rect(65,27,500,28),"NAISPO の あそびば",16,muted);
            Text(new Rect(59,60,820,67),"もぐもぐ モンスター",49,ink);
            Text(new Rect(66,128,760,30),"ぼうを かたむけて、ごはんを とどけよう",19,muted);
            Card(new Rect(941,40,215,112),cream,24);
            Sparkle(new Vector2(980,100),21,Rgb(0xE6B450));
            Text(new Rect(1010,53,130,26),"ごちそう",16,muted,TextAnchor.MiddleLeft);
            Text(new Rect(1008,80,133,55),score+" こ",36,ink,TextAnchor.MiddleLeft);
            if(SoftButton(new Rect(1180,46,116,49),"ひとやすみ",cream)) { paused=true;draggingGate=false; }
            if(SoftButton(new Rect(1308,46,91,49),"せってい",cream)) { settingsOpen=true;draggingGate=false; }
            Text(new Rect(1180,114,219,28),streak>0 ? $"{streak} れんぞく！" : "ゆっくり あそぼう",18,leaf,TextAnchor.MiddleCenter);
        }
        void DrawPlayfield()
        {
            // A small cloud is the source of the food; game coordinates remain unchanged.
            Cloud(PivotX,209,1.08f,cream);
            Circle(new Vector2(PivotX-22,202),3,Rgb(0xB2AA98));
            Circle(new Vector2(PivotX+22,202),3,Rgb(0xB2AA98));
            Rounded(new Rect(PivotX-9,213,18,5),2,Rgb(0xD6C4AA));
            Oval(new Rect(20,704,290,70),Rgb(0xECCAB2));
            Oval(new Rect(700,704,290,70),Rgb(0xBFDAD0));
            DrawMonster(leftMonster,165,coral,false);
            DrawMonster(rightMonster,845,cyan,true);
            DrawFavorite(165,false);
            DrawFavorite(845,true);
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
            foreach(var token in tokens)DrawFood(token.Position,token.IsFish,FoodSize,token.Rotation);
            string message=!ready ? "りょうてを ひろげてみよう" : playTime-feedbackTime<2.5f ? feedback : "いちごは ひだり。さかなは みぎ。";
            Card(new Rect(306,713,398,72),cream,28);
            Text(new Rect(322,726,366,43),message,21,ink,TextAnchor.MiddleCenter);
            if(!ready)
            {
                Rounded(new Rect(350,353,310,67),24,cream);
                Text(new Rect(368,364,274,43),analyzer.CalibrationFramesRemaining>0 ? "じゅんび しているよ…" : "みつけるまで まってね",21,muted,TextAnchor.MiddleCenter);
            }
        }
        void DrawFavorite(float centerX,bool isFish)
        {
            Card(new Rect(centerX-100,748,200,64),cream,28);
            DrawFood(new Vector2(centerX-53,778),isFish,45);
            Text(new Rect(centerX-17,758,104,40),isFish ? "さかな" : "いちご",22,ink,TextAnchor.MiddleCenter);
        }
        void DrawMonster(Monster monster,float centerX,Color accent,bool isFish)
        {
            float elapsed=playTime-monster.ReactedAt;
            int expression=elapsed<1.6f ? monster.Expression : 0;
            Texture2D face=monster.Faces[expression];
            if(face==null)face=monster.Faces[0];
            if(face==null)return;
            float motion=Mathf.Clamp01(1-elapsed/0.6f);
            float bounce=expression==1 ? Mathf.Abs(Mathf.Sin(elapsed*14))*12*motion : 0;
            float wiggle=expression==2 ? Mathf.Sin(elapsed*24)*5*motion : 0;
            float idle=Mathf.Sin(playTime*2+(isFish ? 1.5f : 0))*2;
            Oval(new Rect(centerX-107,717,214,26),Rgb(0xA99B7D,0.18f));
            Rect crop=monster.Crop;
            const float width=248;
            float height=width*crop.height/crop.width;
            Rect uv=new Rect(crop.x/face.width,1-crop.yMax/face.height,crop.width/face.width,crop.height/face.height);
            GUI.DrawTextureWithTexCoords(new Rect(centerX-width/2+wiggle,730-height-bounce+idle,width,height),face,uv,true);
            float bubbleY=730-height-75;
            Rounded(new Rect(centerX-103,bubbleY,206,53),24,cream);
            Circle(new Vector2(centerX+30,bubbleY+52),9,cream);
            string speech=expression==1 ? "おいし〜い！" : expression==2 ? "あれれ？" : isFish ? "さかなが すき！" : "いちごが すき！";
            Text(new Rect(centerX-97,bubbleY+6,194,38),speech,21,expression==2 ? muted : ink,TextAnchor.MiddleCenter);
            if(expression==1)
            {
                float pulse=1+Mathf.Sin(elapsed*9)*0.15f;
                Sparkle(new Vector2(centerX-132,730-height+70),13*pulse,Rgb(0xE8B94F));
                Sparkle(new Vector2(centerX+133,730-height+33),10*pulse,Rgb(0xE8B94F));
            }
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
        void DrawFooter()
        {
            Rounded(new Rect(43,838,1354,43),20,Rgb(0xFFF9E9));
            Text(new Rect(66,843,920,33),live ? "りょうてを ひろげて、からだを ゆっくり まわそう" : "マウスを おしたまま、ひだり・みぎに うごかそう",18,muted,TextAnchor.MiddleLeft);
            Text(new Rect(1020,843,351,33),live ? "からだで あそんでいるよ" : "A / D ・ ← / → でも OK",16,leaf,TextAnchor.MiddleRight);
        }
        void DrawPause()
        {
            Fill(new Rect(0,0,1440,900),Rgb(0x4C5145,0.34f));
            Card(new Rect(470,290,500,310),cream,36);
            Text(new Rect(500,325,440,64),"ちょっと ひとやすみ",31,ink,TextAnchor.MiddleCenter);
            Text(new Rect(500,399,440,38),"じゅんびが できたら、つづけよう",19,muted,TextAnchor.MiddleCenter);
            if(SoftButton(new Rect(577,481,286,61),"つづける",Rgb(0xE8BE6C)))paused=false;
        }
        void DrawSettings()
        {
            Fill(new Rect(0,0,1440,900),Rgb(0x4C5145,0.34f));
            Card(new Rect(260,88,920,740),cream,32);
            Text(new Rect(298,113,610,52),"あそびの せってい",30,ink);
            Text(new Rect(300,171,700,33),"Kinect をつなぐと、からだの向きで あそべます。",18,muted);
            if(SoftButton(new Rect(1050,113,90,48),"とじる",Rgb(0xF1E7D1))) { settingsOpen=false;draggingGate=false; }
            if(SoftButton(new Rect(300,225,182,49),"マウス・キー",!live ? Rgb(0xD6E7CD) : Rgb(0xF3EAD7)))
            { source.StopCapture();live=false;ready=true;targetAngle=0;ResetRound(); }
            if(SoftButton(new Rect(495,225,182,49),"Kinect をつなぐ",live ? Rgb(0xD6E7CD) : Rgb(0xF3EAD7)))Connect();
            Rect box=new Rect(300,292,377,246);
            Rounded(box,20,Rgb(0xE5E9DC));
            if(live&&preview!=null)
            {
                Rect fitted=Fit(new Rect(312,304,353,222),preview.width,preview.height);
                GUI.DrawTexture(fitted,preview);
                var r=analyzer.Result;
                if(r.Valid)
                {
                    float x=mirror ? preview.width-1-r.CenterX : r.CenterX;
                    Vector2 p=new Vector2(fitted.x+x/preview.width*fitted.width,fitted.y+r.CenterY/preview.height*fitted.height);
                    float a=r.AngleDegrees*Mathf.Deg2Rad;
                    Vector2 d=new Vector2(Mathf.Cos(a)*(mirror ? -1 : 1),Mathf.Sin(a))*65;
                    Capsule(p-d,p+d,3,cyan);Circle(p,5,cream);
                }
            }
            else
            {
                Circle(new Vector2(488,374),22,leaf);
                Capsule(new Vector2(488,401),new Vector2(488,458),12,leaf);
                Capsule(new Vector2(405,416),new Vector2(572,416),12,leaf);
                Text(new Rect(319,474,339,36),"ここに カメラの映像が うつります",16,muted,TextAnchor.MiddleCenter);
            }
            string status=!live ? "マウス・キーで あそべます。\nKinect は、まわりを空けてからつないでね。" : !source.Running ? "Kinect を確認してください。\n電源・USB を確認して、つなぎ直してください。" : analyzer.CalibrationFramesRemaining>0 ? "背景をおぼえています。\nカメラの前から はなれて まってね。" : !analyzer.Calibrated ? "背景を読み取れませんでした。\nまわりを空けて、とりなおしてください。" : !ready ? "手をひろげてみよう。\n映らないときは、高さを調整してください。" : $"からだの向きが みえています。\n{source.Fps:F0} FPS / 認識の確かさ {analyzer.Result.Confidence:P0}";
            Text(new Rect(302,559,372,103),status,18,ink);
            if(live&&!source.Running)Text(new Rect(302,661,372,58),source.Status,12,muted);
            Text(new Rect(724,224,398,35),"カメラで どこを みる？",21,ink);
            if(SoftButton(new Rect(724,270,190,48),"ゆか・からだ",!table ? Rgb(0xD6E7CD) : Rgb(0xF3EAD7)))SetPreset(false);
            if(SoftButton(new Rect(928,270,190,48),"つくえ・て",table ? Rgb(0xD6E7CD) : Rgb(0xF3EAD7)))SetPreset(true);
            Text(new Rect(724,342,394,31),"背景からの高さ",18,ink);
            minHeight=SoftSlider(new Rect(724,407,394,22),minHeight,10,1500);
            Text(new Rect(724,378,394,26),$"ひくいところ  {minHeight:F0} mm",16,muted);
            maxHeight=SoftSlider(new Rect(724,476,394,22),maxHeight,Mathf.Max(minHeight+10,50),2500);
            Text(new Rect(724,447,394,26),$"たかいところ  {maxHeight:F0} mm",16,muted);
            GUI.enabled=live&&source.Running;
            if(SoftButton(new Rect(724,525,394,50),"背景を とりなおす",Rgb(0xE8BE6C)))Calibrate();
            GUI.enabled=true;
            Text(new Rect(724,588,394,50),"人や手を映さず、床や机だけにしてから\n「背景を とりなおす」を押してください。",15,muted);
            if(SoftButton(new Rect(724,652,190,43),mirror ? "左右反転：オン" : "左右反転：オフ",Rgb(0xF3EAD7)))mirror=!mirror;
            if(SoftButton(new Rect(928,652,190,43),invert ? "回転反転：オン" : "回転反転：オフ",Rgb(0xF3EAD7)))invert=!invert;
            if(SoftButton(new Rect(300,744,206,48),"はじめから",Rgb(0xF3EAD7)))ResetRound();
            Text(new Rect(526,748,310,40),$"ごちそう {score} こ ・ おとした / ちがった {misses} こ",14,muted,TextAnchor.MiddleCenter);
            if(SoftButton(new Rect(865,738,253,59),"あそびに もどる",Rgb(0xD6E7CD))) { settingsOpen=false;paused=false;draggingGate=false; }
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
        void Plant(float x,float y,float size)
        {
            Capsule(new Vector2(x,y),new Vector2(x,y-55*size),4*size,leaf);
            Oval(new Rect(x-31*size,y-43*size,32*size,17*size),Rgb(0x89AF86));
            Oval(new Rect(x,y-63*size,32*size,18*size),Rgb(0x89AF86));
        }
        void Flower(float x,float y,float size,Color color)
        {
            Capsule(new Vector2(x,y),new Vector2(x,y+35*size),3*size,leaf);
            for(int i=0;i<5;i++) { float a=i*Mathf.PI*2/5;Circle(new Vector2(x+Mathf.Cos(a)*10*size,y+Mathf.Sin(a)*10*size),8*size,color); }
            Circle(new Vector2(x,y),6*size,Rgb(0xFFF3CA));
        }
        void Sparkle(Vector2 p,float radius,Color color)
        {
            Matrix4x4 old=GUI.matrix;
            GUI.matrix=GUI.matrix*GameLayout.RotationAround(p,45);
            Rounded(new Rect(p.x-radius*0.36f,p.y-radius,radius*0.72f,radius*2),radius*0.3f,color);
            Rounded(new Rect(p.x-radius,p.y-radius*0.36f,radius*2,radius*0.72f),radius*0.3f,color);
            GUI.matrix=old;
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
