<script setup lang="ts">
import { computed, onMounted, reactive, ref } from "vue";
import { view } from "@locus/view-runtime";

type LayoutData = Record<string, any>;

interface ApiResponse {
  ok: boolean;
  message: string;
  json: string;
}

interface FieldDef {
  label: string;
  path: string;
  min: number;
  max: number;
  step: number;
}

interface FieldGroup {
  title: string;
  note?: string;
  fields: FieldDef[];
}

interface LayoutTab {
  id: string;
  label: string;
  description: string;
  groups: FieldGroup[];
}

const defaults: LayoutData = {
  hand: {
    playerRootY: 0, playerRootHeight: 292, playerBaseY: 26, playerArcDepth: 38,
    playerMaxSpacing: 132, playerTotalWidth: 1540, playerRotationPerOffset: -3.65,
    playerCardWidth: 194, playerCardHeight: 274, playerMinScale: 0.6,
    opponentRootY: 0, opponentRootHeight: 86, opponentBaseY: 76, opponentArcDepth: 5,
    opponentMaxSpacing: 70, opponentTotalWidth: 780, opponentRotationPerOffset: 1.65,
    opponentCardWidth: 74, opponentCardHeight: 104, opponentMinScale: 0.62,
    fullScaleCardCount: 7,
  },
  battlefield: {
    playerDropAreaPosition: { x: 0, y: 0.08, z: -1.62 }, playerDropAreaScale: { x: 14, y: 0.08, z: 3.5 },
    opponentDropAreaPosition: { x: 0, y: 0.08, z: 1.62 }, opponentDropAreaScale: { x: 14, y: 0.08, z: 3.5 },
    playerLandRowZ: -2.48, playerFrontRowZ: -1.18, opponentLandRowZ: 2.48, opponentFrontRowZ: 1.18,
    landRowY: 0.205, frontRowY: 0.255, rowYStep: 0.003, landSpacing: 0.78, frontSpacing: 0.92,
    rowWidth: 12.1, minimumScale: 0.42, creatureScale: { x: 0.58, y: 0.04, z: 0.8 },
    permanentScale: { x: 0.53, y: 0.036, z: 0.73 }, attackOffsetZ: 0.46,
    idleYawPerCard: -0.85, idleYawLimit: 5.5, blockerPlayerZ: -0.18, blockerOpponentZ: 0.18,
    blockerBaseY: 0.285, blockerSpacingX: 0.48, blockerLayerY: 0.006,
  },
  stack: {
    panelPosition: { x: -34, y: 74 }, panelSize: { x: 430, y: 540 }, cardSize: { x: 220, y: 310 },
    topCardPosition: { x: -106, y: 24 }, depthOffset: { x: -46, y: 22 }, firstBackRotation: -8,
    rotationStep: -6, maxVisibleCards: 4, inspectThreshold: 3,
    inspectButtonPosition: { x: -4, y: 74 }, inspectButtonSize: { x: 72, y: 44 },
    targetPanelPosition: { x: 0, y: 318 }, targetPanelSize: { x: 470, y: 86 },
    cancelButtonPosition: { x: -276, y: 18 }, cancelButtonSize: { x: 106, y: 76 },
    previewPanelPosition: { x: 28, y: 0 }, previewPanelSize: { x: 382, y: 546 },
    previewCardSize: { x: 334, y: 472 }, browserViewportPosition: { x: 0, y: 8 },
    browserViewportSize: { x: 1320, y: 390 }, browserCardSize: { x: 226, y: 320 },
    browserCardSpacing: 184, browserMargin: 54, browserContentHeight: 360,
    browserClosePosition: { x: 0, y: 52 }, browserCloseSize: { x: 280, y: 70 },
    browserScrollbarPosition: { x: 0, y: 136 }, browserScrollbarSize: { x: 980, y: 18 },
    targetArrowWidth: 5, targetArrowHeadSize: 30,
  },
  hud: {
    playerPanelPosition: { x: 28, y: 24 }, opponentPanelPosition: { x: -28, y: -24 },
    playerPanelSize: { x: 420, y: 116 }, opponentPanelSize: { x: 420, y: 116 }, zoneButtonSize: { x: 82, y: 112 },
    playerDeckPosition: { x: 116, y: 150 }, playerGraveyardPosition: { x: 206, y: 150 }, playerExilePosition: { x: 296, y: 150 },
    opponentDeckPosition: { x: -468, y: -84 }, opponentGraveyardPosition: { x: -378, y: -84 }, opponentExilePosition: { x: -288, y: -84 },
    phaseRingPosition: { x: -34, y: 216 }, phaseRingSize: 174, phaseButtonSize: 150,
    actionPromptPosition: { x: -208, y: 310 }, actionPromptSize: { x: 430, y: 44 },
    controlRingPosition: { x: -154, y: 24 }, controlRingSize: 92, controlButtonSize: 78,
    endRingPosition: { x: -42, y: 18 }, endRingSize: 106, endButtonSize: 90,
    skipOpponentPosition: { x: 26, y: -26 }, skipOpponentSize: { x: 196, y: 58 },
    manaPanelPosition: { x: -34, y: 140 }, manaPanelHeight: 58, manaEmptyWidth: 112,
    manaBaseWidth: 82, manaItemSpacing: 56, manaItemWidth: 52, manaTokenSize: 36,
  },
};

const numberField = (label: string, path: string, min: number, max: number, step = 1): FieldDef => ({ label, path, min, max, step });
const vector2Fields = (label: string, path: string, min: number, max: number, step = 1): FieldDef[] => [
  numberField(`${label} X`, `${path}.x`, min, max, step), numberField(`${label} Y`, `${path}.y`, min, max, step),
];
const vector3Fields = (label: string, path: string, min: number, max: number, step = 0.01): FieldDef[] => [
  numberField(`${label} X`, `${path}.x`, min, max, step), numberField(`${label} Y`, `${path}.y`, min, max, step),
  numberField(`${label} Z`, `${path}.z`, min, max, step),
];

const tabs: LayoutTab[] = [
  {
    id: "hand", label: "双方手牌", description: "调整玩家和对手手牌根区域、扇形、间距与卡牌尺寸。",
    groups: [
      { title: "玩家手牌", fields: [
        numberField("根区域 Y", "hand.playerRootY", -300, 300), numberField("根区域高度", "hand.playerRootHeight", 40, 600),
        numberField("卡牌基准 Y", "hand.playerBaseY", -200, 300), numberField("扇形弧度", "hand.playerArcDepth", 0, 180),
        numberField("最大间距", "hand.playerMaxSpacing", 10, 300), numberField("最大总宽度", "hand.playerTotalWidth", 100, 2400, 10),
        numberField("每张旋转角", "hand.playerRotationPerOffset", -15, 15, 0.05),
        numberField("卡牌宽度", "hand.playerCardWidth", 20, 500), numberField("卡牌高度", "hand.playerCardHeight", 30, 700),
        numberField("最小缩放", "hand.playerMinScale", 0.1, 1, 0.01),
      ] },
      { title: "对手手牌", fields: [
        numberField("根区域 Y", "hand.opponentRootY", -300, 300), numberField("根区域高度", "hand.opponentRootHeight", 20, 400),
        numberField("卡牌基准 Y", "hand.opponentBaseY", -100, 300), numberField("扇形弧度", "hand.opponentArcDepth", 0, 120),
        numberField("最大间距", "hand.opponentMaxSpacing", 10, 220), numberField("最大总宽度", "hand.opponentTotalWidth", 100, 1800, 10),
        numberField("每张旋转角", "hand.opponentRotationPerOffset", -15, 15, 0.05),
        numberField("卡牌宽度", "hand.opponentCardWidth", 20, 300), numberField("卡牌高度", "hand.opponentCardHeight", 30, 420),
        numberField("最小缩放", "hand.opponentMinScale", 0.1, 1, 0.01), numberField("满尺寸张数", "hand.fullScaleCardCount", 1, 20),
      ] },
    ],
  },
  {
    id: "battlefield", label: "场上卡牌", description: "这里使用 ArenaWhitebox 的局部世界坐标；Z 控制上下位置，X/Z 缩放控制卡面宽高。",
    groups: [
      { title: "可投放战场区域", fields: [
        ...vector3Fields("玩家区域位置", "battlefield.playerDropAreaPosition", -20, 20),
        ...vector3Fields("玩家区域缩放", "battlefield.playerDropAreaScale", 0.01, 24),
        ...vector3Fields("对手区域位置", "battlefield.opponentDropAreaPosition", -20, 20),
        ...vector3Fields("对手区域缩放", "battlefield.opponentDropAreaScale", 0.01, 24),
      ] },
      { title: "双方卡牌行", fields: [
        numberField("玩家地牌行 Z", "battlefield.playerLandRowZ", -5, 5, 0.01), numberField("玩家前排 Z", "battlefield.playerFrontRowZ", -5, 5, 0.01),
        numberField("对手地牌行 Z", "battlefield.opponentLandRowZ", -5, 5, 0.01), numberField("对手前排 Z", "battlefield.opponentFrontRowZ", -5, 5, 0.01),
        numberField("地牌行高度 Y", "battlefield.landRowY", 0, 2, 0.001), numberField("前排行高度 Y", "battlefield.frontRowY", 0, 2, 0.001),
        numberField("卡牌层叠高度", "battlefield.rowYStep", 0, 0.05, 0.001), numberField("地牌间距", "battlefield.landSpacing", 0.05, 3, 0.01),
        numberField("前排间距", "battlefield.frontSpacing", 0.05, 3, 0.01), numberField("单行最大宽度", "battlefield.rowWidth", 1, 20, 0.1),
        numberField("拥挤时最小缩放", "battlefield.minimumScale", 0.1, 1, 0.01),
      ] },
      { title: "场上卡牌尺寸与状态", fields: [
        ...vector3Fields("生物卡缩放", "battlefield.creatureScale", 0.001, 2, 0.001),
        ...vector3Fields("其他永久物缩放", "battlefield.permanentScale", 0.001, 2, 0.001),
        numberField("攻击前移距离", "battlefield.attackOffsetZ", 0, 2, 0.01),
        numberField("每张待机旋转", "battlefield.idleYawPerCard", -10, 10, 0.05), numberField("待机旋转上限", "battlefield.idleYawLimit", 0, 30, 0.1),
      ] },
      { title: "阻挡布局", fields: [
        numberField("玩家阻挡位置 Z", "battlefield.blockerPlayerZ", -3, 3, 0.01), numberField("对手阻挡位置 Z", "battlefield.blockerOpponentZ", -3, 3, 0.01),
        numberField("阻挡卡高度 Y", "battlefield.blockerBaseY", 0, 2, 0.001), numberField("多阻挡横向间距", "battlefield.blockerSpacingX", 0, 2, 0.01),
        numberField("多阻挡层叠高度", "battlefield.blockerLayerY", 0, 0.1, 0.001),
      ] },
    ],
  },
  {
    id: "stack", label: "堆叠与施放", description: "调整右侧堆叠牌、施法确认、目标选择和堆叠预览。屏幕坐标按各自锚点计算。",
    groups: [
      { title: "堆叠牌堆", fields: [
        ...vector2Fields("面板位置", "stack.panelPosition", -1200, 1200), ...vector2Fields("面板尺寸", "stack.panelSize", 20, 1600),
        ...vector2Fields("堆叠卡尺寸", "stack.cardSize", 20, 800), ...vector2Fields("顶牌位置", "stack.topCardPosition", -800, 800),
        ...vector2Fields("每层偏移", "stack.depthOffset", -300, 300), numberField("第一张背牌旋转", "stack.firstBackRotation", -30, 30, 0.1),
        numberField("后续旋转步长", "stack.rotationStep", -30, 30, 0.1), numberField("最多显示张数", "stack.maxVisibleCards", 1, 12),
        numberField("显示展开按钮阈值", "stack.inspectThreshold", 1, 20),
        ...vector2Fields("展开按钮位置", "stack.inspectButtonPosition", -600, 600), ...vector2Fields("展开按钮尺寸", "stack.inspectButtonSize", 10, 400),
      ] },
      { title: "施放与目标选择", fields: [
        ...vector2Fields("目标提示位置", "stack.targetPanelPosition", -1000, 1000), ...vector2Fields("目标提示尺寸", "stack.targetPanelSize", 20, 1200),
        ...vector2Fields("取消按钮位置", "stack.cancelButtonPosition", -1000, 1000), ...vector2Fields("取消按钮尺寸", "stack.cancelButtonSize", 20, 500),
        numberField("目标箭头宽度", "stack.targetArrowWidth", 1, 30, 0.5), numberField("目标箭头头部尺寸", "stack.targetArrowHeadSize", 5, 120),
      ] },
      { title: "堆叠卡右键预览", fields: [
        ...vector2Fields("预览面板位置", "stack.previewPanelPosition", -1000, 1000), ...vector2Fields("预览面板尺寸", "stack.previewPanelSize", 20, 1200),
        ...vector2Fields("预览卡尺寸", "stack.previewCardSize", 20, 1000),
      ] },
    ],
  },
  {
    id: "browser", label: "区域浏览器", description: "堆叠、牌库、坟场和放逐区共用这个全屏卡牌浏览器。",
    groups: [
      { title: "浏览区域", fields: [
        ...vector2Fields("可视区域位置", "stack.browserViewportPosition", -800, 800), ...vector2Fields("可视区域尺寸", "stack.browserViewportSize", 100, 2200),
        ...vector2Fields("浏览卡尺寸", "stack.browserCardSize", 20, 800), numberField("卡牌水平间距", "stack.browserCardSpacing", 20, 600),
        numberField("左右边距", "stack.browserMargin", 0, 400), numberField("内容区域高度", "stack.browserContentHeight", 20, 1000),
      ] },
      { title: "完成按钮与滚动条", fields: [
        ...vector2Fields("完成按钮位置", "stack.browserClosePosition", -800, 800), ...vector2Fields("完成按钮尺寸", "stack.browserCloseSize", 20, 800),
        ...vector2Fields("滚动条位置", "stack.browserScrollbarPosition", -800, 800), ...vector2Fields("滚动条尺寸", "stack.browserScrollbarSize", 10, 1800),
      ] },
    ],
  },
  {
    id: "players", label: "玩家信息与区域", description: "调整双方玩家信息面板，以及牌库、坟场、放逐区按钮。玩家对象从左下角定位，对手对象从右上角定位。",
    groups: [
      { title: "玩家信息面板", fields: [
        ...vector2Fields("玩家面板位置", "hud.playerPanelPosition", -1200, 1200), ...vector2Fields("玩家面板尺寸", "hud.playerPanelSize", 20, 1200),
        ...vector2Fields("对手面板位置", "hud.opponentPanelPosition", -1200, 1200), ...vector2Fields("对手面板尺寸", "hud.opponentPanelSize", 20, 1200),
      ] },
      { title: "牌库 / 坟场 / 放逐", fields: [
        ...vector2Fields("区域按钮尺寸", "hud.zoneButtonSize", 20, 400),
        ...vector2Fields("玩家牌库位置", "hud.playerDeckPosition", -1200, 1200), ...vector2Fields("玩家坟场位置", "hud.playerGraveyardPosition", -1200, 1200),
        ...vector2Fields("玩家放逐区位置", "hud.playerExilePosition", -1200, 1200), ...vector2Fields("对手牌库位置", "hud.opponentDeckPosition", -1200, 1200),
        ...vector2Fields("对手坟场位置", "hud.opponentGraveyardPosition", -1200, 1200), ...vector2Fields("对手放逐区位置", "hud.opponentExilePosition", -1200, 1200),
      ] },
    ],
  },
  {
    id: "hud", label: "阶段与操作 UI", description: "调整右下阶段环、确认按钮、自动/全控、操作提示、跳过回合和法术力面板。",
    groups: [
      { title: "阶段与操作提示", fields: [
        ...vector2Fields("阶段环位置", "hud.phaseRingPosition", -1200, 1200), numberField("阶段环尺寸", "hud.phaseRingSize", 20, 500),
        numberField("阶段按钮尺寸", "hud.phaseButtonSize", 20, 480), ...vector2Fields("操作提示位置", "hud.actionPromptPosition", -1200, 1200),
        ...vector2Fields("操作提示尺寸", "hud.actionPromptSize", 20, 1200),
      ] },
      { title: "自动 / 确认 / 跳过", fields: [
        ...vector2Fields("自动控制环位置", "hud.controlRingPosition", -1200, 1200), numberField("自动控制环尺寸", "hud.controlRingSize", 20, 400),
        numberField("自动控制按钮尺寸", "hud.controlButtonSize", 20, 380), ...vector2Fields("确认按钮环位置", "hud.endRingPosition", -1200, 1200),
        numberField("确认按钮环尺寸", "hud.endRingSize", 20, 500), numberField("确认按钮尺寸", "hud.endButtonSize", 20, 480),
        ...vector2Fields("跳过对手回合位置", "hud.skipOpponentPosition", -1200, 1200), ...vector2Fields("跳过对手回合尺寸", "hud.skipOpponentSize", 20, 800),
      ] },
      { title: "法术力面板", fields: [
        ...vector2Fields("法术力面板位置", "hud.manaPanelPosition", -1200, 1200), numberField("面板高度", "hud.manaPanelHeight", 20, 300),
        numberField("空法术力宽度", "hud.manaEmptyWidth", 20, 600), numberField("基础宽度", "hud.manaBaseWidth", 20, 600),
        numberField("法术力项目间距", "hud.manaItemSpacing", 10, 240), numberField("法术力项目宽度", "hud.manaItemWidth", 10, 220),
        numberField("法术力图标尺寸", "hud.manaTokenSize", 8, 180),
      ] },
    ],
  },
];

const layout = reactive<LayoutData>(clone(defaults));
const activeTabId = ref(tabs[0].id);
const loading = ref(false);
const statusText = ref("等待读取");
const errorText = ref("");
const autoApply = ref(true);
const saveScene = ref(false);
const undoStack = ref<LayoutData[]>([]);
const redoStack = ref<LayoutData[]>([]);
const activeEditPath = ref("");
const canUndo = computed(() => undoStack.value.length > 0 && !loading.value);
const canRedo = computed(() => redoStack.value.length > 0 && !loading.value);
const isApplying = ref(false);
const applyQueued = ref(false);
let applyTimer: number | undefined;

const activeTab = computed(() => tabs.find((tab) => tab.id === activeTabId.value) ?? tabs[0]);
const statusClass = computed(() => errorText.value ? "bad" : loading.value ? "busy" : "ok");

function clone<T>(value: T): T {
  return JSON.parse(JSON.stringify(value));
}

function deepMerge(base: LayoutData, next: LayoutData): LayoutData {
  const result = clone(base);
  for (const [key, value] of Object.entries(next ?? {})) {
    if (value && typeof value === "object" && !Array.isArray(value)) result[key] = deepMerge(result[key] ?? {}, value);
    else result[key] = value;
  }
  return result;
}

function assignLayout(next: LayoutData) {
  const merged = deepMerge(defaults, next ?? {});
  for (const key of Object.keys(layout)) delete layout[key];
  Object.assign(layout, merged);
}

function getNumber(path: string): number {
  let current: any = layout;
  for (const part of path.split(".")) current = current?.[part];
  return Number(current ?? 0);
}

function setNumber(path: string, value: number) {
  const parts = path.split(".");
  let current: any = layout;
  for (let i = 0; i < parts.length - 1; i++) current = current[parts[i]];
  current[parts[parts.length - 1]] = value;
}

function beginFieldEdit(path: string) {
  if (loading.value || activeEditPath.value === path) return;
  if (activeEditPath.value) activeEditPath.value = "";
  undoStack.value.push(clone(layout));
  redoStack.value = [];
  if (undoStack.value.length > 50) undoStack.value.shift();
  activeEditPath.value = path;
}

function endFieldEdit(path: string) {
  if (activeEditPath.value === path) activeEditPath.value = "";
}

function onFieldInput(path: string, event: Event) {
  const value = Number((event.target as HTMLInputElement).value);
  if (!Number.isFinite(value)) return;
  beginFieldEdit(path);
  setNumber(path, value);
  scheduleApply();
}

async function callApi(method: string, args?: unknown) {
  return await view.callScript("ArenaUiLayoutTunerApi", method, args ?? {}) as ApiResponse;
}

async function refresh() {
  activeEditPath.value = "";
  isApplying.value = false;
  applyQueued.value = false;
  if (applyTimer !== undefined) {
    window.clearTimeout(applyTimer);
    applyTimer = undefined;
  }
  loading.value = true;
  errorText.value = "";
  try {
    const response = await callApi("Read");
    if (!response.ok) throw new Error(response.message || "读取失败");
    assignLayout(JSON.parse(response.json || "{}"));
    undoStack.value = [];
    redoStack.value = [];
    activeEditPath.value = "";
    statusText.value = response.message || "已读取";
  } catch (error) {
    errorText.value = error instanceof Error ? error.message : String(error);
    statusText.value = "读取失败";
  } finally {
    loading.value = false;
  }
}

async function applyNow(statusOverride?: string): Promise<boolean> {
  loading.value = true;
  errorText.value = "";
  try {
    const response = await callApi("Apply", { json: JSON.stringify(layout), saveScene: saveScene.value });
    if (!response.ok) throw new Error(response.message || "应用失败");
    if (response.json) assignLayout(JSON.parse(response.json));
    statusText.value = statusOverride || response.message || "已应用";
    return true;
  } catch (error) {
    errorText.value = error instanceof Error ? error.message : String(error);
    statusText.value = "应用失败";
    return false;
  } finally {
    loading.value = false;
  }
}

function scheduleApply() {
  if (!autoApply.value) return;
  if (isApplying.value) {
    applyQueued.value = true;
    return;
  }
  if (applyTimer !== undefined) window.clearTimeout(applyTimer);
  applyTimer = window.setTimeout(async () => {
    applyTimer = undefined;
    isApplying.value = true;
    try {
      await applyNow();
    } finally {
      isApplying.value = false;
      if (applyQueued.value) {
        applyQueued.value = false;
        scheduleApply();
      }
    }
  }, 220);
}

async function undoLast() {
  const previous = undoStack.value[undoStack.value.length - 1];
  if (!previous) return;

  if (applyTimer !== undefined) {
    window.clearTimeout(applyTimer);
    applyTimer = undefined;
  }
  activeEditPath.value = "";
  const current = clone(layout);
  assignLayout(previous);
  const applied = await applyNow("已撤销上一次调整。");
  if (!applied) assignLayout(current);
  else {
    undoStack.value.pop();
    redoStack.value.push(current);
    if (redoStack.value.length > 50) redoStack.value.shift();
  }
}

async function redoLast() {
  const next = redoStack.value[redoStack.value.length - 1];
  if (!next) return;

  if (applyTimer !== undefined) {
    window.clearTimeout(applyTimer);
    applyTimer = undefined;
  }
  activeEditPath.value = "";
  const current = clone(layout);
  assignLayout(next);
  const applied = await applyNow("已重做上一次调整。");
  if (!applied) assignLayout(current);
  else {
    redoStack.value.pop();
    undoStack.value.push(current);
    if (undoStack.value.length > 50) undoStack.value.shift();
  }
}

async function resetDefaults() {
  activeEditPath.value = "";
  const previousRedo = clone(redoStack.value);
  undoStack.value.push(clone(layout));
  redoStack.value = [];
  if (undoStack.value.length > 50) undoStack.value.shift();
  if (applyTimer !== undefined) {
    window.clearTimeout(applyTimer);
    applyTimer = undefined;
  }
  loading.value = true;
  errorText.value = "";
  try {
    const response = await callApi("Reset");
    if (!response.ok) throw new Error(response.message || "重置失败");
    assignLayout(JSON.parse(response.json || "{}"));
    statusText.value = response.message || "已重置";
  } catch (error) {
    undoStack.value.pop();
    redoStack.value = previousRedo;
    errorText.value = error instanceof Error ? error.message : String(error);
    statusText.value = "重置失败";
  } finally {
    loading.value = false;
  }
}

onMounted(() => void refresh());
</script>

<template>
  <main class="view-shell" data-locus-template="blank">
    <header class="view-toolbar">
      <div class="toolbar-title">
        <span>Arena 对局 UI 布局调节器</span>
        <small :class="statusClass">{{ statusText }}</small>
      </div>
      <div class="toolbar-actions">
        <label class="inline-check"><input v-model="autoApply" type="checkbox" /> 自动应用</label>
        <label class="inline-check"><input v-model="saveScene" type="checkbox" /> 同时保存场景</label>
        <button type="button" :disabled="!canUndo" @click="undoLast()">撤销</button>
        <button type="button" :disabled="!canRedo" @click="redoLast()">重做</button>
        <button type="button" :disabled="loading" @click="refresh()">重新读取</button>
        <button class="primary" type="button" :disabled="loading" @click="applyNow()">应用并保存</button>
        <button type="button" :disabled="loading" @click="resetDefaults()">恢复默认</button>
      </div>
    </header>

    <div class="workspace">
      <nav class="section-nav">
        <button v-for="tab in tabs" :key="tab.id" type="button" :class="{ active: activeTabId === tab.id }" @click="activeEditPath = ''; activeTabId = tab.id">
          {{ tab.label }}
        </button>
        <p>建议在对局 Play Mode 中开启“自动应用”实时调节。每次拖动或输入记录一步，可用撤销/重做返回；数值会保存到布局配置资产。</p>
      </nav>

      <section class="view-content tuner-content">
        <p v-if="errorText" class="error-box">{{ errorText }}</p>
        <div class="section-heading">
          <h2>{{ activeTab.label }}</h2>
          <p>{{ activeTab.description }}</p>
        </div>

        <section v-for="group in activeTab.groups" :key="group.title" class="view-panel">
          <div class="panel-title">
            <span>{{ group.title }}</span>
            <small v-if="group.note">{{ group.note }}</small>
          </div>
          <div class="control-grid">
            <label v-for="field in group.fields" :key="field.path">
              <span>{{ field.label }}</span>
              <input type="range" :disabled="loading" :min="field.min" :max="field.max" :step="field.step" :value="getNumber(field.path)" @pointerdown="beginFieldEdit(field.path)" @input="onFieldInput(field.path, $event)" @change="endFieldEdit(field.path)" @pointercancel="endFieldEdit(field.path)" />
              <input type="number" :disabled="loading" :min="field.min" :max="field.max" :step="field.step" :value="getNumber(field.path)" @focus="beginFieldEdit(field.path)" @input="onFieldInput(field.path, $event)" @blur="endFieldEdit(field.path)" />
            </label>
          </div>
        </section>
      </section>
    </div>
  </main>
</template>

