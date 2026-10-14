using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class ForgeBattlefieldController
{
    private RectTransform manaPoolRow,publicManaPoolRow;
    private static Font poolGlyphFont;
    private static readonly string[] PoolGlyphs={"\uE600","\uE601","\uE602","\uE603","\uE604","\uE904"};
    private static readonly string[] PoolSymbols={"W","U","B","R","G","C"};
    private static readonly Color[] PoolColors={new Color(.86f,.8f,.55f),new Color(.24f,.6f,.96f),new Color(.65f,.49f,.8f),new Color(.98f,.4f,.24f),new Color(.3f,.8f,.45f),new Color(.7f,.77f,.82f)};
    private sealed class PoolCell { public RectTransform root;public Text count;public Button button;public int amount;public float changed=-10; }
    private readonly Dictionary<string,PoolCell> poolCells=new Dictionary<string,PoolCell>();
    private readonly Dictionary<string,PoolCell> publicPoolCells=new Dictionary<string,PoolCell>();
    private IEnumerable<Button> manaPoolButtons=>poolCells.Values.Select(c=>c.button);
    private int manaOwner;
    private static ForgeManaPoolEntry[] PoolEntries(ForgePlayerView player)
    {
        if(player?.manaPoolEntries!=null)return player.manaPoolEntries.Where(e=>e.count>0).ToArray();
        // Totals and snow subsets keep older recordings readable. Live snapshots
        // supply exact native buckets and the corresponding payment option IDs.
        var totals=player?.manaPool?.Length==6?player.manaPool:new[]{0,0,0,player?.redMana??0,0,0};
        var entries=new List<ForgeManaPoolEntry>();
        for(int i=0;i<12;i++) {
            int color=i%6,snow=player?.snowManaPool?.Length==6?Mathf.Clamp(player.snowManaPool[color],0,totals[color]):0;
            int count=i>=6?snow:Mathf.Max(0,totals[color]-snow);
            if(count>0)entries.Add(new ForgeManaPoolEntry{id="mana:"+(i>=6?"S:":"")+PoolSymbols[color],symbol=PoolSymbols[color],count=count,snow=i>=6});
        }
        return entries.ToArray();
    }
    private RectTransform CreatePool(string name)
    {
        var row=Panel(surface,name,Vector2.zero,new Vector2(240,68),Color.clear);
        row.gameObject.AddComponent<RectMask2D>();var scroll=row.gameObject.AddComponent<BattlefieldHorizontalScroll>();
        scroll.hidePartialChildren=true;
        var content=Rect(row,"Content",Vector2.zero,row.sizeDelta);
        content.anchorMin=content.anchorMax=content.pivot=new Vector2(0,1);
        scroll.viewport=row;scroll.content=content;return row;
    }
    private PoolCell CreatePoolCell(RectTransform content,ForgeManaPoolEntry entry)
    {
        string name=entry.restricted?"Pool_Restricted_"+entry.id:"Pool_"+(entry.snow?"S_":"")+entry.symbol;
        var root=Panel(content,name,Vector2.zero,new Vector2(56,34),Color.clear);root.anchorMin=root.anchorMax=new Vector2(0,1);
        var cell=new PoolCell{root=root,button=root.gameObject.AddComponent<Button>()};
        cell.button.targetGraphic=root.GetComponent<Image>();cell.button.transition=Selectable.Transition.None;
        int color=Math.Max(0,Array.IndexOf(PoolSymbols,entry.symbol));
        if(poolGlyphFont==null)poolGlyphFont=Resources.Load<Font>("UI/ManaSymbols/ManaGlyphs");
        var symbol=Rect(root,"Symbol",new Vector2(-9,3),new Vector2(26,28));
        var glyph=symbol.gameObject.AddComponent<Text>();glyph.font=poolGlyphFont;
        glyph.text=entry.snow?"\uE996":PoolGlyphs[color];glyph.fontSize=22;glyph.color=PoolColors[color];
        glyph.alignment=TextAnchor.MiddleCenter;glyph.horizontalOverflow=HorizontalWrapMode.Overflow;glyph.verticalOverflow=VerticalWrapMode.Overflow;glyph.raycastTarget=false;
        var glyphPreference=symbol.gameObject.AddComponent<PreferenceText>();glyphPreference.KeepFont=true;
        glyphPreference.SetBaseSize(22);glyph.font=poolGlyphFont;
        if(entry.restricted)symbol.gameObject.AddComponent<RestrictedManaGlow>();
        cell.count=Label(root,"Count","",new Vector2(15,-6),new Vector2(28,30),18,Paper);
        cell.count.fontStyle=FontStyle.Normal;cell.count.supportRichText=false;cell.count.alignment=TextAnchor.MiddleLeft;
        cell.count.horizontalOverflow=HorizontalWrapMode.Overflow;cell.count.verticalOverflow=VerticalWrapMode.Overflow;cell.count.raycastTarget=false;
        if(battlefieldNumeralFont==null)battlefieldNumeralFont=Font.CreateDynamicFontFromOSFont(new[]{"Times New Roman","Liberation Serif","Georgia"},24);
        var pref=cell.count.GetComponent<PreferenceText>();if(pref!=null){pref.KeepFont=true;pref.enabled=false;}
        cell.count.font=battlefieldNumeralFont;
        var outline=cell.count.gameObject.AddComponent<Outline>();outline.effectColor=new Color(0,0,0,.95f);outline.effectDistance=new Vector2(1,-1);
        return cell;
    }
    private void RenderPool(RectTransform row,Dictionary<string,PoolCell> cells,ForgePlayerView player,bool interactive,bool sameOwner)
    {
        var entries=PoolEntries(player);var scroll=row.GetComponent<BattlefieldHorizontalScroll>();
        var retained=new HashSet<string>(entries.Select(e=>e.id));
        foreach(var id in cells.Keys.Where(id=>!retained.Contains(id)).ToArray()) {
            cells[id].root.gameObject.SetActive(false);Destroy(cells[id].root.gameObject);cells.Remove(id);
        }
        float stride=54;
        foreach(var entry in entries) {
            if(!cells.TryGetValue(entry.id,out var cell)){cell=CreatePoolCell(scroll.content,entry);cells.Add(entry.id,cell);}
            cell.root.gameObject.SetActive(true);cell.count.fontSize=Mathf.RoundToInt(18*GamePreferences.TextScale);cell.count.text=entry.count.ToString();
            stride=Mathf.Max(stride,30+cell.count.preferredWidth+8);
            int index=interactive?OptionIndex(entry.id):-1;long captured=requestId;
            cell.button.onClick.RemoveAllListeners();cell.button.onClick.AddListener(()=>Choose(index,captured));
            cell.button.interactable=interactive&&canAct&&!submitted&&view?.inputPending!=true&&index>=0&&!view.request.options[index].disabled;
            // Both players may inspect/scroll. Only a current native payment
            // option can submit; disabled public buttons cannot spend mana.
            cell.root.GetComponent<Image>().raycastTarget=true;
            if(sameOwner&&entry.count>cell.amount)cell.changed=Time.unscaledTime;else if(!sameOwner||entry.count!=cell.amount)cell.changed=-10;
            cell.amount=entry.count;
        }
        // One short row fits between the land tiles and the hand. More colors
        // or restriction buckets extend horizontally instead of under the hand.
        int rows=1,columns=entries.Length,visible=Mathf.Min(4,Mathf.Max(1,columns));
        float width=visible*stride,height=rows*34;
        PlacePlayerMana(row,player?.seat??seat,width,height);row.gameObject.SetActive(entries.Length>0);
        scroll.content.sizeDelta=new Vector2(Mathf.Max(width,columns*stride),height);scroll.Configure(stride);
        for(int i=0;i<entries.Length;i++) {
            var cell=cells[entries[i].id];cell.root.sizeDelta=new Vector2(stride,34);
            cell.root.anchoredPosition=new Vector2((i/rows+.5f)*stride,-(i%rows+.5f)*34);
            ((RectTransform)cell.root.Find("Symbol")).anchoredPosition=new Vector2(-stride*.5f+17,3);
            cell.count.rectTransform.anchoredPosition=new Vector2(-stride*.5f+31,-2);
            cell.count.rectTransform.pivot=new Vector2(0,.5f);cell.count.rectTransform.sizeDelta=new Vector2(stride-32,26);
        }
    }
    private void PresentManaPool()
    {
        var owner=view?.players?.FirstOrDefault(p=>p.seat==ActionPlayerSeat);
        SceneText("CornerCluster/ManaRow/Label").gameObject.SetActive(false);
        if(manaPoolRow==null)manaPoolRow=CreatePool("ManaPoolChips");
        RenderPool(manaPoolRow,poolCells,owner,true,manaOwner==owner?.seat);manaOwner=owner?.seat??0;
        var other=view?.players?.FirstOrDefault(p=>p.seat!=manaOwner);
        if(publicManaPoolRow==null)publicManaPoolRow=CreatePool("PublicManaPool");
        RenderPool(publicManaPoolRow,publicPoolCells,other,false,true);UpdateManaPoolPulse();
    }
    private void PlacePlayerMana(RectTransform row,int playerSeat,float width,float height)
    {
        row.SetSiblingIndex(Node("CornerCluster").GetSiblingIndex());
        row.anchorMin=row.anchorMax=Vector2.zero;row.pivot=new Vector2(.5f,.5f);
        float offset=(height-34)*.5f;
        // The player information column is outside both land and hand lanes.
        row.anchoredPosition=new Vector2(24+width*.5f,playerSeat==seat?248-offset:surface.rect.height-248+offset);
        row.sizeDelta=new Vector2(width,height);
    }
    private void UpdateManaPoolPulse()
    {
        foreach(var cell in poolCells.Values.Concat(publicPoolCells.Values)) {
            float elapsed=Time.unscaledTime-cell.changed,pulse=elapsed>=0&&elapsed<.45f?Mathf.Sin(elapsed/.45f*Mathf.PI):0;
            cell.root.localScale=Vector3.one; // Color pulse keeps column bounds intact.
            cell.count.color=Color.Lerp(Color.white,Gold,pulse*.4f);
        }
    }
}
