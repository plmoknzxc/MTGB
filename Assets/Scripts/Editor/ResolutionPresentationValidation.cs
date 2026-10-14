#if UNITY_EDITOR
// Historical entry points forward to the current dock/known-zone acceptance.
public static class ResolutionPresentationValidation {
    public static void Run()=>ResolutionDockPresentationValidation.Run();
    public static void RunExploreOnly()=>ResolutionDockPresentationValidation.Run();
}
#endif
