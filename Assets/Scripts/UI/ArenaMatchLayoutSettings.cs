using System;
using UnityEngine;

[Serializable]
public sealed class ArenaMatchLayoutSettings
{
    public MTGAHandLayoutSettings hand = new MTGAHandLayoutSettings();
    public BattlefieldLayout battlefield = new BattlefieldLayout();
    public StackLayout stack = new StackLayout();
    public HudLayout hud = new HudLayout();

    public void CopyFrom(ArenaMatchLayoutSettings other)
    {
        if (other == null) return;
        EnsureSections();
        hand.CopyFrom(other.hand ?? new MTGAHandLayoutSettings());
        battlefield.CopyFrom(other.battlefield ?? new BattlefieldLayout());
        stack.CopyFrom(other.stack ?? new StackLayout());
        hud.CopyFrom(other.hud ?? new HudLayout());
        Sanitize();
    }

    public void Sanitize()
    {
        EnsureSections();
        hand.Sanitize();
        battlefield.Sanitize();
        stack.Sanitize();
        hud.Sanitize();
    }

    private void EnsureSections()
    {
        if (hand == null) hand = new MTGAHandLayoutSettings();
        if (battlefield == null) battlefield = new BattlefieldLayout();
        if (stack == null) stack = new StackLayout();
        if (hud == null) hud = new HudLayout();
    }

    [Serializable]
    public sealed class BattlefieldLayout
    {
        public Vector3 playerDropAreaPosition = new Vector3(0f, 0.08f, -1.62f);
        public Vector3 playerDropAreaScale = new Vector3(14f, 0.08f, 3.5f);
        public Vector3 opponentDropAreaPosition = new Vector3(0f, 0.08f, 1.62f);
        public Vector3 opponentDropAreaScale = new Vector3(14f, 0.08f, 3.5f);
        public float playerLandRowZ = -2.48f;
        public float playerFrontRowZ = -1.18f;
        public float opponentLandRowZ = 2.48f;
        public float opponentFrontRowZ = 1.18f;
        public float landRowY = 0.205f;
        public float frontRowY = 0.255f;
        public float rowYStep = 0.003f;
        public float landSpacing = 0.78f;
        public float frontSpacing = 0.92f;
        public float rowWidth = 12.10f;
        public float minimumScale = 0.42f;
        public Vector3 creatureScale = new Vector3(0.58f, 0.040f, 0.80f);
        public Vector3 permanentScale = new Vector3(0.53f, 0.036f, 0.73f);
        public float attackOffsetZ = 0.46f;
        public float idleYawPerCard = -0.85f;
        public float idleYawLimit = 5.5f;
        public float blockerPlayerZ = -0.18f;
        public float blockerOpponentZ = 0.18f;
        public float blockerBaseY = 0.285f;
        public float blockerSpacingX = 0.48f;
        public float blockerLayerY = 0.006f;

        public void CopyFrom(BattlefieldLayout other)
        {
            playerDropAreaPosition = other.playerDropAreaPosition;
            playerDropAreaScale = other.playerDropAreaScale;
            opponentDropAreaPosition = other.opponentDropAreaPosition;
            opponentDropAreaScale = other.opponentDropAreaScale;
            playerLandRowZ = other.playerLandRowZ;
            playerFrontRowZ = other.playerFrontRowZ;
            opponentLandRowZ = other.opponentLandRowZ;
            opponentFrontRowZ = other.opponentFrontRowZ;
            landRowY = other.landRowY;
            frontRowY = other.frontRowY;
            rowYStep = other.rowYStep;
            landSpacing = other.landSpacing;
            frontSpacing = other.frontSpacing;
            rowWidth = other.rowWidth;
            minimumScale = other.minimumScale;
            creatureScale = other.creatureScale;
            permanentScale = other.permanentScale;
            attackOffsetZ = other.attackOffsetZ;
            idleYawPerCard = other.idleYawPerCard;
            idleYawLimit = other.idleYawLimit;
            blockerPlayerZ = other.blockerPlayerZ;
            blockerOpponentZ = other.blockerOpponentZ;
            blockerBaseY = other.blockerBaseY;
            blockerSpacingX = other.blockerSpacingX;
            blockerLayerY = other.blockerLayerY;
        }

        public void Sanitize()
        {
            playerDropAreaScale = Positive(playerDropAreaScale, 0.01f);
            opponentDropAreaScale = Positive(opponentDropAreaScale, 0.01f);
            landSpacing = Mathf.Max(0.01f, landSpacing);
            frontSpacing = Mathf.Max(0.01f, frontSpacing);
            rowWidth = Mathf.Max(0.1f, rowWidth);
            minimumScale = Mathf.Clamp(minimumScale, 0.1f, 1f);
            creatureScale = Positive(creatureScale, 0.001f);
            permanentScale = Positive(permanentScale, 0.001f);
            idleYawLimit = Mathf.Max(0f, idleYawLimit);
        }
    }

    [Serializable]
    public sealed class StackLayout
    {
        public Vector2 panelPosition = new Vector2(-34f, 74f);
        public Vector2 panelSize = new Vector2(430f, 540f);
        public Vector2 cardSize = new Vector2(220f, 310f);
        public Vector2 topCardPosition = new Vector2(-106f, 24f);
        public Vector2 depthOffset = new Vector2(-46f, 22f);
        public float firstBackRotation = -8f;
        public float rotationStep = -6f;
        public int maxVisibleCards = 4;
        public int inspectThreshold = 3;
        public Vector2 inspectButtonPosition = new Vector2(-4f, 74f);
        public Vector2 inspectButtonSize = new Vector2(72f, 44f);
        public Vector2 targetPanelPosition = new Vector2(0f, 318f);
        public Vector2 targetPanelSize = new Vector2(470f, 86f);
        public Vector2 cancelButtonPosition = new Vector2(-276f, 18f);
        public Vector2 cancelButtonSize = new Vector2(106f, 76f);
        public Vector2 previewPanelPosition = new Vector2(28f, 0f);
        public Vector2 previewPanelSize = new Vector2(382f, 546f);
        public Vector2 previewCardSize = new Vector2(334f, 472f);
        public Vector2 browserViewportPosition = new Vector2(0f, 8f);
        public Vector2 browserViewportSize = new Vector2(1320f, 390f);
        public Vector2 browserCardSize = new Vector2(226f, 320f);
        public float browserCardSpacing = 184f;
        public float browserMargin = 54f;
        public float browserContentHeight = 360f;
        public Vector2 browserClosePosition = new Vector2(0f, 52f);
        public Vector2 browserCloseSize = new Vector2(280f, 70f);
        public Vector2 browserScrollbarPosition = new Vector2(0f, 136f);
        public Vector2 browserScrollbarSize = new Vector2(980f, 18f);
        public float targetArrowWidth = 5f;
        public float targetArrowHeadSize = 30f;

        public void CopyFrom(StackLayout other)
        {
            panelPosition = other.panelPosition;
            panelSize = other.panelSize;
            cardSize = other.cardSize;
            topCardPosition = other.topCardPosition;
            depthOffset = other.depthOffset;
            firstBackRotation = other.firstBackRotation;
            rotationStep = other.rotationStep;
            maxVisibleCards = other.maxVisibleCards;
            inspectThreshold = other.inspectThreshold;
            inspectButtonPosition = other.inspectButtonPosition;
            inspectButtonSize = other.inspectButtonSize;
            targetPanelPosition = other.targetPanelPosition;
            targetPanelSize = other.targetPanelSize;
            cancelButtonPosition = other.cancelButtonPosition;
            cancelButtonSize = other.cancelButtonSize;
            previewPanelPosition = other.previewPanelPosition;
            previewPanelSize = other.previewPanelSize;
            previewCardSize = other.previewCardSize;
            browserViewportPosition = other.browserViewportPosition;
            browserViewportSize = other.browserViewportSize;
            browserCardSize = other.browserCardSize;
            browserCardSpacing = other.browserCardSpacing;
            browserMargin = other.browserMargin;
            browserContentHeight = other.browserContentHeight;
            browserClosePosition = other.browserClosePosition;
            browserCloseSize = other.browserCloseSize;
            browserScrollbarPosition = other.browserScrollbarPosition;
            browserScrollbarSize = other.browserScrollbarSize;
            targetArrowWidth = other.targetArrowWidth;
            targetArrowHeadSize = other.targetArrowHeadSize;
        }

        public void Sanitize()
        {
            panelSize = Positive(panelSize, 1f);
            cardSize = Positive(cardSize, 1f);
            maxVisibleCards = Mathf.Clamp(maxVisibleCards, 1, 12);
            inspectThreshold = Mathf.Clamp(inspectThreshold, 1, 20);
            inspectButtonSize = Positive(inspectButtonSize, 1f);
            targetPanelSize = Positive(targetPanelSize, 1f);
            cancelButtonSize = Positive(cancelButtonSize, 1f);
            previewPanelSize = Positive(previewPanelSize, 1f);
            previewCardSize = Positive(previewCardSize, 1f);
            browserViewportSize = Positive(browserViewportSize, 1f);
            browserCardSize = Positive(browserCardSize, 1f);
            browserCardSpacing = Mathf.Max(1f, browserCardSpacing);
            browserMargin = Mathf.Max(0f, browserMargin);
            browserContentHeight = Mathf.Max(1f, browserContentHeight);
            browserCloseSize = Positive(browserCloseSize, 1f);
            browserScrollbarSize = Positive(browserScrollbarSize, 1f);
            targetArrowWidth = Mathf.Max(1f, targetArrowWidth);
            targetArrowHeadSize = Mathf.Max(1f, targetArrowHeadSize);
        }
    }

    [Serializable]
    public sealed class HudLayout
    {
        public Vector2 playerPanelPosition = new Vector2(28f, 24f);
        public Vector2 opponentPanelPosition = new Vector2(-28f, -24f);
        public Vector2 playerPanelSize = new Vector2(420f, 116f);
        public Vector2 opponentPanelSize = new Vector2(420f, 116f);
        public Vector2 zoneButtonSize = new Vector2(82f, 112f);
        public Vector2 playerDeckPosition = new Vector2(116f, 150f);
        public Vector2 playerGraveyardPosition = new Vector2(206f, 150f);
        public Vector2 playerExilePosition = new Vector2(296f, 150f);
        public Vector2 opponentDeckPosition = new Vector2(-468f, -84f);
        public Vector2 opponentGraveyardPosition = new Vector2(-378f, -84f);
        public Vector2 opponentExilePosition = new Vector2(-288f, -84f);
        public Vector2 phaseRingPosition = new Vector2(-34f, 216f);
        public float phaseRingSize = 174f;
        public float phaseButtonSize = 150f;
        public Vector2 actionPromptPosition = new Vector2(-208f, 310f);
        public Vector2 actionPromptSize = new Vector2(430f, 44f);
        public Vector2 controlRingPosition = new Vector2(-154f, 24f);
        public float controlRingSize = 92f;
        public float controlButtonSize = 78f;
        public Vector2 endRingPosition = new Vector2(-42f, 18f);
        public float endRingSize = 106f;
        public float endButtonSize = 90f;
        public Vector2 skipOpponentPosition = new Vector2(26f, -26f);
        public Vector2 skipOpponentSize = new Vector2(196f, 58f);
        public Vector2 manaPanelPosition = new Vector2(-34f, 140f);
        public float manaPanelHeight = 58f;
        public float manaEmptyWidth = 112f;
        public float manaBaseWidth = 82f;
        public float manaItemSpacing = 56f;
        public float manaItemWidth = 52f;
        public float manaTokenSize = 36f;

        public void CopyFrom(HudLayout other)
        {
            playerPanelPosition = other.playerPanelPosition;
            opponentPanelPosition = other.opponentPanelPosition;
            playerPanelSize = other.playerPanelSize;
            opponentPanelSize = other.opponentPanelSize;
            zoneButtonSize = other.zoneButtonSize;
            playerDeckPosition = other.playerDeckPosition;
            playerGraveyardPosition = other.playerGraveyardPosition;
            playerExilePosition = other.playerExilePosition;
            opponentDeckPosition = other.opponentDeckPosition;
            opponentGraveyardPosition = other.opponentGraveyardPosition;
            opponentExilePosition = other.opponentExilePosition;
            phaseRingPosition = other.phaseRingPosition;
            phaseRingSize = other.phaseRingSize;
            phaseButtonSize = other.phaseButtonSize;
            actionPromptPosition = other.actionPromptPosition;
            actionPromptSize = other.actionPromptSize;
            controlRingPosition = other.controlRingPosition;
            controlRingSize = other.controlRingSize;
            controlButtonSize = other.controlButtonSize;
            endRingPosition = other.endRingPosition;
            endRingSize = other.endRingSize;
            endButtonSize = other.endButtonSize;
            skipOpponentPosition = other.skipOpponentPosition;
            skipOpponentSize = other.skipOpponentSize;
            manaPanelPosition = other.manaPanelPosition;
            manaPanelHeight = other.manaPanelHeight;
            manaEmptyWidth = other.manaEmptyWidth;
            manaBaseWidth = other.manaBaseWidth;
            manaItemSpacing = other.manaItemSpacing;
            manaItemWidth = other.manaItemWidth;
            manaTokenSize = other.manaTokenSize;
        }

        public void Sanitize()
        {
            playerPanelSize = Positive(playerPanelSize, 1f);
            opponentPanelSize = Positive(opponentPanelSize, 1f);
            zoneButtonSize = Positive(zoneButtonSize, 1f);
            phaseRingSize = Mathf.Max(1f, phaseRingSize);
            phaseButtonSize = Mathf.Max(1f, phaseButtonSize);
            actionPromptSize = Positive(actionPromptSize, 1f);
            controlRingSize = Mathf.Max(1f, controlRingSize);
            controlButtonSize = Mathf.Max(1f, controlButtonSize);
            endRingSize = Mathf.Max(1f, endRingSize);
            endButtonSize = Mathf.Max(1f, endButtonSize);
            skipOpponentSize = Positive(skipOpponentSize, 1f);
            manaPanelHeight = Mathf.Max(1f, manaPanelHeight);
            manaEmptyWidth = Mathf.Max(1f, manaEmptyWidth);
            manaBaseWidth = Mathf.Max(1f, manaBaseWidth);
            manaItemSpacing = Mathf.Max(1f, manaItemSpacing);
            manaItemWidth = Mathf.Max(1f, manaItemWidth);
            manaTokenSize = Mathf.Max(1f, manaTokenSize);
        }
    }

    private static Vector2 Positive(Vector2 value, float minimum)
    {
        return new Vector2(Mathf.Max(minimum, value.x), Mathf.Max(minimum, value.y));
    }

    private static Vector3 Positive(Vector3 value, float minimum)
    {
        return new Vector3(Mathf.Max(minimum, value.x), Mathf.Max(minimum, value.y), Mathf.Max(minimum, value.z));
    }
}
