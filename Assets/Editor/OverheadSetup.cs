using System;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Naispo.Overhead;

public static class OverheadSetup
{
    const string ScenePath="Assets/Scenes/OverheadSort.unity";
    [MenuItem("Overhead/Create prototype scene")]
    public static void CreateScene()
    {
        GraphicsSettings.defaultRenderPipeline=null;
        QualitySettings.renderPipeline=null;
        PlayerSettings.companyName="NAISPO";
        PlayerSettings.productName="NAISPO Overhead";
        PlayerSettings.defaultScreenWidth=1440;
        PlayerSettings.defaultScreenHeight=900;
        PlayerSettings.fullScreenMode=FullScreenMode.Windowed;
        PlayerSettings.runInBackground=true;
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var camera=new GameObject("Camera").AddComponent<Camera>();
        camera.clearFlags=CameraClearFlags.SolidColor;
        camera.backgroundColor=new Color(0.025f,0.04f,0.07f);
        camera.orthographic=true;
        camera.transform.position=new Vector3(0,0,-10);
        camera.tag="MainCamera";
        var app=new GameObject("Overhead prototype");
        app.AddComponent<OverheadDepthSource>();
        app.AddComponent<OverheadApp>();
        System.IO.Directory.CreateDirectory("Assets/Scenes");
        EditorSceneManager.SaveScene(scene,ScenePath);
        EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(ScenePath,true)};
        AssetDatabase.SaveAssets();
        Debug.Log("OVERHEAD_SCENE_OK");
    }
    [MenuItem("Overhead/Build Windows")]
    public static void BuildWindows()
    {
        CreateScene();
        var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions {
            scenes=new[]{ScenePath},target=BuildTarget.StandaloneWindows64,
            locationPathName="Builds/Windows/NAISPO-Overhead.exe",options=BuildOptions.None
        });
        if(report.summary.result!=BuildResult.Succeeded) throw new Exception("Overhead build failed");
        Debug.Log("OVERHEAD_BUILD_OK");
    }
}
