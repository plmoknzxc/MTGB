const string W="D:/project/MTGB/design-demos/battlefield-redesign-2026-10-24/";
var ui=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldController>();var flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance;
void Quiet(){foreach(var pointer in UnityEngine.Object.FindObjectsByType<ForgeBattlefieldCardPointer>(FindObjectsSortMode.None)){pointer.ResetHover();pointer.AllowHandHover=false;}typeof(ForgeBattlefieldController).GetMethod("LeaveInspectCard",flags).Invoke(ui,null);}
System.Collections.IEnumerator Run(){
 var v=JsonUtility.FromJson<ForgeSeatView>(System.IO.File.ReadAllText(W+"native-full-seat.json"));ui.Present(null,1,false,null,null,"");yield return null;ui.Present(v,1,true,_=>{},null,"本地 UI 样本");typeof(ForgeBattlefieldController).GetMethod("SetRedesignHandExpanded",flags).Invoke(ui,new object[]{true});foreach(string name in new[]{"ManaPoolChips","PublicManaPool"})GameObject.Find("Canvas").transform.Find(name).GetComponent<BattlefieldHorizontalScroll>().horizontalNormalizedPosition=0;Quiet();yield return new WaitForSecondsRealtime(.5f);ScreenCapture.CaptureScreenshot(W+"stack-direct-effects.png");yield return new WaitForEndOfFrame();
 ui.Present(v,1,false,null,null,"副本 · 只读布局预览");
}
ui.StartCoroutine(Run());return "Actual direct complete-effect screenshot started";
