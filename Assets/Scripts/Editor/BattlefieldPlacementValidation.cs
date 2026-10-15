#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Explicit display adapters exercise production layout and public projection.
// They do not create native game objects or claim new rule outcomes.
public sealed class BattlefieldPlacementValidation
{
    const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
    static T Field<T>(object o,string n)=>(T)o.GetType().GetField(n,Hidden|BindingFlags.Public).GetValue(o);
    static void Set(object o,string n,object v)=>o.GetType().GetField(n,Hidden).SetValue(o,v);
    static void Prop(object o,string n,object v)=>o.GetType().GetProperty(n).SetValue(o,v);
    static object Call(object o,string n,params object[] args)=>o.GetType().GetMethod(n,Hidden).Invoke(o,args);
    readonly List<string> steps=new();readonly List<string> failures=new();string output;ForgeBattlefieldController host;
    void Check(bool ok,string why){steps.Add((ok?"PASS ":"FAIL ")+why);if(!ok)failures.Add(why);}
    public static void Run(string output){
        if(!Application.isPlaying)throw new Exception("Battlefield Play mode required");
        Directory.CreateDirectory(output);var go=new GameObject("PlacementValidation");UnityEngine.Object.DontDestroyOnLoad(go);
        var runner=new BattlefieldPlacementValidation{output=output,host=go.AddComponent<ForgeBattlefieldController>()};runner.host.enabled=false;runner.host.StartCoroutine(runner.Guard());
    }
    IEnumerator Guard(){
        File.WriteAllText(output+"/result.txt","RUNNING");var e=Exercise();
        while(true){object next;try{if(!e.MoveNext())break;next=e.Current;}catch(Exception ex){failures.Add(ex.ToString());break;}yield return next;}
        (e as IDisposable)?.Dispose();File.WriteAllLines(output+"/steps.txt",steps);
        File.WriteAllText(output+"/result.txt",failures.Count==0?"PASS bounded projection/layout/exit checks":string.Join("\n",failures.Prepend("FAIL")));UnityEngine.Object.Destroy(host.gameObject);
    }
    static Rect Bounds(RectTransform root,RectTransform surface){var c=new Vector3[4];root.GetWorldCorners(c);var points=c.Select(p=>(Vector2)surface.InverseTransformPoint(p)).ToArray();return Rect.MinMaxRect(points.Min(p=>p.x),points.Min(p=>p.y),points.Max(p=>p.x),points.Max(p=>p.y));}
    static void Click(GameObject root){var r=(RectTransform)root.transform;var p=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(null,r.TransformPoint(r.rect.center))};var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(p,hits);if(hits.Count==0||!hits[0].gameObject.transform.IsChildOf(root.transform))throw new Exception("Blocked click "+root.name);ExecuteEvents.ExecuteHierarchy(hits[0].gameObject,p,ExecuteEvents.pointerClickHandler);}
    static ForgeCardView Clone(ForgeCardView c)=>JsonUtility.FromJson<ForgeCardView>(JsonUtility.ToJson(c));
    IEnumerator Exercise(){
        var ui=host;bool enabled=ui.enabled;ui.enabled=false;
        float scale=GamePreferences.TextScale;var oldService=Field<OnlineMatchService>(ui,"service");OnlineMatchService observer=null;
        try{
            while(!GamePreferences.Ready)yield return null;
            var seed=JsonUtility.FromJson<ForgeSeatView>(File.ReadAllText(".tools/live-fixes-1014/food-view.json"));
            var combat=JsonUtility.FromJson<ForgeSeatView>(File.ReadAllText(".tools/ui-browsing-boundary-1014/attack-after/initial.json"));
            var walker=combat.players.SelectMany(p=>p.battlefield).First(c=>c.planeswalker);var battle=combat.players.SelectMany(p=>p.battlefield).First(c=>c.battle);
            var land=seed.players.SelectMany(p=>p.battlefield).First(c=>c.land);var support=seed.players.SelectMany(p=>p.battlefield).First(c=>!c.land&&!c.creature);var creature=seed.players.SelectMany(p=>p.battlefield).First(c=>c.creature);
            foreach(float size in new[]{1f,1.3f})foreach(int count in new[]{1,8,16,17}){
                GamePreferences.SetScale(size);var v=JsonUtility.FromJson<ForgeSeatView>(JsonUtility.ToJson(seed));v.awaitingInput=false;v.request=null;v.notices=Array.Empty<ForgeNotice>();
                foreach(var player in v.players){
                    var field=new List<ForgeCardView>();int id=player.seat*10000;
                    var templates=Enumerable.Repeat(land,Math.Min(count,16)).Concat(Enumerable.Repeat(creature,count>=16?16:2)).Concat(Enumerable.Repeat(support,count>=16?6:1)).Concat(count==17?new[]{walker,battle}:Array.Empty<ForgeCardView>());
                    foreach(var template in templates){var c=Clone(template);c.id=id++;c.ownerSeat=player.seat;c.tapped=c.land&&c.id%2==0;c.stackableStateKnown=true;c.stackablePermanent=false;c.zone="Battlefield";field.Add(c);}player.battlefield=field.ToArray();
                    player.manaPool=new[]{0,2,0,0,2,0};player.snowManaPool=new[]{0,1,0,0,0,0};
                    player.manaPoolEntries=new[]{new ForgeManaPoolEntry{id="mana:U",symbol="U",count=1},new ForgeManaPoolEntry{id="mana:S:U",symbol="U",count=1,snow=true},new ForgeManaPoolEntry{id="mana:G:restricted:100:1",symbol="G",count=2,restricted=true,sourceName="PRIVATE SOURCE"}};
                    if(count==17){
                        player.effectMarkers=new[]{"The Monarch","The Initiative","The Ring"}.Select((name,i)=>new ForgeCardView{id=id+100+i,ownerSeat=player.seat,name=name,effectMarker=true,markerKind="effect",helperArtName=name,zone="Command",currentRules=""}).ToArray();
                        player.emblems=Enumerable.Range(0,3).Select(i=>new ForgeCardView{id=id+200+i,ownerSeat=player.seat,name="徽记 "+i,emblem=true,zone="Command",currentRules=""}).ToArray();
                    }
                }
                var projected=OnlineSpectatorView.Create(new OnlinePlayerViewState{forge=v},3).forge;
                Check(projected.players.All(p=>p.manaPoolEntries?.Length==3&&p.manaPoolEntries[1].snow&&p.manaPoolEntries[2].restricted),size+"/"+count+" spectator preserves both mana bucket kinds");
                Check(!JsonUtility.ToJson(projected).Contains("PRIVATE SOURCE")&&projected.request==null&&projected.players.All(p=>p.hand.Length==0),size+"/"+count+" public projection omits private source/hand/request");
                // Display the original public field so placement tests are independent of missing buckets.
                ui.Present(null,1,false,null,null,"");ui.Present(v,1,false,null,null,"");yield return new WaitForSecondsRealtime(.4f);Canvas.ForceUpdateCanvases();
                var surface=Field<RectTransform>(ui,"surface");var roots=((IDictionary)Field<object>(ui,"arenaCards")).Values.Cast<object>().Select(o=>Field<RectTransform>(o,"root")).ToArray();
                var lanes=Field<Dictionary<string,ScrollRect>>(ui,"denseFieldRows");
                foreach(bool own in new[]{true,false}){
                    var pool=Field<RectTransform>(ui,own?"manaPoolRow":"publicManaPoolRow");var rect=Bounds(pool,surface);string prefix=own?"Your":"Opponent";
                    Check(rect.xMin>-surface.rect.width*.5f+340,size+"/"+count+" "+prefix+" mana outside marker/info column");
                    var landIds=v.players.Single(p=>p.seat==(own?1:2)).battlefield.Where(c=>c.land).Select(c=>"Card_"+c.id).ToHashSet();
                    Check(roots.Where(r=>landIds.Contains(r.name)).All(r=>!rect.Overlaps(Bounds(r,surface))),size+"/"+count+" "+prefix+" mana never overlaps land cards");
                    var creatureIds=v.players.Single(p=>p.seat==(own?1:2)).battlefield.Where(c=>c.creature).Select(c=>"Card_"+c.id).ToHashSet();
                    Check(roots.Where(r=>creatureIds.Contains(r.name)).All(r=>!rect.Overlaps(Bounds(r,surface))),size+"/"+count+" "+prefix+" mana never overlaps creatures");
                    Check(pool.GetComponentsInChildren<Button>().All(b=>!b.interactable),size+"/"+count+" public idle mana cannot spend");
                    var lane=(RectTransform)lanes[prefix+"Support"].transform;var sr=Bounds(lane,surface);
                    Check(own?sr.yMin<-300:sr.yMax>300,size+"/"+count+" "+prefix+" support beside own hand edge");
                    if(count==17){
                        var wr=Bounds((RectTransform)lanes[prefix+"Planeswalkers"].transform,surface);var br=Bounds((RectTransform)lanes[prefix+"Battles"].transform,surface);
                        Check(!sr.Overlaps(wr)&&!wr.Overlaps(br)&&!sr.Overlaps(br),size+"/mixed "+prefix+" support/walker/battle lanes separated");
                        var markerIds=v.players.Single(p=>p.seat==(own?1:2)).effectMarkers.Concat(v.players.Single(p=>p.seat==(own?1:2)).emblems).Select(c=>"Card_"+c.id).ToHashSet();
                        Check(roots.Where(r=>markerIds.Contains(r.name)).All(r=>!rect.Overlaps(Bounds(r,surface))),size+"/mixed "+prefix+" mana clear of full state/emblem column");
                    }
                }
                var a=Bounds((RectTransform)lanes["YourSupport"].transform,surface);var b=Bounds((RectTransform)lanes["OpponentSupport"].transform,surface);
                Check(b.yMin-a.yMax>80,size+"/"+count+" opposing support regions separated");
                ScreenCapture.CaptureScreenshot(output+"/board-"+size+"-"+count+".png");yield return new WaitForEndOfFrame();
                ui.Present(projected,1,false,null,null,"");yield return new WaitForEndOfFrame();
                foreach(string fieldName in new[]{"poolCells","publicPoolCells"}){
                    var cells=((IDictionary)Field<object>(ui,fieldName)).Values.Cast<object>().ToArray();
                    Check(cells.Length==3&&cells.Sum(c=>Field<int>(c,"amount"))==4,size+"/"+count+" projected "+fieldName+" renders all public buckets/counts");
                }
            }
            observer=OnlineMatchService.EnsureInstance();if(observer.IsOnline)throw new Exception("Active editor network session; exit test must be isolated");
            Prop(observer,"LocalSeat",3);Prop(observer,"IsInMatch",true);Set(ui,"service",observer);
            var publicView=OnlineSpectatorView.Create(new OnlinePlayerViewState{forge=seed},3).forge;ui.Present(publicView,1,false,null,null,"");yield return new WaitForEndOfFrame();Canvas.ForceUpdateCanvases();
            var authored=Field<Transform>(ui,"authored");var exit=authored.Find("GearBox/ExitSpectator")?.GetComponent<Button>();
            Check(exit!=null&&exit.gameObject.activeInHierarchy&&exit.interactable,"spectator has visible exit button");
            var viewShortcut=Field<Button>(ui,"viewBattlefieldButton");
            Check(exit!=null&&!Bounds((RectTransform)exit.transform,Field<RectTransform>(ui,"surface")).Overlaps(Bounds((RectTransform)viewShortcut.transform,Field<RectTransform>(ui,"surface"))),"spectator exit has separate slot from view/return shortcut");
            ScreenCapture.CaptureScreenshot(output+"/spectator-controls.png");yield return new WaitForEndOfFrame();
            if(exit!=null){Click(exit.gameObject);yield return null;yield return null;Check(!observer.IsInMatch&&observer.LocalSeat==0&&UnityEngine.SceneManagement.SceneManager.GetActiveScene().name=="Main","exit ray clears spectator session and returns Main");}
        }finally{if(ui!=null){ui.enabled=enabled;Set(ui,"service",oldService);}if(observer!=null&&observer.LocalSeat>2)observer.Disconnect();GamePreferences.SetScale(scale);}
    }
}
#endif
