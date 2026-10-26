using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class MainMenuController
{
    private Canvas localAiSetupPage;
    private void OpenLocalPlayPage()
    {
        if(AwaitCardData(OpenLocalPlayPage)||localAiSetupPage!=null)return;
        var decks=DeckRepository.LoadAll().ToArray();
        var page=localAiSetupPage=ReplayWidgets.Canvas("LocalAiSetup",550);
        var background=ReplayWidgets.Panel(page.transform,"Background",0,0,1920,1080,MenuVisualTheme.Ink);
        background.anchorMin=Vector2.zero;background.anchorMax=Vector2.one;background.sizeDelta=Vector2.zero;
        var art=ReplayWidgets.Rect(background,"Artwork",0,0,1920,1080).gameObject.AddComponent<RawImage>();
        art.texture=Resources.Load<Texture2D>("Menu/ArchiveSanctum");art.color=new Color(.35f,.43f,.57f,.5f);art.raycastTarget=false;
        var fit=art.gameObject.AddComponent<AspectRatioFitter>();fit.aspectMode=AspectRatioFitter.AspectMode.EnvelopeParent;
        fit.aspectRatio=art.texture!=null?(float)art.texture.width/art.texture.height:16f/9;
        SetScreenVisible(startScreenGroup,false);SetScreenVisible(deckBuilderGroup,false);
        var root=ReplayWidgets.Rect(page.transform,"LocalPlayPage",0,0,1560,940);
        ReplayWidgets.Label(root,AiText("本地对战","Local play","ローカル対戦"),-420,428,700,80,38);
        var subtitle=ReplayWidgets.Label(root,AiText("选择赛制、卡组与本局规则","Choose decks and rules for this game","フォーマット、デッキと対戦ルールを設定"),-420,365,700,42,21);
        subtitle.color=MenuVisualTheme.Muted;
        void Back(){Destroy(page.gameObject);localAiSetupPage=null;ShowStartScreenNow();}
        var back=ReplayWidgets.Button(root,AiText("返回主菜单","Main menu","メインメニュー"),650,420,220,Back);back.name="LocalPlayBack";
        RectTransform Section(string name,float x,string title){
            var panel=ReplayWidgets.Panel(root,name,x,20,730,620,new Color(.055f,.075f,.105f,.96f));
            panel.GetComponent<Image>().sprite=IntegratedUiUtility.RoundedRect(730,620,10);
            ReplayWidgets.Label(panel,title,0,260,650,52,27);return panel;
        }
        var rules=Section("Rules",-395,AiText("对局规则","Game rules","対戦ルール"));
        var players=Section("Decks",395,AiText("双方卡组","Players and decks","対戦デッキ"));
        Dropdown Pick(Transform parent,string name,float x,float y,float width,string[] options){
            var d=Instantiate(deckDropdown,parent);d.name=name;d.onValueChanged=new Dropdown.DropdownEvent();
            var r=(RectTransform)d.transform;r.anchorMin=r.anchorMax=r.pivot=new Vector2(.5f,.5f);r.anchoredPosition=new Vector2(x,y);r.sizeDelta=new Vector2(width,58);
            d.ClearOptions();d.AddOptions(options.ToList());d.RefreshShownValue();return d;
        }
        void RuleLabel(string value,float y)=>ReplayWidgets.Label(rules,value,-220,y,210,58,22);
        RuleLabel(AiText("赛制","Format","フォーマット"),170);
        var formats=DeckFormatLegality.OnlineKeys;
        var format=Pick(rules,"LocalFormat",105,170,400,formats.Select(f=>f=="none"?AiText("不限赛制","Unrestricted","制限なし"):GamePreferences.T(DeckFormatLegality.FormatName(f))).ToArray());
        format.SetValueWithoutNotify(Mathf.Max(0,Array.IndexOf(formats,currentDeck?.format)));format.RefreshShownValue();
        RuleLabel(AiText("初始生命","Starting life","初期ライフ"),70);
        var life=ReplayWidgets.Input(rules,formats[format.value]=="brawl"?"25":"20",105,70,400);life.name="LocalStartingLife";
        life.contentType=InputField.ContentType.IntegerNumber;life.characterLimit=3;
        RuleLabel(AiText("先手","Play first","先攻"),-30);
        var first=Pick(rules,"LocalFirstPlayer",105,-30,400,new[]{AiText("随机","Random","ランダム"),AiText("你先手","You play first","自分が先攻"),AiText("AI 先手","AI plays first","AIが先攻")});
        RuleLabel(AiText("洗牌","Shuffling","シャッフル"),-130);
        var shuffle=Pick(rules,"LocalShuffle",105,-130,400,new[]{AiText("正常洗牌","Shuffle normally","通常のシャッフル"),AiText("整局不洗切卡组","Keep saved order all game","対戦中はシャッフルしない")});
        var hint=ReplayWidgets.Label(rules,AiText("初始生命对双方生效，可设为1～200。\n不洗切时，开局、调度和卡牌效果均保留顺序。","Starting life applies to both players (1–200).\nSaved order disables opening, mulligan and effect shuffles.","初期ライフは両プレイヤーに適用（1～200）。\n順序固定では開始時、マリガン、効果でも切り直しません。"),0,-240,650,86,18);hint.color=MenuVisualTheme.Muted;
        var options=decks.Length==0?new[]{AiText("没有已保存卡组","No saved decks","保存済みデッキなし")}:decks.Select(d=>d.name).ToArray();
        ReplayWidgets.Label(players,AiText("你的卡组","Your deck","自分のデッキ"),0,180,650,44,22);
        var own=Pick(players,"LocalPlayerDeck",0,120,650,options);
        var ownInfo=ReplayWidgets.Label(players,"",0,64,650,42,18);ownInfo.color=MenuVisualTheme.Muted;
        ReplayWidgets.Label(players,AiText("AI 卡组","AI deck","AIのデッキ"),0,-12,650,44,22);
        var other=Pick(players,"LocalAiDeck",0,-72,650,options);
        var otherInfo=ReplayWidgets.Label(players,"",0,-128,650,42,18);otherInfo.color=MenuVisualTheme.Muted;
        var deckHint=ReplayWidgets.Label(players,AiText("按左侧选定的赛制检查双方卡组。\n保留已保存卡组的原有内容和顺序。","Both decks are checked against the selected format.\nSaved deck contents and order stay unchanged.","両デッキを選択したフォーマットで検証します。\n保存済みデッキの内容と順序は変更しません。"),0,-240,650,86,18);deckHint.color=MenuVisualTheme.Muted;
        int selected=Array.FindIndex(decks,d=>d.name==currentDeck?.name);own.SetValueWithoutNotify(Mathf.Max(0,selected));
        other.SetValueWithoutNotify(decks.Length>1?(own.value+1)%decks.Length:0);own.RefreshShownValue();other.RefreshShownValue();
        var status=ReplayWidgets.Label(root,"",-200,-370,1080,100,20);status.name="LocalPlayStatus";
        var begin=ReplayWidgets.Button(root,AiText("开始对局","Start match","対戦開始"),585,-380,320,()=>{
            try{
                var config=new OnlineRoomConfig{formatId=formats[format.value],startingLife=int.Parse(life.text),firstPlayerSeat=first.value,noShuffle=shuffle.value==1};
                OnlineMatchService.EnsureInstance().StartLocalAiMatch(decks[own.value],decks[other.value],settings:config);
                Destroy(page.gameObject);localAiSetupPage=null;
            }catch(Exception error){status.text=error.Message;}
        },true);begin.name="LocalPlayStart";
        ((RectTransform)begin.transform).sizeDelta=new Vector2(320,64);
        string Summary(DeckDefinition d)=>AiText("主牌 ","Main ","メイン ")+d.cards.Sum(c=>c.count)+" · "+AiText("备牌 ","Sideboard ","サイド ")+d.sideboard.Sum(c=>c.count)+" · "+GamePreferences.T(DeckFormatLegality.FormatName(d.format));
        void Refresh(){
            bool valid=int.TryParse(life.text,out int value)&&value>=1&&value<=200;
            begin.interactable=decks.Length>0&&valid;own.interactable=other.interactable=decks.Length>0;
            status.text=decks.Length==0?AiText("请先在卡组编辑中保存卡组。","Save a deck in the deck editor first.","先にデッキ編集で保存してください。"):!valid?AiText("请输入1～200的初始生命。","Enter starting life from 1 to 200.","初期ライフを1～200で入力してください。"):"";
            if(decks.Length>0){ownInfo.text=Summary(decks[own.value]);otherInfo.text=Summary(decks[other.value]);}
        }
        own.onValueChanged.AddListener(_=>Refresh());other.onValueChanged.AddListener(_=>Refresh());life.onValueChanged.AddListener(_=>Refresh());
        first.onValueChanged.AddListener(_=>Refresh());shuffle.onValueChanged.AddListener(_=>Refresh());
        format.onValueChanged.AddListener(_=>{life.text=formats[format.value]=="brawl"?"25":"20";Refresh();});
        Refresh();
    }
}
