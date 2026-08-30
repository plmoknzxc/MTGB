using System;
using UnityEngine;

public sealed class CardScriptRuntimeManager : MonoBehaviour
{
    public static CardScriptRuntimeManager Instance { get; private set; }

    public ICardScriptRuntime Runtime { get; private set; }

    public static CardScriptRuntimeManager EnsureExists()
    {
        if (Instance != null)
        {
            Instance.ConfigureLifetime();
            Instance.EnsureRuntime();
            Instance.RemoveDuplicateInstances();
            return Instance;
        }

        CardScriptRuntimeManager[] existingManagers =
            Resources.FindObjectsOfTypeAll<CardScriptRuntimeManager>();
        for (int i = 0; i < existingManagers.Length; i++)
        {
            CardScriptRuntimeManager manager = existingManagers[i];
            if (!IsRuntimeInstance(manager)) continue;

            Instance = manager;
            manager.ConfigureLifetime();
            manager.EnsureRuntime();
            manager.RemoveDuplicateInstances(existingManagers);
            return manager;
        }

        GameObject scriptSystem = new GameObject("CardScriptRuntimeManager");
        if (!Application.isPlaying)
        {
            scriptSystem.hideFlags = HideFlags.HideAndDontSave;
        }

        return scriptSystem.AddComponent<CardScriptRuntimeManager>();
    }

    public static IDisposable UseRules(GameRules rules)
    {
        return CardScriptApi.UseRules(rules);
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            DestroyManagerObject(gameObject);
            return;
        }

        Instance = this;
        ConfigureLifetime();
        EnsureRuntime();
        RemoveDuplicateInstances();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;

        Runtime?.Dispose();
        Runtime = null;
    }

    private void ConfigureLifetime()
    {
        if (Application.isPlaying)
        {
            gameObject.hideFlags = HideFlags.None;
            DontDestroyOnLoad(gameObject);
            return;
        }

        gameObject.hideFlags = HideFlags.HideAndDontSave;
    }

    private void EnsureRuntime()
    {
        if (Runtime != null) return;

        Runtime = new LuaCSharpCardScriptRuntime();
        Debug.Log("Lua-CSharp card script runtime initialized.");
    }

    private void RemoveDuplicateInstances(
        CardScriptRuntimeManager[] existingManagers = null)
    {
        CardScriptRuntimeManager[] managers = existingManagers
            ?? Resources.FindObjectsOfTypeAll<CardScriptRuntimeManager>();

        for (int i = 0; i < managers.Length; i++)
        {
            CardScriptRuntimeManager manager = managers[i];
            if (manager == null
                || manager == this
                || !IsRuntimeInstance(manager))
            {
                continue;
            }

            DestroyManagerObject(manager.gameObject);
        }
    }

    private static bool IsRuntimeInstance(CardScriptRuntimeManager manager)
    {
        if (manager == null) return false;

        GameObject target = manager.gameObject;
        return target.scene.IsValid()
            || (target.hideFlags & HideFlags.DontSave) != 0;
    }

    private static void DestroyManagerObject(GameObject target)
    {
        if (target == null) return;

        if (Application.isPlaying)
        {
            Destroy(target);
        }
        else
        {
            DestroyImmediate(target);
        }
    }

    public ICardScriptInstance CreateCardInstance(RuntimeCard card, string scriptCode)
    {
        return Runtime?.CreateCardInstance(card, scriptCode);
    }

    public void Invoke(ICardScriptInstance instance, string functionName)
    {
        Runtime?.Invoke(instance, functionName);
    }

    public bool InvokeBoolean(ICardScriptInstance instance, string functionName, bool defaultValue)
    {
        return Runtime != null
            ? Runtime.InvokeBoolean(instance, functionName, defaultValue)
            : defaultValue;
    }
}
