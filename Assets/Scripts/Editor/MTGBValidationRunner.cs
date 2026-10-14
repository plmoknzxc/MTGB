#if UNITY_EDITOR
using System;
using System.IO;
using Unity.Pipeline.Commands;
using UnityEditor;
using UnityEngine;

public static class MTGBValidationRunner
{
    private static bool running, exitWhenDone;
    private static int stage;
    private static double deadline;
    public static void ScheduleForAutomation() => Begin(false);
    [MenuItem("MTGB/验证/基础运行检查")]
    public static void ValidateFromMenu() => Begin(false);
    [CliCommand("mtgb_validate_project", "Validate Forge catalog, persistent session and process isolation")]
    public static void ValidateFromCli() => Begin(false);
    public static void ValidateFromCommandLine() => Begin(true);
    private static void Begin(bool exit)
    {
        if (running) throw new InvalidOperationException("Project validation is running.");
        running = true; exitWhenDone = exit;
        Directory.CreateDirectory("Temp"); File.WriteAllText("Temp/project-validation-result.txt", "RUNNING");
        try
        {
            ForgeMigrationValidation.ValidateCatalog();
            ForgeMigrationValidation.ValidateSession();
            stage = 1; deadline = EditorApplication.timeSinceStartup + 180;
            EditorApplication.update += Tick;
        }
        catch (Exception error) { Finish(error); }
    }
    private static void Tick()
    {
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException("Forge validation timed out.");
            if (stage == 1 && !ForgeMigrationValidation.IsRunning)
            {
                if (ForgeMigrationValidation.LastError != null) throw ForgeMigrationValidation.LastError;
                ForgeIsolationValidation.Run(); stage = 2;
            }
            else if (stage == 2 && !ForgeIsolationValidation.IsRunning)
            {
                if (ForgeIsolationValidation.LastError != null) throw ForgeIsolationValidation.LastError;
                Finish(null);
            }
        }
        catch (Exception error) { Finish(error); }
    }
    private static void Finish(Exception error)
    {
        EditorApplication.update -= Tick; running = false;
        File.WriteAllText("Temp/project-validation-result.txt", error?.ToString() ?? "PASS");
        if (error != null) Debug.LogException(error);
        else Debug.Log("MTGB Forge project validation passed. Run Windows network smoke separately.");
        if (exitWhenDone) EditorApplication.Exit(error == null ? 0 : 1);
    }
}
#endif
