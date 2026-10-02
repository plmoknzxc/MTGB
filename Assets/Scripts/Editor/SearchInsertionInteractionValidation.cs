using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public static class SearchInsertionInteractionValidation
{
    // Run in Play mode. Uses the real input module's press/drag/release routing.
    [MenuItem("MTGB/验证/卡牌/搜索插入交互（运行模式）")]
    public static void Run()
    {
        if (!Application.isPlaying) throw new InvalidOperationException("Run this validation in Play mode.");
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var panel = FindPanel();
        if (panel == null)
        {
            var main = UnityEngine.Object.FindFirstObjectByType<MainMenuController>();
            typeof(MainMenuController).GetMethod("ShowDeckBuilder", flags).Invoke(main, null);
            typeof(MainMenuController).GetMethod("OnAdvancedSearch", flags).Invoke(main, null);
            panel = FindPanel();
        }
        var footer = panel.transform.Find("SearchPanel").GetComponentsInChildren<Button>()
            .Where(b => b.name == "查看结果" || b.name == "重置条件" || b.name == "取消")
            .OrderBy(b => ((RectTransform)b.transform).anchoredPosition.x).Select(b => b.name).ToArray();
        Check(string.Join(",", footer) == "查看结果,重置条件,取消", "Footer order");
        foreach (var existing in panel.GetComponentsInChildren<SearchInsertionMenu>()) existing.Close();
        foreach (bool mana in new[] { true, false })
        {
            var input = panel.GetComponentsInChildren<InputField>().First(i => i.name.Contains(mana ? "费用符号" : "类别与"));
            var menu = input.GetComponent<SearchInsertionMenu>(); menu.Open();
            var popup = panel.transform.Find("InsertionPopup");
            popup.GetComponentsInChildren<Button>().First(b => b.name == (mana ? "通用费用" : "生物副类别")).onClick.Invoke();
            Canvas.ForceUpdateCanvases();
            var bar = popup.Find("Menu/ScrollTrack").GetComponent<Scrollbar>();
            Gesture(bar.handleRect.gameObject, new Vector2(0, -70));
            Check(menu.IsOpen && bar.value < .99f, "Scrollbar drag/release must scroll and keep menu open");
            Gesture(popup.Find("Menu/Options").gameObject, new Vector2(0, 35));
            Check(menu.IsOpen, "List drag/release must keep menu open");
            Gesture(popup.Find("Menu").gameObject, Vector2.zero);
            Check(menu.IsOpen, "Interior click must keep menu open");
            Gesture(popup.Find("Dismiss").gameObject, Vector2.zero);
            Check(!menu.IsOpen, "Outside click must close menu");
            // Destroy is deferred; detach the closed popup so the next iteration finds the new one.
            popup.SetParent(null, false);
        }
        SessionState.SetString("SearchInsertion.Interaction", "PASS: footer order; mana/type scrollbar and list drag/release; interior/outside clicks");
    }
    static AdvancedSearchPanel FindPanel() => Resources.FindObjectsOfTypeAll<AdvancedSearchPanel>()
        .FirstOrDefault(p => p.gameObject.scene.IsValid() && p.gameObject.activeInHierarchy);
    static void Check(bool condition, string description)
    {
        if (!condition) throw new Exception(description);
    }
    static void Gesture(GameObject target, Vector2 delta)
    {
        var system = EventSystem.current;
        var module = system.GetComponent<StandaloneInputModule>();
        const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;
        var press = typeof(StandaloneInputModule).GetMethod("ProcessMousePress", flags);
        var drag = typeof(PointerInputModule).GetMethod("ProcessDrag", flags);
        var rect = (RectTransform)target.transform;
        var start = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center));
        var pointer = new PointerEventData(system) {
            button = PointerEventData.InputButton.Left, position = start, pointerId = -1,
            pointerCurrentRaycast = new RaycastResult { gameObject = target, screenPosition = start, module = target.GetComponentInParent<GraphicRaycaster>() }
        };
        var data = new PointerInputModule.MouseButtonEventData { buttonData = pointer, buttonState = PointerEventData.FramePressState.Pressed };
        press.Invoke(module, new object[] { data });
        if (delta != Vector2.zero)
        {
            pointer.delta = delta; pointer.position += delta;
            drag.Invoke(module, new object[] { pointer });
        }
        data.buttonState = PointerEventData.FramePressState.Released;
        press.Invoke(module, new object[] { data });
    }
}
