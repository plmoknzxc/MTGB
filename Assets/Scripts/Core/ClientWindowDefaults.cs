using UnityEngine;

public static class ClientWindowDefaults
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSplashScreen)]
    private static void Initialize()
    {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        if (Application.isBatchMode) return;
        // Older releases saved fullscreen preferences; always start in a normal window.
        Screen.SetResolution(1280, 720, FullScreenMode.Windowed);
#endif
    }
}
