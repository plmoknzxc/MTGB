using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class ForgeBattlefieldController
{
    private static bool IsSpeedMarker(ForgeCardView card)=>card.effectMarker&&card.markerKind=="effect"&&card.helperArtName is "Start Your Engines!" or "Max Speed" or "Max Speed!";
    private static string SpeedMarkerValue(ForgeCardView card)
    {
        if(!IsSpeedMarker(card))return "";
        var match=System.Text.RegularExpressions.Regex.Match(card.markerStatus??"",@"^SPEED: ([1-4]|MAX!)$");
        return match.Success?match.Groups[1].Value=="MAX!"?"4":match.Groups[1].Value:"";
    }
    private static bool IsPublicRuleHelper(ForgeCardView card)=>card!=null&&!card.faceDown&&card.effectMarker&&card.markerKind=="effect"&&card.helperArtName is "The Monarch" or "The Initiative" or "The Ring";
    private static string MarkerName(ForgeCardView card)=>IsSprocketMarker(card)?TapPaymentText("装置齿轮组","Contraption sprockets","からくり列"):RegenerationSourceName(card)??(card.effectSourceName=="Dragonborn Immolator"&&(card.name??"").EndsWith("'s Boon",StringComparison.Ordinal)?string.Format(GamePreferences.T("{0}'s boon"),GamePreferences.CardFaceName(card.effectSourceName)):card.markerKind=="player-protection"?TapPaymentText("玩家保护","Player protection","プレイヤーのプロテクション"):IsPublicRuleHelper(card)
        ?card.helperArtName=="The Ring"?TapPaymentText("魔戒","The Ring","指輪"):card.helperArtName=="The Monarch"?TapPaymentText("君主","The Monarch","統治者"):DeckKeywordGlossary.All.FirstOrDefault(e=>e.English==card.helperArtName)?.Name??card.name
        :card.markerKind=="game"?card.helperArtName switch {
        "Day"=>TapPaymentText("白昼","Day","昼"),"Night"=>TapPaymentText("黑夜","Night","夜"),_=>card.name
    }:IsSpeedMarker(card)?card.helperArtName is "Max Speed" or "Max Speed!"?TapPaymentText("速度极限！","Max Speed!","最高速度！"):TapPaymentText("发动引擎！","Start Your Engines!","エンジン始動！"):card.name);
    private static string MarkerRules(ForgeCardView card)
    {
        if(IsSprocketMarker(card))return card.markerStatus+"\n\n"+(card.currentRules=="At the beginning of your upkeep, if you control a Contraption, move the CRANK! counter to the next sprocket and crank any number of that sprocket's Contraptions."?SprocketRules():card.currentRules);
        if(card.markerKind=="player-protection")return string.Join("\n",(card.currentRules??"").Split('\n').Select(line=>{
            string[] names={"white","blue","black","red","green"},zh={"反白","反蓝","反黑","反红","反绿"},ja={"白","青","黒","赤","緑"};
            int index=Array.FindIndex(names,n=>line=="Protection from "+n);
            return index<0?line:TapPaymentText("你具有"+zh[index]+"保护。","You have protection from "+names[index]+".","あなたはプロテクション（"+ja[index]+"）を持つ。");
        }));
        if(card.markerKind=="game"&&card.helperArtName is "Day" or "Night"&&card.currentRules==(card.helperArtName=="Day"?"当前为白昼。":"当前为黑夜。"))
            return card.helperArtName=="Day"?TapPaymentText("当前为白昼。","It is currently day.","現在は昼です。"):TapPaymentText("当前为黑夜。","It is currently night.","現在は夜です。");
        string rules=GeneratedPublicRuleText(card.currentRules??""),status=card.markerStatus??"",speed=SpeedMarkerValue(card);
        rules=GeneratedDepartureRules(card,rules)??rules;
        if(RegenerationSourceName(card)!=null&&rules=="Regeneration (if creature would be destroyed, regenerate it instead)")
            rules=GamePreferences.T(rules);
        // Translate the captured-number line separately: a format placeholder
        // must not swallow an unrelated rules paragraph as part of its value.
        if(System.Text.RegularExpressions.Regex.IsMatch(status,@"^Noted number: -?\d+$"))
            status=GamePreferences.NativeText(status);
        if(card.effectSourceName=="Dragonborn Immolator" && rules=="When you cast your next creature spell, it perpetually gets +X/+0, where X is the noted number.")
            rules=GamePreferences.T(rules);
        if(card.effectSourceName=="A-Maelstrom Muse" && rules.StartsWith("The next instant or sorcery spell you cast this turn costs {X} less to cast, where X is ",StringComparison.Ordinal)
            &&rules.EndsWith("'s power at the time A-Maelstrom Muse's ability resolved.",StringComparison.Ordinal))
            rules=GamePreferences.T("The next instant or sorcery spell you cast this turn costs {X} less to cast, where X is the noted number.");
        if(card.effectMarker&&!card.faceDown&&card.genericEffectName&&card.effectSourceName=="Teyo, Geometric Tactician"
            &&rules=="Each player may attack only the nearest opponent in the chosen direction and planeswalkers controller by that opponent.")
            rules=TapPaymentText("每位牌手只能攻击所选方向上最近的对手，以及由该对手操控的鹏洛客。",rules,"各プレイヤーは選ばれた方向の最も近い対戦相手と、その対戦相手がコントロールするプレインズウォーカーしか攻撃できない。");
        if(IsPublicRuleHelper(card)) {
            // Translate only exact known native paragraphs; changed or additional
            // conditions keep their original meaning and remain visible.
            rules=rules.Replace("\r\n","\n");
            rules=string.Join("\n\n",rules.Split(new[]{"\n\n"},StringSplitOptions.None).Select(paragraph=>paragraph switch {
                "At the beginning of your end step, draw a card." => TapPaymentText("在你的结束步骤开始时，抓一张牌。","At the beginning of your end step, draw a card.","あなたの終了ステップの開始時に、カード１枚を引く。"),
                "Whenever a creature deals combat damage to you, its controller becomes the monarch." => TapPaymentText("每当一个生物对你造成战斗伤害时，其操控者成为君主。","Whenever a creature deals combat damage to you, its controller becomes the monarch.","クリーチャー１体があなたに戦闘ダメージを与えるたび、それのコントローラーが統治者になる。"),
                "Whenever one or more creatures a player controls deal combat damage to you, that player takes the initiative." => TapPaymentText("每当由一位牌手操控的一个或数个生物对你造成战斗伤害时，该牌手掌握先制。","Whenever one or more creatures a player controls deal combat damage to you, that player takes the initiative.","プレイヤー１人がコントロールしているクリーチャー１体以上があなたに戦闘ダメージを与えるたび、そのプレイヤーがイニシアチブを得る。"),
                "Whenever you take the initiative and at the beginning of your upkeep, venture into Undercity. (If you're in a dungeon, advance to the next room. If not, enter Undercity. You can take the initiative even if you already have it.)" => TapPaymentText("每当你掌握先制时，以及在你的维持开始时，深入地底城。（若你已身处地城，则前进至下一个房间。否则，进入地底城。即使你已握有先制，仍能掌握先制。）","Whenever you take the initiative and at the beginning of your upkeep, venture into Undercity. (If you're in a dungeon, advance to the next room. If not, enter Undercity. You can take the initiative even if you already have it.)","あなたがイニシアチブを得るたび、およびあなたのアップキープの開始時に、地下街探索をする。（ダンジョンにいるなら次の部屋に進む。そうでないなら地下街に入る。すでにイニシアチブを持っていても、イニシアチブを得ることができる。）"),
                "Your Ring-bearer is legendary." => TapPaymentText("你的持戒者是传奇。","Your Ring-bearer is legendary.","あなたの指輪所持者は伝説である。"),
                "Your Ring-bearer can't be blocked by creatures with greater power." => TapPaymentText("力量大于你的持戒者之生物不能阻挡它。","Your Ring-bearer can't be blocked by creatures with greater power.","あなたの指輪所持者は、それより大きいパワーを持つクリーチャーにはブロックされない。"),
                "Whenever your ring-bearer attacks, draw a card, then discard a card." => TapPaymentText("每当你的持戒者攻击时，抓一张牌，然后弃一张牌。","Whenever your ring-bearer attacks, draw a card, then discard a card.","あなたの指輪所持者が攻撃するたび、カード１枚を引き、その後カード１枚を捨てる。"),
                "Whenever your Ring-bearer becomes blocked a creature, that creature's controller sacrifices it at the end of combat." => TapPaymentText("每当你的持戒者被一个生物阻挡时，该生物的操控者在战斗结束时牺牲它。","Whenever your Ring-bearer becomes blocked a creature, that creature's controller sacrifices it at the end of combat.","あなたの指輪所持者がクリーチャー１体にブロックされるたび、そのクリーチャーのコントローラーは戦闘終了時にそれを生け贄に捧げる。"),
                "Whenever your Ring-bearer deals combat damage to a player, each opponent loses 3 life." => TapPaymentText("每当你的持戒者对一位牌手造成战斗伤害时，每位对手失去3点生命。","Whenever your Ring-bearer deals combat damage to a player, each opponent loses 3 life.","あなたの指輪所持者がプレイヤー１人に戦闘ダメージを与えるたび、各対戦相手は３点のライフを失う。"),
                "At end of combat, the controller of the creature that blocked The Ring sacrifices that creature." => TapPaymentText("在战斗结束时，曾阻挡持戒者的生物之操控者牺牲该生物。","At end of combat, the controller of the creature that blocked The Ring sacrifices that creature.","戦闘終了時に、指輪所持者をブロックしたクリーチャーのコントローラーはそのクリーチャーを生け贄に捧げる。"),
                _=>paragraph
            }));
        }
        if(IsPublicRuleHelper(card)&&card.helperArtName=="The Ring") {
            var count=System.Text.RegularExpressions.Regex.Match(status,@"^魔戒已引诱你 (\d+) 次$");
            if(count.Success)status=TapPaymentText("魔戒已引诱你 ","The Ring has tempted you ","指輪の誘惑：")+count.Groups[1].Value+TapPaymentText(" 次"," times","回");
        }
        if(speed.Length>0) {
            status=TapPaymentText("速度：","Speed: ","速度：")+speed;
            if(rules=="Whenever one or more opponents lose life during your turn, if your speed is less than 4, your speed increases by 1. This ability triggers only once each turn.")
                rules=TapPaymentText("于你的回合中，每当一个或数个对手失去生命时，若你的速度小于4，则速度增加1。此异能每回合只会触发一次。",rules,"あなたのターン中に対戦相手１人以上がライフを失うたび、あなたの速度が４未満なら、あなたの速度は１増加する。この能力は毎ターン１回しか誘発しない。");
        }
        return status.Length==0?rules:rules.Length==0?status:status+"\n\n"+rules;
    }
    private static Sprite emblemShape;
    private static Sprite EmblemShape()
    {
        if(emblemShape!=null)return emblemShape;
        const int w=160,h=110;
        var texture=new Texture2D(w,h,TextureFormat.RGBA32,false){hideFlags=HideFlags.HideAndDontSave,filterMode=FilterMode.Bilinear};
        var pixels=new Color[w*h];
        for(int y=0;y<h;y++)for(int x=0;x<w;x++) {
            float edge=23*Mathf.Abs((x+.5f-w*.5f)/(w*.5f));
            pixels[y*w+x]=new Color(1,1,1,Mathf.Clamp01(y-edge)*Mathf.Clamp01(Mathf.Min(x,w-1-x)));
        }
        texture.SetPixels(pixels);texture.Apply(false,true);
        emblemShape=Sprite.Create(texture,new Rect(0,0,w,h),new Vector2(.5f,.5f));
        emblemShape.hideFlags=HideFlags.HideAndDontSave;return emblemShape;
    }
    // Warp's turn-end trigger is separate from its later exile-casting
    // permission. The permission is already represented by the exiled card.
    private static bool IsExiledCardCastPermission(ForgeCardView card)
        => card?.effectMarker==true && card.markerKind=="effect"
            && (card.name??"").StartsWith("Warped ",StringComparison.Ordinal)
            && (card.currentRules??"").Trim()=="You may cast the card.";
    private static IEnumerable<ForgeCardView> VisibleEffectMarkers(ForgePlayerView player)
        => (player.effectMarkers??Array.Empty<ForgeCardView>()).Where(c=>!IsExiledCardCastPermission(c));

    private void PresentEmblems(ForgePlayerView player,bool own)
    {
        var markers=VisibleEffectMarkers(player).Concat(own?view.globalEffects??Array.Empty<ForgeCardView>():Array.Empty<ForgeCardView>()).ToArray();
        bool State(ForgeCardView card)=>card.markerKind!="delayed"&&!string.IsNullOrEmpty(card.helperArtName);
        float manaReserve = IsRedesignLayout ? RedesignPlayerManaReserve : 0;
        PresentMarkerLane(player,own,"States",markers.Where(State).ToArray(),(own?1:-1)*(200+manaReserve));
        PresentMarkerLane(player,own,"Emblems",(player.emblems??Array.Empty<ForgeCardView>()).Concat(markers.Where(c=>!State(c))).ToArray(),(own?1:-1)*(314+manaReserve));
    }
    private void PresentMarkerLane(ForgePlayerView player,bool own,string lane,ForgeCardView[] markers,float y)
    {
        const int capacity=3;
        string GroupKey(ForgeCardView c)=>c.markerKind=="delayed"?"delayed:"+c.ownerSeat+":"+c.printingKey+":"+c.backFace+":"+c.name+":"+(string.IsNullOrEmpty(c.markerGroupKey)?ArenaGroupKey(c):c.markerGroupKey):c.emblem?"emblem:"+c.ownerSeat+":"+c.printingKey+":"+c.name+":"+c.currentType+":"+c.currentRules:"id:"+c.id;
        var groups=markers.GroupBy(GroupKey).SelectMany(g=>expandedGroups.Contains("markers:"+player.seat+":"+lane+":"+g.Key)?g.Select(c=>new[]{c}):new[]{g.ToArray()}).ToArray();
        string pageKey="markers:"+player.seat+":"+lane;
        int maxPage=Math.Max(0,(groups.Length-1)/capacity);
        int page=pages.TryGetValue(pageKey,out int saved)?Math.Min(saved,maxPage):0;
        pages[pageKey]=page;
        var plate=(RectTransform)Node(own?"PlayerPlate":"OpponentPlate");
        bool stateLane=lane=="States";
        float spacing=stateLane?106:86;
        for(int i=page*capacity;i<Math.Min(groups.Length,(page+1)*capacity);i++) {
            var card=groups[i][0];
            var position=cardsRoot.InverseTransformPoint(plate.TransformPoint(new Vector3(-spacing+(i%capacity)*spacing,y,0)));
            // Reuse the existing image, shape, hover and native object identity.
            DrawArenaCard(cardsRoot,card,groups[i].Length,(own?"Your":"Opponent")+"Emblem:"+card.id,"markers:"+player.seat+":"+lane+":"+GroupKey(card),new Vector2(position.x,position.y),new Vector2(stateLane?96:76,stateLane?126:card.markerKind=="delayed"?68:82),false,0,i);
        }
        foreach(int direction in new[]{-1,1}) {
            string name=lane+(direction<0?"Previous":"Next");
            var button=plate.Find(name)?.GetComponent<Button>();
            if(button==null)button=StackIcon(plate,name,direction<0?StackControlIcon.Kind.Left:StackControlIcon.Kind.Right,()=>{});
            button.gameObject.SetActive(maxPage>0);
            var rect=(RectTransform)button.transform;rect.anchoredPosition=new Vector2(direction*(stateLane?174:150),y);rect.sizeDelta=new Vector2(30,30);
            ((RectTransform)rect.Find("Icon")).sizeDelta=new Vector2(14,14);
            button.interactable=direction<0?page>0:page<maxPage;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(()=>{pages[pageKey]=Mathf.Clamp(page+direction,0,maxPage);PresentIntegrated(connectionText.text);});
        }
    }
    private static Sprite effectShape;
    private static string GeneratedDepartureRules(ForgeCardView source,string rules)
    {
        if(source==null||source.faceDown||string.IsNullOrEmpty(source.name))return null;
        if(NativeMessageText.TryTranslate(rules,out var generated))return generated;
        string name=GamePreferences.CardFaceName(source.name);
        if(rules=="Return exiled permanent to the battlefield.")
            return TapPaymentText("将被放逐的永久物移回战场。",rules,"追放されているパーマネントを戦場に戻す。");
        if(rules=="At the beginning of the next cleanup step, sacrifice "+source.name+".")
            return TapPaymentText("在下一个清理步骤开始时，牺牲"+name+"。",rules,"次のクリンナップ・ステップの開始時に、"+name+"を生け贄に捧げる。");
        if(rules=="At end of combat, sacrifice "+source.name+".")
            return TapPaymentText("在战斗结束时，牺牲"+name+"。",rules,"戦闘終了時に、"+name+"を生け贄に捧げる。");
        if((source.keywords??Array.Empty<string>()).Contains("DECAYED")
            &&rules=="When a creature with decayed attacks, sacrifice it at end of combat.")
            return TapPaymentText("当具有腐朽异能的生物攻击时，在战斗结束时牺牲它。",rules,"腐乱を持つクリーチャーが攻撃したとき、戦闘終了時にそれを生け贄に捧げる。");
        return null;
    }
    public static string PendingDepartureText(ForgePendingDeparture instruction)
    {
        // Conditions outside this bounded translation retain the exact engine text.
        if(!string.IsNullOrEmpty(instruction.condition))return instruction.description??"";
        string phase=System.Text.RegularExpressions.Regex.Replace(instruction.phase??"",@"\s+","").Trim().ToLowerInvariant();
        string when=phase switch {
            "endofturn"=>instruction.yourTurn?TapPaymentText("在该效果操控者的下一个结束步骤开始时，","At the beginning of that effect controller's next end step, ","その効果のコントローラーの次の終了ステップの開始時に、"):TapPaymentText("在下一个结束步骤开始时，","At the beginning of the next end step, ","次の終了ステップの開始時に、"),
            "endcombat"=>TapPaymentText("在战斗结束时，","At end of combat, ","戦闘終了時に、"),
            "cleanup"=>TapPaymentText("在下一个清理步骤开始时，","At the beginning of the next cleanup step, ","次のクリンナップ・ステップの開始時に、"),
            "upkeep"=>instruction.yourTurn?TapPaymentText("在该效果操控者的下一个维持开始时，","At the beginning of that effect controller's next upkeep, ","その効果のコントローラーの次のアップキープの開始時に、"):TapPaymentText("在下一个维持开始时，","At the beginning of the next upkeep, ","次のアップキープの開始時に、"),
            _=>""
        };
        if(when.Length==0)return instruction.description??"";
        string action=instruction.action switch {
            "sacrifice"=>TapPaymentText("牺牲此永久物。","sacrifice this permanent.","このパーマネントを生け贄に捧げる。"),
            "exile"=>TapPaymentText("放逐此永久物。","exile this permanent.","このパーマネントを追放する。"),
            "destroy"=>TapPaymentText("消灭此永久物。","destroy this permanent.","このパーマネントを破壊する。"),
            _=>""
        };
        return action.Length==0?instruction.description??"":when+action;
    }
    private static Sprite EffectMarkerShape()
    {
        if(effectShape!=null)return effectShape;
        var texture=new Texture2D(160,110,TextureFormat.RGBA32,false){hideFlags=HideFlags.HideAndDontSave,filterMode=FilterMode.Bilinear};
        var pixels=new Color[160*110];
        for(int y=0;y<110;y++)for(int x=0;x<160;x++) {
            float inset=Mathf.Max(0,14-Mathf.Min(y,109-y));
            pixels[y*160+x]=new Color(1,1,1,Mathf.Clamp01(Mathf.Min(x,159-x)-inset));
        }
        texture.SetPixels(pixels);texture.Apply(false,true);
        effectShape=Sprite.Create(texture,new Rect(0,0,160,110),new Vector2(.5f,.5f));
        effectShape.hideFlags=HideFlags.HideAndDontSave;return effectShape;
    }
    private static void PresentFaceDownFace(RectTransform face,Sprite sprite,Vector2 size)
    {
        var image=face.GetComponent<Image>();image.sprite=IntegratedUiUtility.RoundedRect(140,125,7);image.color=Ink;
        face.gameObject.AddComponent<Mask>().showMaskGraphic=true;
        var art=Rect(face,"FaceDownBack",Vector2.zero,size).gameObject.AddComponent<RawImage>();
        art.texture=sprite.texture;art.raycastTarget=false;
        var r=sprite.rect;float height=r.width*size.y/size.x;
        art.uvRect=new Rect(r.x/sprite.texture.width,(r.y+(r.height-height)*.5f)/sprite.texture.height,r.width/sprite.texture.width,height/sprite.texture.height);
    }
    private IEnumerator AnimateFaceUp(RectTransform back,RectTransform front)
    {
        const float duration=.32f;
        front.localScale=new Vector3(0,1,1);
        for(float t=0;t<duration;t+=Time.unscaledDeltaTime) {
            if(back==null || front==null)break;
            float progress=t/duration;
            back.localScale=new Vector3(Mathf.Max(0,1-progress*2),1,1);
            front.localScale=new Vector3(Mathf.Max(0,progress*2-1),1,1);
            yield return null;
        }
        if(back!=null)Destroy(back.gameObject);
        if(front!=null)front.localScale=Vector3.one;
    }
}
