#if UNITY_EDITOR
using System;
using Unity.Pipeline.Commands;
using UnityEditor;
using UnityEngine;

public static class MTGBValidationRunner
{
    [MenuItem("MTGB/Validate Project")]
    public static void ValidateFromMenu()
    {
        ValidateAll();
        Debug.Log("MTGB project validation passed.");
    }

    [CliCommand("mtgb_validate_project", "Run all MTGB project validation suites")]
    public static void ValidateFromCli()
    {
        ValidateFromMenu();
    }

    public static void ValidateFromCommandLine()
    {
        try
        {
            ValidateAll();
            Debug.Log("MTGB project validation passed.");
            EditorApplication.Exit(0);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }

    private static void ValidateAll()
    {
        CardDatabaseValidationMenu.ValidateForAutomation();
        GameChoiceValidationMenu.ValidateForAutomation();
        GameDeterminismValidationMenu.ValidateForAutomation();
        OnlineProtocolValidationMenu.ValidateForAutomation();
    }
}
#endif
