#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// The authored battlefield keeps its editable HUD. A disposable empty match
// supplies only the runtime-created backdrop and fixed panels while editing.
[InitializeOnLoad]
public static class BattlefieldStaticScenePreview
{
    const string Preference="MTGB.BattlefieldStaticScenePreview";
    static readonly List<GameObject> generatedRoots=new();
    static readonly Dictionary<GameObject,bool> originalVisibility=new();
    static ForgeBattlefieldController controller;
    static BattlefieldWorldTable table;
    static Scene previewScene;
    static bool building, cleaning, queued;
    static double nextRefresh;
    static SceneView decoratedView;
    static bool previousGizmos;
    static float previousGridOpacity;

    static BattlefieldStaticScenePreview()
    {
        EditorSceneManager.activeSceneChangedInEditMode+=(_,active)=>{
            if(previewScene.IsValid()&&previewScene!=active)Clean();
            Schedule();
        };
        EditorSceneManager.sceneOpened+=(_,__)=>Schedule();
        EditorSceneManager.sceneSaving+=(_,__)=>Clean();
        EditorSceneManager.sceneSaved+=_=>Schedule();
        EditorSceneManager.sceneClosing+=(_,__)=>Clean();
        EditorApplication.playModeStateChanged+=change=>{
            if(change==PlayModeStateChange.ExitingEditMode)Clean();
            if(change==PlayModeStateChange.EnteredEditMode)Schedule();
        };
        AssemblyReloadEvents.beforeAssemblyReload+=Clean;
        EditorApplication.update+=Refresh;
        Schedule();
    }

    [MenuItem("MTGB/静态战场预览")]
    static void Toggle()
    {
        bool enabled=!EditorPrefs.GetBool(Preference,true);
        EditorPrefs.SetBool(Preference,enabled);
        Menu.SetChecked("MTGB/静态战场预览",enabled);
        if(enabled)Schedule();else Clean();
    }

    [MenuItem("MTGB/静态战场预览",true)]
    static bool ValidateToggle()
    {
        Menu.SetChecked("MTGB/静态战场预览",EditorPrefs.GetBool(Preference,true));
        return true;
    }

    static void Schedule()
    {
        if(queued)return;
        queued=true;
        EditorApplication.delayCall+=()=>{queued=false;Build();};
    }

    static HashSet<int> SceneObjects(Scene scene)
    {
        var result=new HashSet<int>();
        foreach(var root in scene.GetRootGameObjects())
            foreach(var node in root.GetComponentsInChildren<Transform>(true))result.Add(node.gameObject.GetInstanceID());
        return result;
    }

    static void CleanOrphanPreviewObjects()
    {
        foreach(var go in Resources.FindObjectsOfTypeAll<GameObject>()){
            if(go==null||go.scene.IsValid()||go.transform.parent!=null
                ||(go.hideFlags&HideFlags.DontSaveInEditor)==0)continue;
            if(go.name=="World cards"||go.name=="HUD and lifted cards"||go.name=="Battlefield Editor Preview")
                UnityEngine.Object.DestroyImmediate(go);
        }
    }

    static void Build()
    {
        if(building||cleaning||controller!=null||EditorApplication.isPlayingOrWillChangePlaymode
            ||EditorApplication.isCompiling||!EditorPrefs.GetBool(Preference,true))return;
        var scene=SceneManager.GetActiveScene();
        if(!scene.IsValid()||!ForgeBattlefieldController.IsBattlefieldSceneActive)return;
        var canvas=scene.GetRootGameObjects().FirstOrDefault(go=>go.name=="Canvas");
        var layout=canvas!=null?canvas.GetComponent<BattlefieldRedesignLayout>():null;
        var world=canvas!=null?canvas.GetComponent<BattlefieldWorldTable>():null;
        if(layout==null||world==null)return;
        building=true;
        try{
            var sample=layout.ReadEditorPreview();
            if(sample==null)return;
            CleanOrphanPreviewObjects();
            var original=SceneObjects(scene);
            originalVisibility.Clear();
            foreach(var root in scene.GetRootGameObjects())
                foreach(var node in root.GetComponentsInChildren<Transform>(true))
                    originalVisibility[node.gameObject]=node.gameObject.activeSelf;
            previewScene=scene;
            table=world;
            GamePreferences.PrepareEditorPreview();
            table.InitializeEditorPreview();
            if(table.CardsRoot==null)throw new InvalidOperationException("World table preview could not initialize.");
            var host=new GameObject("Battlefield Editor Preview");
            host.hideFlags=HideFlags.HideInHierarchy|HideFlags.DontSaveInEditor;
            controller=host.AddComponent<ForgeBattlefieldController>();
            int startingLife=sample.formatId=="commander"?40:20;
            var state=new ForgeSeatView{
                seat=sample.seat,turn=1,turnSeat=sample.seat,prioritySeat=sample.seat,
                phase=sample.phase,boardKey=sample.boardKey,formatId=sample.formatId,
                players=sample.players.Select(player=>new ForgePlayerView{
                    seat=player.seat,targetId=player.targetId,life=startingLife
                }).ToArray()
            };
            controller.Present(state,state.seat,false,null,null,"编辑预览");
            foreach(var root in scene.GetRootGameObjects())
                foreach(var node in root.GetComponentsInChildren<Transform>(true)){
                    var go=node.gameObject;
                    if(original.Contains(go.GetInstanceID()))continue;
                    go.hideFlags|=HideFlags.DontSaveInEditor;
                    if(node.parent==null||original.Contains(node.parent.gameObject.GetInstanceID())){
                        go.hideFlags|=HideFlags.HideInHierarchy;
                        if(go!=host&&go!=table.CardsRoot.gameObject&&go!=table.HudCamera.gameObject)
                            generatedRoots.Add(go);
                    }
                }
            Canvas.ForceUpdateCanvases();
            DecorateSceneView();
            RefreshNow();
            nextRefresh=EditorApplication.timeSinceStartup+.2;
        }catch(Exception error){
            Debug.LogError("Battlefield edit preview: "+error);
            Clean();
        }finally{building=false;}
    }

    static void Refresh()
    {
        if(EditorApplication.timeSinceStartup<nextRefresh)return;
        nextRefresh=EditorApplication.timeSinceStartup+.2;
        if(controller==null){Build();return;}
        if(EditorApplication.isPlayingOrWillChangePlaymode)return;
        RefreshNow();
    }

    static void RefreshNow()
    {
        if(controller==null||table==null)return;
        var canvas=table.GetComponent<Canvas>();
        foreach(var transition in canvas.GetComponentsInChildren<BattlefieldUiTransition>(true))
            transition.SnapEditorPreview();
        Canvas.ForceUpdateCanvases();
        table.RefreshEditorPreview();
        EditorApplication.QueuePlayerLoopUpdate();
    }

    static object SceneGrid(SceneView view)
        =>view?.GetType().GetProperty("sceneViewGrids",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic)?.GetValue(view);

    static PropertyInfo GridOpacity(object grid)
        =>grid?.GetType().GetProperty("gridOpacity",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);

    static void DecorateSceneView()
    {
        var view=SceneView.lastActiveSceneView;
        if(view==null)return;
        decoratedView=view;
        previousGizmos=view.drawGizmos;
        var grid=SceneGrid(view);
        previousGridOpacity=GridOpacity(grid)?.GetValue(grid) is float opacity?opacity:1f;
        view.drawGizmos=false;
        GridOpacity(grid)?.SetValue(grid,0f);
        if(view.size<4f||view.size>20f)
            view.LookAtDirect(Vector3.zero,Quaternion.Euler(67.4f,0,0),8f);
        view.Repaint();
    }

    static void RestoreSceneView()
    {
        if(decoratedView==null)return;
        decoratedView.drawGizmos=previousGizmos;
        var grid=SceneGrid(decoratedView);
        GridOpacity(grid)?.SetValue(grid,previousGridOpacity);
        decoratedView.Repaint();
        decoratedView=null;
    }

    static void Clean()
    {
        if(cleaning)return;
        cleaning=true;
        try{
            RestoreSceneView();
            if(table!=null){
                table.GetComponent<BattlefieldRedesignLayout>()?.SetHudVisible(false);
                table.DisposeEditorPreview();
            }
            foreach(var go in generatedRoots)if(go!=null)UnityEngine.Object.DestroyImmediate(go);
            generatedRoots.Clear();
            if(controller!=null)UnityEngine.Object.DestroyImmediate(controller.gameObject);
            controller=null;table=null;
            foreach(var pair in originalVisibility)
                if(pair.Key!=null&&pair.Key.activeSelf!=pair.Value)pair.Key.SetActive(pair.Value);
            originalVisibility.Clear();
            GamePreferences.ClearEditorPreview();
            if(previewScene.IsValid()&&previewScene.isLoaded)EditorApplication.QueuePlayerLoopUpdate();
            previewScene=default;
        }finally{cleaning=false;}
    }
}
#endif
