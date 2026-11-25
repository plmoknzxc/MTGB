using UnityEngine;
using UnityEngine.UI;

public sealed class GameSettingsPanel : MonoBehaviour
{
    static GameSettingsPanel current;
    Text preview;
    System.Action concede;
    Button concedeButton;
    Button puzzleExit;
    Text puzzleGoal;
    OnlineMatchService puzzleSession;
    GameObject concedeConfirmation;
    bool archiveStyle;
    bool battlefield;
    bool fourSeatSettings;
    Button fixedSeats, resizableSeats;
    int styledRevision = -1;
    MenuThemeStyle styledTheme;
    RectTransform dialog;
    RectTransform soundDialog;
    Button soundButton;
    Button doneButton;
    Text soundHeading;
    Text dialogTitle;
    Toggle replayToggle;
    public static bool IsOpen => current!=null && current.gameObject.activeInHierarchy;
    public static GameSettingsPanel Show(Transform parent, System.Action onConcede=null, bool archiveStyle=false, bool battlefield=false, OnlineMatchService puzzleSession=null)
    {
        var canvas=parent.GetComponentInParent<Canvas>().rootCanvas;
        bool fourSeats=battlefield && UnityEngine.SceneManagement.SceneManager.GetActiveScene().name==ForgeBattlefieldController.FourPlayerSceneName;
        if(current!=null && (current.archiveStyle!=archiveStyle || current.battlefield!=battlefield || current.fourSeatSettings!=fourSeats))
        {
            current.gameObject.SetActive(false);
            Destroy(current.gameObject);
            current=null;
        }
        if(current!=null)
        {
            if (current.transform.parent != canvas.transform) current.transform.SetParent(canvas.transform,false);
            current.Configure(onConcede,puzzleSession);
            current.gameObject.SetActive(true);
            current.transform.SetAsLastSibling();
            current.FitPanel();
            return current;
        }
        var go=new GameObject("GameSettings",typeof(RectTransform),typeof(Image),typeof(GameSettingsPanel));
        go.transform.SetParent(canvas.transform,false);
        var r=(RectTransform)go.transform;r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.offsetMin=r.offsetMax=Vector2.zero;
        go.GetComponent<Image>().color=new Color(0,0,0,.75f);
        current=go.GetComponent<GameSettingsPanel>();
        current.archiveStyle=archiveStyle;
        current.battlefield=battlefield;
        current.fourSeatSettings=fourSeats;
        current.Build();current.Configure(onConcede,puzzleSession);
        return current;
    }
    void Configure(System.Action onConcede,OnlineMatchService session)
    {
        puzzleSession=session;
        bool puzzle=session?.CurrentPuzzle!=null;
        concede=puzzle?null:onConcede;
        concedeButton.gameObject.SetActive(concede!=null);
        ((RectTransform)doneButton.transform).anchoredPosition = new Vector2(concede!=null || puzzle ? 245 : 0,
            ((RectTransform)concedeButton.transform).anchoredPosition.y);
        replayToggle.gameObject.SetActive(!puzzle);
        if(puzzle && puzzleExit==null)
        {
            float y=((RectTransform)concedeButton.transform).anchoredPosition.y;
            puzzleExit=Button(dialog,"退出残局",-245,y,()=>puzzleSession?.LeavePuzzle(false));
            MenuVisualTheme.StyleDeckTool(puzzleExit);
            puzzleGoal=Label(dialog,"",20,((RectTransform)replayToggle.transform).anchoredPosition,new Vector2(730,50));
        }
        if(puzzleExit!=null)
        {
            puzzleExit.gameObject.SetActive(puzzle);
            puzzleGoal.gameObject.SetActive(puzzle);
            if(puzzle) puzzleGoal.text=session.CurrentPuzzle.title+"   ·   "+session.CurrentPuzzle.Objective;
        }
        if(concedeConfirmation!=null)concedeConfirmation.SetActive(false);
        if(soundDialog!=null)soundDialog.gameObject.SetActive(false);
        dialog.gameObject.SetActive(true);
    }
    void Build()
    {
        var box=new GameObject("SettingsPanel",typeof(RectTransform),typeof(Image));box.transform.SetParent(transform,false);
        float compactOffset=GamePreferences.LanguageSelectionEnabled ? 0 : 68;
        float detailOffset=battlefield ? 0 : 54;
        float windowSpace=fourSeatSettings?88:0;
        const float soundRowHeight = 64;
        float pickerHeight = MenuThemePicker.Height + PlayerAvatarPicker.Height + soundRowHeight;
        var r=(RectTransform)box.transform;r.anchorMin=r.anchorMax=r.pivot=new Vector2(.5f,.5f);
        r.sizeDelta = new Vector2(820, 704 - (compactOffset+detailOffset)*2 + pickerHeight + windowSpace);
        float belowPickers = r.sizeDelta.y/2 - 80 - pickerHeight;
        float upperOffset = belowPickers - (GamePreferences.LanguageSelectionEnabled ? 208 : 72);
        float footerOffset = 416 - r.sizeDelta.y/2;
        box.GetComponent<Image>().color=MenuVisualTheme.Panel;
        dialog=r;
        if (archiveStyle) MenuTypography.Scope(dialog);
        dialogTitle=Label(box.transform,"游戏设置",28,new Vector2(0,r.sizeDelta.y/2-36),new Vector2(730,64));
        var themePicker = MenuThemePicker.Create(box.transform);
        themePicker.sizeDelta = new Vector2(730,MenuThemePicker.Height);
        themePicker.anchoredPosition = new Vector2(0,r.sizeDelta.y/2-80-MenuThemePicker.Height/2);
        var avatars=PlayerAvatarPicker.Create(box.transform);
        avatars.sizeDelta=new Vector2(730,PlayerAvatarPicker.Height);
        avatars.anchoredPosition=new Vector2(0,r.sizeDelta.y/2-80-MenuThemePicker.Height-PlayerAvatarPicker.Height/2);
        float soundY = belowPickers + soundRowHeight/2;
        soundHeading=Label(box.transform,"声音",21,new Vector2(0,soundY),new Vector2(730,44));
        soundHeading.GetComponent<PreferenceText>().Literal=true;
        soundButton=Button(box.transform,"调节音量",288,soundY,OpenSound);
        ((RectTransform)soundButton.transform).sizeDelta = new Vector2(154,44);
        soundButton.GetComponentInChildren<Text>().rectTransform.sizeDelta = new Vector2(144,42);
        FitPanel();
        if(GamePreferences.LanguageSelectionEnabled)
        {
            Label(box.transform,"语言",21,new Vector2(0,belowPickers-33),new Vector2(730,40));
            Button(box.transform,"简体中文",-245,belowPickers-96,()=>GamePreferences.SetLanguage("zh-CN"));
            Button(box.transform,"日本語",0,belowPickers-96,()=>GamePreferences.SetLanguage("ja-JP"));
            Button(box.transform,"English",245,belowPickers-96,()=>GamePreferences.SetLanguage("en-US"));
        }
        Label(box.transform,"文字大小",21,new Vector2(0,36+upperOffset),new Vector2(730,40));
        Button(box.transform,"小",-245,-24+upperOffset,()=>GamePreferences.SetScale(.85f));
        Button(box.transform,"标准",0,-24+upperOffset,()=>GamePreferences.SetScale(1));
        Button(box.transform,"大",245,-24+upperOffset,()=>GamePreferences.SetScale(1.3f));
        preview=Label(box.transform,"",20,new Vector2(0,-96+upperOffset),new Vector2(730,48));
        if(battlefield)
        {
            Label(box.transform,"卡牌详情",21,new Vector2(0,-146+upperOffset),new Vector2(730,36));
            Button(box.transform,"左侧面板",-122,-194+upperOffset,()=>GamePreferences.SetHoverCardDetails(false));
            Button(box.transform,"悬停浮窗",122,-194+upperOffset,()=>GamePreferences.SetHoverCardDetails(true));
        }
        if(fourSeatSettings)
        {
            string Local(string zh,string en,string ja)=>GamePreferences.Language.StartsWith("en")?en:GamePreferences.Language.StartsWith("ja")?ja:zh;
            Label(box.transform,Local("四人战场布局","Four-seat layout","4人戦の配置"),21,new Vector2(0,-238+upperOffset),new Vector2(730,36));
            fixedSeats=Button(box.transform,Local("固定四分屏","Fixed quadrants","固定四分割"),-122,-282+upperOffset,()=>GamePreferences.SetResizableFourSeats(false));
            resizableSeats=Button(box.transform,Local("拖动边框","Resizable panes","枠をドラッグ"),122,-282+upperOffset,()=>GamePreferences.SetResizableFourSeats(true));
        }
        replayToggle = ReplayWidgets.AutoSaveToggle(box.transform);
        ((RectTransform)replayToggle.transform).anchoredPosition = new Vector2(0,-256+footerOffset);
        doneButton=Button(box.transform,"完成",0,-316+footerOffset,()=>gameObject.SetActive(false));
        concedeButton=Button(box.transform,"认输",-245,-316+footerOffset,ConfirmConcede);
        RefreshTheme();
    }

    void RefreshTheme()
    {
        if (!archiveStyle || styledTheme == MenuThemeCatalog.Current) return;
        styledTheme = MenuThemeCatalog.Current;
        GetComponent<Image>().color = MenuVisualTheme.DialogShade;
        MenuVisualTheme.StyleDeckDialogSurface(dialog,dialogTitle);
        MenuVisualTheme.StylePreferenceToggle(replayToggle);
        foreach (Transform row in dialog)
            if (row.TryGetComponent<Text>(out var label)) label.color = MenuVisualTheme.ArchivePaper;
    }
    void OnRectTransformDimensionsChange() => FitPanel();

    void FitPanel()
    {
        if (transform.Find("SettingsPanel") is not RectTransform box || box.sizeDelta.y <= 0) return;
        var area=((RectTransform)transform).rect;
        if (area.width > 28 && area.height > 28) {
            box.localScale=Vector3.one*Mathf.Min(1,(area.width-28)/box.sizeDelta.x,(area.height-28)/box.sizeDelta.y);
            if (soundDialog!=null)
                soundDialog.localScale=Vector3.one*Mathf.Min(1,(area.width-28)/soundDialog.sizeDelta.x,(area.height-28)/soundDialog.sizeDelta.y);
        }
    }
    static string SoundText(string zh,string en,string ja) =>
        GamePreferences.Language.StartsWith("en") ? en : GamePreferences.Language.StartsWith("ja") ? ja : zh;

    void OpenSound()
    {
        if(soundDialog!=null)Destroy(soundDialog.gameObject);
        dialog.gameObject.SetActive(false);
        soundDialog=ReplayWidgets.Panel(transform,"SoundSettings",0,0,650,344,MenuVisualTheme.Panel);
        var title=Label(soundDialog,SoundText("声音","Sound","サウンド"),28,new Vector2(0,132),new Vector2(570,48));
        MenuVisualTheme.StyleDeckDialogSurface(soundDialog,title,75);
        SoundSlider("Music",SoundText("背景音乐","Music","BGM"),45,true);
        SoundSlider("Effects",SoundText("音效","Sound effects","効果音"),-27,false);
        var done=Button(soundDialog,"完成",0,-117,()=>{
            PlayerPrefs.Save();
            soundDialog.gameObject.SetActive(false);
            dialog.gameObject.SetActive(true);
        });
        MenuVisualTheme.StyleDeckTool(done,true);
        FitPanel();
    }
    void SoundSlider(string id,string caption,float y,bool music)
    {
        Label(soundDialog,caption,21,new Vector2(-198,y),new Vector2(166,48));
        var value=Label(soundDialog,"",20,new Vector2(244,y),new Vector2(76,48));
        value.alignment=TextAnchor.MiddleRight;
        var hit=ReplayWidgets.Panel(soundDialog,id,35,y,290,48,Color.clear);
        var slider=hit.gameObject.AddComponent<Slider>();
        var track=ReplayWidgets.Panel(hit,"Track",0,0,270,7,MenuVisualTheme.Slate);
        var fill=ReplayWidgets.Panel(track,"Fill",0,0,0,0,MenuVisualTheme.MainAccent);
        fill.anchorMin=Vector2.zero;fill.anchorMax=Vector2.one;fill.offsetMin=fill.offsetMax=Vector2.zero;
        var handle=ReplayWidgets.Panel(track,"Handle",0,0,18,25,MenuVisualTheme.ArchivePaper);
        slider.fillRect=fill;
        slider.handleRect=handle;
        slider.targetGraphic=handle.GetComponent<Image>();
        slider.minValue=0;slider.maxValue=100;slider.wholeNumbers=true;
        slider.SetValueWithoutNotify(Mathf.RoundToInt(100*(music?GameAudio.MusicVolume:GameAudio.EffectsVolume)));
        value.text=slider.value+"%";
        slider.onValueChanged.AddListener(percent=>{
            GameAudio.SetVolume(music,percent/100);
            value.text=percent+"%";
        });
    }
    void ConfirmConcede()
    {
        if(concede==null)return;
        if(concedeConfirmation==null)
        {
            concedeConfirmation=new GameObject("ConcedeConfirmation",typeof(RectTransform),typeof(Image));
            concedeConfirmation.transform.SetParent(transform,false);
            var rect=(RectTransform)concedeConfirmation.transform;rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;
            concedeConfirmation.GetComponent<Image>().color=archiveStyle?MenuVisualTheme.DialogShade:new Color(.035f,.05f,.07f,.98f);
            var box=ReplayWidgets.Panel(rect,"ConfirmDialog",0,0,560,186,MenuVisualTheme.Panel);
            var heading=Label(box,"确认认输？",26,new Vector2(0,52),new Vector2(504,50));
            Button(box,"继续对局",-128,-38,()=>{concedeConfirmation.SetActive(false);dialog.gameObject.SetActive(true);});
            Button(box,"确认认输",128,-38,()=> {
                if(!gameObject.activeInHierarchy || !concedeConfirmation.activeSelf)return;
                var action=concede;concede=null;gameObject.SetActive(false);action?.Invoke();
            });
            heading.alignment=TextAnchor.MiddleCenter;
        }
        if(archiveStyle){
            var box=(RectTransform)concedeConfirmation.transform.Find("ConfirmDialog");
            MenuVisualTheme.StyleDeckDialogSurface(box,box.Find("Label").GetComponent<Text>());
            MenuVisualTheme.StyleDeckTool(box.Find("继续对局").GetComponent<Button>(),true);
            MenuVisualTheme.StyleDeckTool(box.Find("确认认输").GetComponent<Button>());
            concedeConfirmation.GetComponent<Image>().color=MenuVisualTheme.DialogShade;
        }
        dialog.gameObject.SetActive(false);
        concedeConfirmation.SetActive(true);concedeConfirmation.transform.SetAsLastSibling();
    }
    void Update()
    {
        if(preview!=null) preview.text=GamePreferences.T("文字预览")+" · "+Mathf.RoundToInt(GamePreferences.TextScale*100)+"%";
        if(archiveStyle && styledRevision==GamePreferences.Revision)return;
        styledRevision=GamePreferences.Revision;
        if(soundButton!=null){
            soundHeading.text=SoundText("声音","Sound","サウンド");
            soundButton.GetComponentInChildren<Text>().text=SoundText("调节音量","Volume","音量調整");
        }
        RefreshTheme();
        foreach(var button in GetComponentsInChildren<Button>())
        {
            if(button.GetComponentInParent<PlayerAvatarPicker>()!=null)continue;
            bool selected=button.name=="简体中文" && GamePreferences.Language=="zh-CN" || button.name=="日本語" && GamePreferences.Language=="ja-JP" || button.name=="English" && GamePreferences.Language=="en-US"
                || button.name=="小" && GamePreferences.TextScale<.9f || button.name=="标准" && Mathf.Approximately(GamePreferences.TextScale,1) || button.name=="大" && GamePreferences.TextScale>1.2f
                || button.name=="左侧面板" && !GamePreferences.HoverCardDetails || button.name=="悬停浮窗" && GamePreferences.HoverCardDetails;
            selected|=button==fixedSeats && !GamePreferences.ResizableFourSeats || button==resizableSeats && GamePreferences.ResizableFourSeats;
            if(archiveStyle)MenuVisualTheme.StyleDeckTool(button,selected || button.name=="完成" || button.name=="继续对局");
            else button.GetComponent<Image>().color=selected?MenuVisualTheme.Primary:MenuVisualTheme.Raised;
        }
    }
    void LateUpdate()
    {
        // A fresh match snapshot can reorder the hand; the modal must continue to block it.
        if(transform.GetSiblingIndex()!=transform.parent.childCount-1)transform.SetAsLastSibling();
    }
    static Text Label(Transform parent,string value,int size,Vector2 position,Vector2 dimensions)
    {
        var go=new GameObject("Label",typeof(RectTransform),typeof(Text));go.transform.SetParent(parent,false);
        var t=go.GetComponent<Text>();t.text=value;t.font=GamePreferences.Font ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");t.fontSize=size;t.color=MenuVisualTheme.Paper;t.alignment=TextAnchor.MiddleLeft;t.raycastTarget=false;
        t.rectTransform.sizeDelta=dimensions;t.rectTransform.anchoredPosition=position;PreferenceText.SetSize(t,size);return t;
    }
    static Button Button(Transform parent,string value,float x,float y,UnityEngine.Events.UnityAction action)
    {
        var go=new GameObject(value,typeof(RectTransform),typeof(Image),typeof(Button));go.transform.SetParent(parent,false);
        var r=(RectTransform)go.transform;r.sizeDelta=new Vector2(218,50);r.anchoredPosition=new Vector2(x,y);
        ButtonPressSound.Bind(go.GetComponent<Button>());
        go.GetComponent<Image>().color=MenuVisualTheme.Raised;go.GetComponent<Button>().onClick.AddListener(action);
        var text=Label(go.transform,value,20,Vector2.zero,new Vector2(208,48));text.alignment=TextAnchor.MiddleCenter;
        return go.GetComponent<Button>();
    }
}
