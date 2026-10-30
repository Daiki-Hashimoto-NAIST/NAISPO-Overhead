using UnityEngine;

namespace Naispo.Overhead
{
    public sealed partial class OverheadApp
    {
        void DrawMonsterEmotion(MonsterVisualPose pose,float centerX)
        {
            if(!pose.Celebrating&&pose.Expression==0)return;
            float celebration=playTime-FinaleStart;
            float elapsed=pose.Celebrating ? celebration-0.35f : pose.Elapsed-0.18f;
            float remaining=pose.Celebrating ? (garden.IsComplete ? 2.6f : 1.8f)-celebration : 2.25f-pose.Elapsed;
            float appear=Mathf.SmoothStep(0,1,Mathf.Clamp01(elapsed/0.25f));
            float fade=appear*Mathf.SmoothStep(0,1,Mathf.Clamp01(remaining/0.35f));
            if(fade<=0)return;

            // Keep a single quiet icon clear of the jumping head and the food lane.
            // The body can sway underneath it without making the emotion hard to read.
            Vector2 center=new Vector2(centerX,MonsterFeetY-pose.Height-85+(1-appear)*5);
            if(pose.Celebrating||pose.Expression==1)
                DrawIcon(heartIcon,center,new Vector2(44,44),Rgb(0xDB8C9F,fade));
            else if(pose.Expression==2)
                DrawIcon(tangleIcon,center,new Vector2(72,54),Rgb(0xA09599,fade));
        }

    }
}
