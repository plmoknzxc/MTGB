using UnityEngine;
using XLua;

[LuaCallCSharp]
public class XLuaScriptableAPI
{
    // Access to the singleton GameRules
    private GameRules Rules => Object.FindFirstObjectByType<GameRules>();

    public void DealDamage(int targetPlayerID, int amount)
    {
        if (Rules != null)
        {
            Rules.DealDamageToPlayer(targetPlayerID, amount);
        }
    }

    public void DrawCards(int playerID, int count)
    {
        if (Rules != null)
        {
            Rules.ForceDrawCard(playerID, count);
        }
    }
    
    public void Log(string message)
    {
        Debug.Log($"[Lua]: {message}");
    }
}
