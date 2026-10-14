#if UNITY_EDITOR
// Narrow entry: public-hand browsing and transitions only.
public static class KnownHandBrowserPresentationValidation {
    public static void Run()=>ResolutionDockPresentationValidation.RunHandBrowserOnly();
}
#endif
