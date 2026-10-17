using UnityEngine;
using UnityEngine.UI;

// uGUI regenerates Text on canvas scaling without invalidating InputField's
// separate caret mesh. Synchronize after layout, retaining focus and selection.
[RequireComponent(typeof(InputField))]
public sealed class InputFieldLayoutSync : MonoBehaviour
{
    InputField input;
    bool current;
    Rect rect;
    Matrix4x4 matrix;
    Font font;
    int fontSize, screenWidth, screenHeight;
    float pixelsPerUnit, canvasScale;

    void OnEnable()
    {
        input = GetComponent<InputField>();
        current = false;
        Canvas.willRenderCanvases += Synchronize;
    }

    void OnDisable()
    {
        Canvas.willRenderCanvases -= Synchronize;
        current = false;
    }

    void Synchronize()
    {
        if (input == null || !input.isFocused || input.textComponent == null)
        {
            current = false;
            return;
        }
        var text = input.textComponent;
        var transform = text.rectTransform;
        float scale = text.canvas != null ? text.canvas.scaleFactor : 1f;
        if (current && rect == transform.rect && matrix == transform.localToWorldMatrix
            && font == text.font && fontSize == text.fontSize && pixelsPerUnit == text.pixelsPerUnit
            && canvasScale == scale && screenWidth == Screen.width && screenHeight == Screen.height) return;

        rect = transform.rect;
        matrix = transform.localToWorldMatrix;
        font = text.font;
        fontSize = text.fontSize;
        pixelsPerUnit = text.pixelsPerUnit;
        canvasScale = scale;
        screenWidth = Screen.width;
        screenHeight = Screen.height;
        current = true;
        input.ForceLabelUpdate();
        input.Rebuild(CanvasUpdate.LatePreRender);
    }
}
