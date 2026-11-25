const string W="D:/project/MTGB/design-demos/battlefield-redesign-2026-10-24/";
var ui=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldController>();var flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance;var project=typeof(ForgeBattlefieldController).GetMethod("RedesignStackEffect",flags);
var results=new System.Collections.Generic.List<object>();int checks=0,failed=0;string oldLanguage=GamePreferences.Language;float oldScale=GamePreferences.TextScale;
void Check(bool pass,string context){checks++;if(!pass)failed++;results.Add(new{pass,context});}
void Save(string status){var method=Type.GetType("Newtonsoft.Json.JsonConvert, Newtonsoft.Json").GetMethod("SerializeObject",new[]{typeof(object)});System.IO.File.WriteAllText(W+"stack-direct-effects-validation.json",(string)method.Invoke(null,new object[]{new{status,checks,failed,scope="Direct full current-effect scrolling; display adapter only, no new card-rule verification",results}}));}
ForgeSeatView Read(string path)=>JsonUtility.FromJson<ForgeSeatView>(System.IO.File.ReadAllText(path));
ForgeSeatView Sample()=>Read(W+"native-full-seat.json");
Transform Root()=>GameObject.Find("Canvas").transform;
string Effect(ForgeStackItem entry,string fallback="")=>(string)project.Invoke(ui,new object[]{entry,fallback});
string Plain(string text)=>System.Text.RegularExpressions.Regex.Replace(text??"","<[^>]+>","");
string Key(string text)=>System.Text.RegularExpressions.Regex.Replace(Plain(text),@"\s+"," ").Trim().ToLowerInvariant();
Rect Bounds(RectTransform r){var c=new Vector3[4];r.GetWorldCorners(c);return Rect.MinMaxRect(c.Min(v=>v.x),c.Min(v=>v.y),c.Max(v=>v.x),c.Max(v=>v.y));}
bool Click(UnityEngine.UI.Button button){var p=new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current){position=Bounds((RectTransform)button.transform).center,button=UnityEngine.EventSystems.PointerEventData.InputButton.Left};var hits=new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();UnityEngine.EventSystems.EventSystem.current.RaycastAll(p,hits);bool correct=hits.Count>0&&hits[0].gameObject.GetComponentInParent<UnityEngine.UI.Button>()==button;if(correct)UnityEngine.EventSystems.ExecuteEvents.ExecuteHierarchy(hits[0].gameObject,p,UnityEngine.EventSystems.ExecuteEvents.pointerClickHandler);else results.Add(new{button=button.name,hit=hits.FirstOrDefault().gameObject?.name});return correct;}
System.Collections.IEnumerator Wait(float seconds=.15f){yield return new WaitForSecondsRealtime(seconds);Canvas.ForceUpdateCanvases();}
var assembly=typeof(UnityEditor.Editor).Assembly;var gameViewType=assembly.GetType("UnityEditor.GameView");var gameView=UnityEditor.EditorWindow.GetWindow(gameViewType);var sizeProperty=gameViewType.GetProperty("selectedSizeIndex",System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance);int savedSize=(int)sizeProperty.GetValue(gameView);var sizesType=assembly.GetType("UnityEditor.GameViewSizes");var sizes=typeof(UnityEditor.ScriptableSingleton<>).MakeGenericType(sizesType).GetProperty("instance").GetValue(null);var group=sizesType.GetMethod("GetGroup").Invoke(sizes,new object[]{Enum.Parse(assembly.GetType("UnityEditor.GameViewSizeGroupType"),"Standalone")});var added=new System.Collections.Generic.List<int>();
void Resize(int w,int h){int index=((string[])group.GetType().GetMethod("GetDisplayTexts").Invoke(group,null)).Length;var size=Activator.CreateInstance(assembly.GetType("UnityEditor.GameViewSize"),new object[]{Enum.Parse(assembly.GetType("UnityEditor.GameViewSizeType"),"FixedResolution"),w,h,"MTGB complete stack effects"});group.GetType().GetMethod("AddCustomSize").Invoke(group,new[]{size});added.Add(index);sizeProperty.SetValue(gameView,index);gameView.Repaint();}
void Restore(){GamePreferences.SetScale(oldScale);GamePreferences.SetLanguage(oldLanguage);sizeProperty.SetValue(gameView,savedSize);foreach(int i in added.OrderByDescending(i=>i))group.GetType().GetMethod("RemoveCustomSize").Invoke(group,new object[]{i});}
ForgeStackItem Raw(string kind="spell")=>new(){id=97001,kind=kind,controllerSeat=1,rulesText="FIRST_EFFECT_11. SECOND_EFFECT_22!\nTHIRD_EFFECT_33?",description="LIVE_CONTEXT_44.",source=new ForgeCardView{name="Public source",originalName="Public source",currentRules="UNRELATED_KEYWORD_99",originalRules="UNRELATED_KEYWORD_99"}};
void Quiet(){foreach(var pointer in UnityEngine.Object.FindObjectsByType<ForgeBattlefieldCardPointer>(FindObjectsSortMode.None)){pointer.ResetHover();pointer.AllowHandHover=false;}typeof(ForgeBattlefieldController).GetMethod("LeaveInspectCard",flags).Invoke(ui,null);}
System.Collections.IEnumerator Run(){
 foreach(var size in new[]{new Vector2Int(1280,720),new Vector2Int(1920,1080),new Vector2Int(1024,768)}){
  Resize(size.x,size.y);yield return Wait(.2f);
  foreach(string language in new[]{"zh-CN","en-US","ja-JP"}){
   GamePreferences.SetLanguage(language);yield return null;while(!GamePreferences.Ready)yield return null;
   foreach(float scale in new[]{.85f,1f,1.3f}){
    GamePreferences.SetScale(scale);var v=Sample();var e=v.stackItems[0];e.rulesText=string.Join("\n",Enumerable.Range(0,120).Select(i=>"Clause "+i+" remains complete. END_MARKER_"+i));e.description="";e.hasXValue=true;e.xValue=17;e.castingChoices=Array.Empty<ForgeCastingChoice>();v.stackItems=new[]{e};v.stackCount=1;v.stack=new[]{e.description};int submissions=0;
    ui.Present(null,1,false,null,null,"");yield return null;ui.Present(v,1,true,_=>submissions++,null,"");Quiet();yield return Wait(.2f);string context=size+"/"+language+"/"+scale;
    var row=Root().Find("StackColumn/Viewport/Content/StackEntry_0");var body=row.Find("CompactEffect").GetComponent<UnityEngine.UI.ScrollRect>();var source=body.content.Find("CompactNote").GetComponent<UnityEngine.UI.Text>();var text=source.transform.Find("Symbols").GetComponent<TMPro.TextMeshProUGUI>();
    Check(row.Find("EffectToggle")==null||!row.Find("EffectToggle").gameObject.activeSelf,context+" no expansion control");
    Check(source.text.Contains("END_MARKER_119")&&source.text.Contains("X = 17"),context+" complete body and X");
    Check(body.enabled&&body.viewport.rect.height<=280.1f&&body.verticalScrollbar.gameObject.activeInHierarchy,context+" immediate bounded scroll");
    var pointer=new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current){position=Bounds(body.viewport).center,scrollDelta=new Vector2(0,-3)};var hits=new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();UnityEngine.EventSystems.EventSystem.current.RaycastAll(pointer,hits);
    bool scrollHit=hits.Count>0&&hits[0].gameObject.GetComponentInParent<UnityEngine.UI.ScrollRect>()==body;Check(scrollHit,context+" effect wheel input reaches inner scroll");if(scrollHit)UnityEngine.EventSystems.ExecuteEvents.ExecuteHierarchy(hits[0].gameObject,pointer,UnityEngine.EventSystems.ExecuteEvents.scrollHandler);yield return Wait(.1f);Check(body.content.anchoredPosition.y>1&&submissions==0,context+" wheel moves text without game action");
    body.verticalNormalizedPosition=0;yield return Wait(.1f);text.ForceMeshUpdate();var last=text.textInfo.characterInfo.Take(text.textInfo.characterCount).Last(c=>c.isVisible);Check(Bounds(body.viewport).Contains(text.transform.TransformPoint((last.bottomLeft+last.topRight)*.5f)),context+" actual final glyph visible at bottom");
    float offset=body.content.anchoredPosition.y;ui.Present(v,1,true,_=>submissions++,null,"");Quiet();yield return Wait(.1f);Check(Mathf.Abs(body.content.anchoredPosition.y-offset)<2,context+" snapshot preserves scroll position");
    e.rulesText="SHORT_CURRENT_ABILITY_01.";e.kind="ability";e.hasXValue=false;e.source.originalRules=e.source.currentRules="SHORT_CURRENT_ABILITY_01.\nOTHER_UNRELATED_ABILITY_02.";e.rulesSource=null;
    ui.Present(v,1,true,_=>submissions++,null,"");Quiet();yield return Wait(.1f);
    Check(source.text.Contains("SHORT_CURRENT_ABILITY_01")&&!source.text.Contains("OTHER_UNRELATED_ABILITY_02"),context+" current ability only");
    Check(!body.enabled&&!body.verticalScrollbar.gameObject.activeSelf&&body.content.anchoredPosition.y==0,context+" short ability fits naturally and clears old scroll");
    Check(Mathf.Abs(body.viewport.rect.height-body.content.rect.height)<1.1f,context+" short effect has no empty reserved height");
   }
   Save("RUNNING");
  }
 }
 GamePreferences.SetLanguage("en-US");yield return null;while(!GamePreferences.Ready)yield return null;GamePreferences.SetScale(1);
 foreach(string kind in new[]{"ability","trigger"}){
  var e=Raw(kind);string value=Effect(e);Check(value.Contains("FIRST_EFFECT_11")&&value.Contains("THIRD_EFFECT_33")&&!value.Contains("UNRELATED_KEYWORD_99"),kind+" complete current ability excludes other printed abilities");
  e.rulesText="";value=Effect(e);Check(value.Contains("LIVE_CONTEXT_44")&&!value.Contains("UNRELATED_KEYWORD_99"),kind+" old record uses current description rather than entire source card");
 }
 var sample=Sample();ui.Present(null,1,false,null,null,"");yield return null;ui.Present(sample,1,false,null,null,"");Quiet();yield return Wait(.2f);
 var first=Root().Find("StackColumn/Viewport/Content/StackEntry_0");var second=Root().Find("StackColumn/Viewport/Content/StackEntry_1");
 Check(first.Find("CompactTargets").GetComponent<UnityEngine.UI.Text>().text.Contains("2"),"target keeps destination stack number");Check(second.GetComponentsInChildren<UnityEngine.UI.Text>().Any(t=>t.name.StartsWith("CompactIncoming")&&t.text.Contains("1")),"incoming target keeps concrete source and stack number");
 Check(first.GetComponent<UnityEngine.UI.Image>().color!=second.GetComponent<UnityEngine.UI.Image>().color,"enemy and own backgrounds stay distinct");
 var legacy=Sample();legacy.stackItems=Array.Empty<ForgeStackItem>();legacy.stackCount=1;legacy.stack=new[]{string.Join("\n",Enumerable.Range(0,90).Select(i=>"Legacy sentence "+i+". LEGACY_END_"+i))};ui.Present(legacy,1,false,null,null,"");Quiet();yield return Wait(.2f);var oldRow=Root().Find("StackColumn/Viewport/Content/StackEntry_0");var oldBody=oldRow.Find("CompactEffect").GetComponent<UnityEngine.UI.ScrollRect>();Check(oldBody.enabled&&oldBody.content.GetComponentInChildren<UnityEngine.UI.Text>(true).text.Contains("LEGACY_END_89"),"legacy text-only record immediately scrolls complete description");
 var many=Sample();many.stackItems=Enumerable.Range(0,12).Select(i=>{var e=Raw(i%2==0?"ability":"trigger");e.id=98000+i;e.controllerSeat=i%2+1;e.description="";return e;}).ToArray();many.stackCount=12;many.stack=many.stackItems.Select(e=>e.description).ToArray();ui.Present(many,1,false,null,null,"");Quiet();yield return Wait(.2f);var outer=Root().Find("StackColumn/Viewport").GetComponent<UnityEngine.UI.ScrollRect>();Check(outer.verticalScrollbar.gameObject.activeInHierarchy&&outer.content.rect.height>outer.viewport.rect.height,"many complete entries retain outer scroll");outer.verticalNormalizedPosition=0;yield return Wait(.1f);Check(outer.content.anchoredPosition.y>1,"outer list can reach last entries");
 Restore();yield return null;while(!GamePreferences.Ready)yield return null;ui.Present(null,1,false,null,null,"");yield return null;ui.Present(Sample(),1,false,null,null,"副本 · 只读布局预览");Save("COMPLETE");
}
System.Collections.IEnumerator Guard(){var it=Run();for(int i=0;i<100000;i++){bool next=false;object value=null;try{next=it.MoveNext();if(next)value=it.Current;}catch(Exception e){failed++;results.Add(new{exception=e.ToString()});Restore();Save("ERROR");yield break;}if(!next)break;yield return value;}}
Save("STARTING");ui.StartCoroutine(Guard());return "Direct full current-effect UI validation started";
