using System;
using System.Collections.Concurrent;
using System.Reflection;
using System.Threading;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Unity 6000.3.22f1's StandardCloneWorkflow forwards threaded logs through
/// MessagingService.Broadcast, which reads main-thread-only SessionState.
/// Keep the original callback and message, but dispatch it on EditorApplication.update.
/// Remove/re-evaluate this workaround when upgrading the Editor.
/// </summary>
[InitializeOnLoad]
internal static class VirtualPlayerLogThreadFix
{
    private static readonly ConcurrentQueue<Action> Pending = new ConcurrentQueue<Action>();
    private static readonly FieldInfo ThreadedCallbacks = typeof(Application).GetField(
        "s_LogCallbackHandlerThreaded", BindingFlags.Static | BindingFlags.NonPublic);
    private static readonly int MainThreadId = Thread.CurrentThread.ManagedThreadId;
    private static double nextCheck;
    internal static int WrappedCallbacks { get; private set; }
    internal static int ForwardedMessages { get; private set; }

    static VirtualPlayerLogThreadFix()
    {
        // This is a narrow workaround for a verified engine bug, not a general log interceptor.
        if (Application.unityVersion != "6000.3.22f1" ||
            Array.IndexOf(Environment.GetCommandLineArgs(), "--virtual-project-clone") < 0)
            return;

        if (ThreadedCallbacks == null)
        {
            Debug.LogWarning("Virtual player log thread workaround unavailable: callback field changed.");
            return;
        }

        Install();
        EditorApplication.update += Update;
    }

    private static void Install()
    {
        var callbacks = ThreadedCallbacks.GetValue(null) as Application.LogCallback;
        if (callbacks == null) return;
        foreach (Application.LogCallback callback in callbacks.GetInvocationList())
        {
            var declaringType = callback.Method.DeclaringType;
            if (declaringType?.Assembly.GetName().Name != "UnityEditor.MultiplayerModule" ||
                !(declaringType.FullName == "Unity.Multiplayer.PlayMode.Editor.StandardCloneWorkflow" ||
                  declaringType.FullName.StartsWith("Unity.Multiplayer.PlayMode.Editor.StandardCloneWorkflow+", StringComparison.Ordinal)))
                continue;

            Application.logMessageReceivedThreaded -= callback;
            Application.logMessageReceivedThreaded += (message, stack, type) =>
            {
                if (Thread.CurrentThread.ManagedThreadId == MainThreadId)
                    callback(message, stack, type);
                else
                    Pending.Enqueue(() => { callback(message, stack, type); ForwardedMessages++; });
            };
            WrappedCallbacks++;
        }
    }

    private static void Update()
    {
        // The workflow can subscribe after InitializeOnLoad or be re-created by Play Mode.
        if (EditorApplication.timeSinceStartup >= nextCheck)
        {
            nextCheck = EditorApplication.timeSinceStartup + 1;
            Install();
        }
        // Bound work per tick so a burst of worker logs doesn't freeze the editor.
        for (var i = 0; i < 256 && Pending.TryDequeue(out var forward); i++)
            forward();
    }
}
