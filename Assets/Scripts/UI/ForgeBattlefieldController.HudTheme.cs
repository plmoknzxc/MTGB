using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Shared presentation for battlefield information panels, not card colours or
// player-identity colours. All decoration is input-transparent.
public sealed partial class ForgeBattlefieldController
{
    private MenuThemeStyle HudTheme => worldTable != null ? MenuThemeCatalog.Current : null;
    private Color HudInk => HudTheme != null ? (Color)HudTheme.SectionFill : new Color(.038f,.048f,.055f,.97f);
    private Color HudRaised => HudTheme != null ? (Color)HudTheme.ArchivePanel : new Color(.072f,.084f,.089f,.98f);
    private Color HudSelected => HudTheme != null ? (Color)HudTheme.ActionFill : new Color(.12f,.115f,.093f,.98f);
    private Color HudText => HudTheme != null ? (Color)HudTheme.ArchivePaper : new Color(.94f,.92f,.86f);
    private Color HudMuted => HudTheme != null ? (Color)HudTheme.Stone : new Color(.64f,.67f,.65f);
    private Color HudAccent => HudTheme != null ? (Color)HudTheme.ActionEdge : new Color(.78f,.66f,.43f);
    private Color HudEdge => HudTheme != null ? (Color)HudTheme.InputRim : new Color(.49f,.43f,.32f,.66f);
    private Color HudRule => HudTheme != null ? (Color)HudTheme.Etching : new Color(.63f,.54f,.37f,.30f);

    internal Transform JournalThemeRoot => battleSidebar;
    internal Transform JournalToggleThemeRoot => sidebarToggle != null ? sidebarToggle.transform : null;
    private MenuThemeStyle paintedDockTheme;
    private RectTransform phaseDockBacking;
    private readonly List<Button> phaseDockButtons=new();
    private void DuelSurface(RectTransform root,BattlefieldHudGraphic.Surface surface,Color edge,bool targetable=false)
    {
        if (root.TryGetComponent<Image>(out var image)) image.color=Color.clear;
        if (root.TryGetComponent<Outline>(out var outline)) outline.enabled=false;
        if (root.Find("CompactFrame") is Transform frame) frame.gameObject.SetActive(false);
        BattlefieldHudGraphic.Apply(root,HudTheme,surface,edge,targetable);
    }

    private void StyleWorldDock()
    {
        var theme = HudTheme;
        if (theme == null || paintedDockTheme == theme) return;
        paintedDockTheme = theme;
        if(journalPages!=null)
        {
            DuelSurface(journalPages,BattlefieldHudGraphic.Surface.Journal,theme.Silver);
            DuelSurface((RectTransform)journalPages.Find("JournalHeader"),BattlefieldHudGraphic.Surface.JournalHeader,theme.Silver);
            RefreshJournalTabTheme();
        }
        var row = Node("CornerCluster/ControlRow");
        var backing = row.Find("PhaseDockSurface") as RectTransform;
        if (backing == null)
        {
            backing = Panel(row,"PhaseDockSurface",new Vector2(48,27),new Vector2(200,154),HudInk);
            backing.GetComponent<Image>().raycastTarget = false;
            backing.SetAsFirstSibling();
        }
        DuelSurface(backing,BattlefieldHudGraphic.Surface.Phase,theme.ActionEdge);
        phaseDockBacking=backing;
        var actionPanel=(RectTransform)actionRoot.parent.parent;
        StyleHudSurface(actionPanel,HudInk,HudEdge);
        var phase = SceneText("CornerCluster/ControlRow/PhaseText");
        MenuTypography.Use(phase,MenuTypography.Face.Heading);
        phase.color = HudText;
        var turn = SceneText("CornerCluster/ControlRow/TurnChip/TurnLabel");
        MenuTypography.Use(turn,MenuTypography.Face.Control);
        turn.color = HudMuted;
        var round = SceneText("CornerCluster/ControlRow/TurnChip/TurnNumber");
        MenuTypography.Use(round,MenuTypography.Face.Control);
        round.color=theme.ActionEdge;
        var settings = Node("GearBox/SettingsButton").GetComponent<Button>();
        MenuVisualTheme.StyleArchiveControl(settings);
        settings.GetComponentInChildren<ForgeGearIcon>().color = HudText;
    }

    private void LayoutPhaseDock()
    {
        if(phaseDockBacking==null)return;
        var row=phaseDockBacking.parent;
        bool showPhase=view?.turn>0 && !string.IsNullOrEmpty(view.phase) && view.phase!="null" && !IsMulliganChoice;
        phaseDockBacking.gameObject.SetActive(showPhase);
        row.Find("TurnChip").gameObject.SetActive(showPhase);
        row.Find("PhaseText").gameObject.SetActive(showPhase);
        row.Find("PhaseTrack").gameObject.SetActive(showPhase);
        if(!showPhase)return;
        float height=advanceButton.gameObject.activeSelf?154:96;
        float shift=154-height;
        if(shift>0){
            // Payment, attack, target and numeric prompts can all replace Next in this slot.
            var compactBounds=new Rect(-52,-50,200,96);
            row.GetComponentsInChildren(false,phaseDockButtons);
            foreach(var button in phaseDockButtons){
                var rect=(RectTransform)button.transform;
                var min=row.InverseTransformPoint(rect.TransformPoint(rect.rect.min));
                var max=row.InverseTransformPoint(rect.TransformPoint(rect.rect.max));
                if(!compactBounds.Overlaps(UnityEngine.Rect.MinMaxRect(min.x,min.y,max.x,max.y)))continue;
                shift=0;break;
            }
        }
        phaseDockBacking.sizeDelta=new Vector2(200,height);
        phaseDockBacking.anchoredPosition=new Vector2(48,PhaseDockTop-height*.5f-shift);
        ((RectTransform)row.Find("TurnChip")).anchoredPosition=new Vector2(48,82-shift);
        ((RectTransform)row.Find("PhaseText")).anchoredPosition=new Vector2(48,49-shift);
        ((RectTransform)row.Find("PhaseTrack")).anchoredPosition=new Vector2(48,19-shift);
    }

    private void StyleHudSurface(RectTransform root,Color fill,Color edge)
    {
        var image=root.GetComponent<Image>();
        if(image!=null){
            fill.a=1;
            image.color=fill;
            image.sprite=IntegratedUiUtility.SlicedRoundedRect(8);
            image.type=Image.Type.Sliced;
        }
        if(root.TryGetComponent<Outline>(out var outline))outline.enabled=false;
        var frame=RedesignStackFrame(root,6,edge);
        frame.Thickness=1;frame.enabled=true;
    }

    private void StyleBattleIcon(Button button)
    {
        var image=button.image;
        image.sprite=IntegratedUiUtility.RoundedRect(40,38,6);
        image.type=Image.Type.Simple;
        image.color=Color.white;
        Color fill=Color.Lerp(HudRaised,HudTheme.ControlEdge,.22f);
        fill.a=1;
        var colors=ColorBlock.defaultColorBlock;
        colors.normalColor=fill;
        colors.highlightedColor=Color.Lerp(fill,HudAccent,.33f);
        colors.selectedColor=Color.Lerp(fill,HudAccent,.20f);
        colors.pressedColor=HudSelected;
        button.transition=Selectable.Transition.ColorTint;
        button.colors=colors;
        var rim=RedesignStackFrame((RectTransform)button.transform,6,Color.Lerp(HudEdge,HudText,.45f));
        rim.Thickness=1.25f;
        button.GetComponentInChildren<StackControlIcon>().color=HudText;
        if(button.TryGetComponent<Outline>(out var outline))outline.enabled=false;
    }
}
