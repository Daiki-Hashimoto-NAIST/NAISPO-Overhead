using UnityEngine;

namespace Naispo.Overhead
{
    public sealed partial class OverheadApp
    {
        Vector2Int windowedSize=new Vector2Int(1440,900);
        bool displayChanging;
        FullScreenMode requestedDisplayMode;
        float displayRequestTime;
        int displayRequestFrame;

        string DisplayButtonLabel => displayChanging ? "きりかえ中…"
            : Screen.fullScreenMode==FullScreenMode.Windowed ? "全画面にする（F11）" : "ウィンドウに戻す（F11）";

        void UpdateDisplay(bool togglePressed)
        {
            // Unity applies SetResolution at the end of the frame. Keep the same request
            // pending across IMGUI passes, even while gameplay or tracking is paused.
            if(displayChanging&&Time.frameCount>displayRequestFrame&&
                (Screen.fullScreenMode==requestedDisplayMode||Time.unscaledTime-displayRequestTime>2f))
                displayChanging=false;
            if(togglePressed)ToggleDisplay();
        }

        void ToggleDisplay()
        {
            if(displayChanging)return;
            bool enterFullscreen=Screen.fullScreenMode==FullScreenMode.Windowed;
            if(enterFullscreen)windowedSize=new Vector2Int(Screen.width,Screen.height);
            requestedDisplayMode=enterFullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
            displayChanging=true;
            displayRequestFrame=Time.frameCount;
            displayRequestTime=Time.unscaledTime;
            draggingGate=false;
            if(enterFullscreen)
            {
                Resolution desktop=Screen.currentResolution;
                Screen.SetResolution(desktop.width,desktop.height,requestedDisplayMode);
            }
            else Screen.SetResolution(windowedSize.x,windowedSize.y,requestedDisplayMode);
        }
    }
}
