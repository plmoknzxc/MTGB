using System;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 无头校验 TST 测试卡脚本：逐张编译 Lua 并执行 OnInit，报告语法/运行错误。
/// 运行：Unity -batchmode -quit -executeMethod TestCardScriptValidationMenu.RunFromCommandLine
/// </summary>
public static class TestCardScriptValidationMenu
{
    [MenuItem("MTGB/Validate Test Card Scripts")]
    public static void ValidateFromMenu() => Validate();

    public static void RunFromCommandLine()
    {
        int failures = Validate();
        if (failures > 0) throw new InvalidOperationException($"{failures} test card script(s) failed validation.");
        Debug.Log("[MTGB] all TST test card scripts validated.");
    }

    private static int Validate()
    {
        string root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        string scriptDir = Path.Combine(root, "CardScripts", "TST");
        if (!Directory.Exists(scriptDir))
        {
            Debug.LogWarning("[MTGB] no TST script directory; nothing to validate.");
            return 0;
        }

        int failures = 0;
        string[] files = Directory.GetFiles(scriptDir, "*.lua");
        foreach (string file in files)
        {
            string relative = $"CardScripts/TST/{Path.GetFileName(file)}";
            try
            {
                string code = File.ReadAllText(file);
                var definition = new CardDefinition
                {
                    cardName = Path.GetFileNameWithoutExtension(file),
                    cardID = 0,
                    setCode = "TST",
                    collectorNumberText = "0",
                    luaScript = relative
                };
                var card = new RuntimeCard(definition, 1, initializeScript: false);
                using (var runtime = new LuaCSharpCardScriptRuntime())
                using (ICardScriptInstance instance = runtime.CreateCardInstance(card, code))
                {
                    if (instance == null) throw new InvalidOperationException("instance creation returned null");
                }
                Debug.Log($"[MTGB] OK  {relative}");
            }
            catch (Exception exception)
            {
                failures++;
                Debug.LogError($"[MTGB] FAIL {relative}: {exception.Message}");
            }
        }

        Debug.Log($"[MTGB] TST script validation: {files.Length - failures}/{files.Length} passed");
        return failures;
    }
}
