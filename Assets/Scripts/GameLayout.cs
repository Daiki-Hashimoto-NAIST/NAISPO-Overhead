using UnityEngine;

namespace Naispo.Overhead
{
    // One uniform transform is shared by drawing and pointer input.
    public static class GameLayout
    {
        public const float Width=1440,Height=900,StageOffsetX=215;
        public static readonly Rect PlayArea=new Rect(220,175,1000,640);
        public static float Scale(float width,float height) => Mathf.Min(width/Width,height/Height);
        public static Vector2 Offset(float width,float height)
        {
            float scale=Scale(width,height);
            return new Vector2((width-Width*scale)/2,(height-Height*scale)/2);
        }
        public static Vector2 ScreenToCanvas(Vector2 point,float width,float height) => (point-Offset(width,height))/Scale(width,height);
        public static Vector2 CanvasToScreen(Vector2 point,float width,float height) => point*Scale(width,height)+Offset(width,height);
        public static Matrix4x4 Matrix(float width,float height)
        {
            Vector2 offset=Offset(width,height);
            float scale=Scale(width,height);
            return Matrix4x4.TRS(new Vector3(offset.x,offset.y,0),Quaternion.identity,new Vector3(scale,scale,1));
        }
        // Right-multiply the current GUI matrix so the pivot stays in local drawing coordinates.
        public static Matrix4x4 RotationAround(Vector2 pivot,float degrees)
        {
            Vector3 center=new Vector3(pivot.x,pivot.y,0);
            return Matrix4x4.Translate(center)
                *Matrix4x4.Rotate(Quaternion.Euler(0,0,degrees))
                *Matrix4x4.Translate(-center);
        }
    }
}
