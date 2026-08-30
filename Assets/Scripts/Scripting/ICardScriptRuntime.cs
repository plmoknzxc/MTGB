using System;

public interface ICardScriptRuntime : IDisposable
{
    ICardScriptInstance CreateCardInstance(RuntimeCard card, string scriptCode);
    void Invoke(ICardScriptInstance instance, string functionName);
    bool InvokeBoolean(ICardScriptInstance instance, string functionName, bool defaultValue);
}

public interface ICardScriptInstance : IDisposable
{
    RuntimeCard Card { get; }
    bool HasFunction(string functionName);
}
