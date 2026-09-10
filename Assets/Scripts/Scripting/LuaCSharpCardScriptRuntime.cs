using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Lua;
using Lua.Standard;
using UnityEngine;

public sealed class LuaCSharpCardScriptRuntime : ICardScriptRuntime
{
    public const int SupportedApiVersion = 2;
    public bool IsExecutingHook => remainingHookInstructions != int.MaxValue;
    private const int MaximumScriptBytes = 256 * 1024;
    private const int HookInstructionBudget = 200000;
    private const int HookInstructionInterval = 1000;
    private const int MaximumRetainedLuaBytes = 4 * 1024 * 1024;
    private const int MaximumRetainedLuaTables = 4096;
    private const int MaximumRetainedLuaEntries = 32768;
    private const string RuntimeCardKey = "__runtime";

    private readonly LuaState state;
    private readonly LuaTable gameTable;
    private readonly CardScriptApi api = new CardScriptApi();
    private int remainingHookInstructions = int.MaxValue;
    private int executionDepth;
    private const int MaximumExecutionDepth = 32;

    public LuaCSharpCardScriptRuntime()
    {
        state = LuaState.Create();
        state.OpenBasicLibrary();
        state.OpenStringLibrary();
        state.OpenTableLibrary();
        state.OpenMathLibrary();

        gameTable = new LuaTable(0, 40);
        RegisterGameApi();
        RegisterConstants();
        state.Environment["Game"] = gameTable;
        ConfigureInstructionGuard(state);
    }

    private void ConfigureInstructionGuard(LuaState thread)
    {
        thread.SetHook(
            CreateFunction("MTGB.InstructionGuard", context =>
            {
                if (remainingHookInstructions == int.MaxValue) return context.Return();
                remainingHookInstructions -= HookInstructionInterval;
                if (remainingHookInstructions <= 0)
                {
                    throw new InvalidOperationException(
                        $"Lua instruction budget exceeded ({HookInstructionBudget}).");
                }
                return context.Return();
            }),
            string.Empty,
            HookInstructionInterval);
    }

    public ICardScriptInstance CreateCardInstance(RuntimeCard card, string scriptCode)
    {
        if (card == null || string.IsNullOrWhiteSpace(scriptCode)) return null;
        if (Encoding.UTF8.GetByteCount(scriptCode) > MaximumScriptBytes)
        {
            Debug.LogError(
                $"Lua card script {GetCardKey(card)} exceeds the " +
                $"{MaximumScriptBytes} byte source limit.");
            return null;
        }

        try
        {
            string chunkName = card.Data != null && !string.IsNullOrEmpty(card.Data.PrintingKey)
                ? card.Data.PrintingKey
                : card.Data?.cardName ?? "card";
            LuaValue[] results = ExecuteWithInstructionBudget(
                thread => thread.DoStringAsync(scriptCode, chunkName));
            if (results == null || results.Length == 0 || !results[0].TryRead(out LuaTable table))
            {
                Debug.LogWarning($"Lua card script for {card.Data?.cardName ?? "unknown card"} did not return a table.");
                return null;
            }

            LuaValue apiVersionValue = table["ApiVersion"];
            if (apiVersionValue.TryRead(out int requiredApiVersion)
                && requiredApiVersion > SupportedApiVersion)
            {
                Debug.LogError(
                    $"Lua card script {chunkName} requires API {requiredApiVersion}, " +
                    $"but this build supports API {SupportedApiVersion}.");
                return null;
            }

            LuaTable cardProxy = CreateCardProxy(card);
            table["Card"] = cardProxy;
            LuaCSharpCardScriptInstance instance = new LuaCSharpCardScriptInstance(card, table, cardProxy);
            EnforceResourceQuota(instance, "load");
            Invoke(instance, "OnInit");
            if (instance.IsQuarantined)
            {
                instance.Dispose();
                return null;
            }
            return instance;
        }
        catch (Exception exception)
        {
            Debug.LogError(
                $"Lua card script load failed [{GetCardKey(card)}]: {exception}");
            return null;
        }
    }

    public void Invoke(ICardScriptInstance instance, string functionName)
    {
        if (instance is not LuaCSharpCardScriptInstance luaInstance
            || luaInstance.IsQuarantined
            || string.IsNullOrEmpty(functionName))
        {
            return;
        }

        RefreshCardProxy(luaInstance.CardProxy, luaInstance.Card);

        LuaValue functionValue = luaInstance.Table[functionName];
        if (!functionValue.TryRead(out LuaFunction function)) return;

        try
        {
            ExecuteWithInstructionBudget(
                thread => thread.CallAsync(function, new LuaValue[] { luaInstance.Table }));
            EnforceResourceQuota(luaInstance, functionName);
        }
        catch (LuaResourceQuotaException exception)
        {
            Quarantine(luaInstance, functionName, exception);
        }
        catch (Exception exception)
        {
            Debug.LogError(
                $"Lua card hook '{functionName}' failed " +
                $"[{GetCardKey(instance.Card)}]: {exception}");
        }
    }

    public bool InvokeBoolean(ICardScriptInstance instance, string functionName, bool defaultValue)
    {
        if (instance is not LuaCSharpCardScriptInstance luaInstance
            || luaInstance.IsQuarantined
            || string.IsNullOrEmpty(functionName))
        {
            return defaultValue;
        }

        RefreshCardProxy(luaInstance.CardProxy, luaInstance.Card);
        LuaValue functionValue = luaInstance.Table[functionName];
        if (!functionValue.TryRead(out LuaFunction function)) return defaultValue;

        try
        {
            LuaValue[] results = ExecuteWithInstructionBudget(
                thread => thread.CallAsync(function, new LuaValue[] { luaInstance.Table }));
            EnforceResourceQuota(luaInstance, functionName);
            if (results == null || results.Length == 0) return defaultValue;
            if (results[0].TryRead(out bool boolValue)) return boolValue;
            if (results[0].TryRead(out int intValue)) return intValue != 0;
            if (results[0].TryRead(out double doubleValue)) return Math.Abs(doubleValue) > double.Epsilon;
            return defaultValue;
        }
        catch (LuaResourceQuotaException exception)
        {
            Quarantine(luaInstance, functionName, exception);
            return defaultValue;
        }
        catch (Exception exception)
        {
            Debug.LogError(
                $"Lua card predicate '{functionName}' failed on " +
                $"[{GetCardKey(instance.Card)}]: {exception}");
            return defaultValue;
        }
    }

#if UNITY_EDITOR
    public bool ValidateRetainedStateQuotaForAutomation(RuntimeCard card)
    {
        LuaTable table = null;
        LuaTable cardProxy = null;
        LuaCSharpCardScriptInstance instance = null;
        try
        {
            LuaValue[] results = ExecuteWithInstructionBudget(
                thread => thread.DoStringAsync(
                    "return { payload = string.rep('x', 4194305) }",
                    "MTGB.ResourceQuotaValidation"));
            if (results == null
                || results.Length == 0
                || !results[0].TryRead(out table))
            {
                return false;
            }

            cardProxy = CreateCardProxy(card);
            table["Card"] = cardProxy;
            instance = new LuaCSharpCardScriptInstance(card, table, cardProxy);
            try
            {
                EnforceResourceQuota(instance, "validation");
                return false;
            }
            catch (LuaResourceQuotaException)
            {
                return true;
            }
        }
        finally
        {
            instance?.Dispose();
            if (instance == null)
            {
                if (cardProxy != null) cardProxy[RuntimeCardKey] = LuaValue.Nil;
                if (table != null) table["Card"] = LuaValue.Nil;
            }
        }
    }

    public bool ValidateInstructionQuotaForAutomation()
    {
        try
        {
            ExecuteWithInstructionBudget(
                thread => thread.DoStringAsync(
                    "local value = 0 while true do value = value + 1 end",
                    "MTGB.InstructionQuotaValidation"));
            return false;
        }
        catch (Exception exception)
        {
            return exception.ToString().Contains(
                "instruction budget exceeded",
                StringComparison.OrdinalIgnoreCase);
        }
    }

    public bool ValidateNestedQuotaForAutomation(bool recursive)
    {
        gameTable["ValidationNestedCall"] = CreateFunction("Game.ValidationNestedCall", context =>
        {
            ExecuteWithInstructionBudget(thread => thread.DoStringAsync(
                recursive ? "Game.ValidationNestedCall()" : "return 1", "NestedQuota"));
            return context.Return();
        });
        try
        {
            ExecuteWithInstructionBudget(thread => thread.DoStringAsync(
                recursive ? "Game.ValidationNestedCall()" : "for i=1,300 do Game.ValidationNestedCall() end", "OuterQuota"));
            return false;
        }
        catch (Exception exception)
        {
            return exception.ToString().Contains(recursive ? "callback depth exceeded" : "instruction budget exceeded");
        }
        finally { gameTable["ValidationNestedCall"] = LuaValue.Nil; }
    }
#endif

    public void Dispose()
    {
        state?.Dispose();
    }

    private void RegisterGameApi()
    {
        gameTable["Log"] = CreateFunction("Game.Log", context =>
        {
            int index = FirstUserArgumentIndex(context);
            api.Log(ReadString(context, index));
            return context.Return();
        });

        gameTable["DrawCards"] = CreateFunction("Game.DrawCards", context =>
        {
            int index = FirstUserArgumentIndex(context);
            api.DrawCards(ReadInt(context, index), ReadInt(context, index + 1));
            return context.Return();
        });

        gameTable["DrawForOwner"] = CreateFunction("Game.DrawForOwner", context =>
        {
            int index = FirstUserArgumentIndex(context);
            api.DrawForOwner(ReadCard(context.GetArgument(index)), ReadInt(context, index + 1));
            return context.Return();
        });

        gameTable["DealDamage"] = CreateFunction("Game.DealDamage", context =>
        {
            int index = FirstUserArgumentIndex(context);
            api.DealDamage(ReadInt(context, index), ReadInt(context, index + 1));
            return context.Return();
        });

        gameTable["DealDamageToPlayer"] = CreateFunction("Game.DealDamageToPlayer", context =>
        {
            int index = FirstUserArgumentIndex(context);
            bool result = api.DealDamageToPlayer(ReadCard(context.GetArgument(index)), ReadInt(context, index + 1), ReadInt(context, index + 2));
            return context.Return(result);
        });

        gameTable["GainLife"] = CreateFunction("Game.GainLife", context =>
        {
            int index = FirstUserArgumentIndex(context);
            api.GainLife(ReadInt(context, index), ReadInt(context, index + 1));
            return context.Return();
        });

        gameTable["LoseLife"] = CreateFunction("Game.LoseLife", context =>
        {
            int index = FirstUserArgumentIndex(context);
            api.LoseLife(ReadInt(context, index), ReadInt(context, index + 1));
            return context.Return();
        });

        gameTable["GainLifeForOwner"] = CreateFunction("Game.GainLifeForOwner", context =>
        {
            int index = FirstUserArgumentIndex(context);
            api.GainLifeForOwner(ReadCard(context.GetArgument(index)), ReadInt(context, index + 1));
            return context.Return();
        });

        gameTable["LoseLifeForOwner"] = CreateFunction("Game.LoseLifeForOwner", context =>
        {
            int index = FirstUserArgumentIndex(context);
            api.LoseLifeForOwner(ReadCard(context.GetArgument(index)), ReadInt(context, index + 1));
            return context.Return();
        });

        gameTable["AddPoisonCounters"] = CreateFunction("Game.AddPoisonCounters", context =>
        {
            int index = FirstUserArgumentIndex(context);
            api.AddPoisonCounters(ReadInt(context, index), ReadInt(context, index + 1));
            return context.Return();
        });

        gameTable["GetPoisonCounters"] = CreateFunction("Game.GetPoisonCounters", context =>
        {
            int index = FirstUserArgumentIndex(context);
            return context.Return(api.GetPoisonCounters(ReadInt(context, index)));
        });

        gameTable["GetLoyaltyCounters"] = CreateFunction("Game.GetLoyaltyCounters", context =>
        {
            int index = FirstUserArgumentIndex(context);
            return context.Return(api.GetLoyaltyCounters(ReadCard(context.GetArgument(index))));
        });

        gameTable["AddLoyaltyCounters"] = CreateFunction("Game.AddLoyaltyCounters", context =>
        {
            int index = FirstUserArgumentIndex(context);
            return context.Return(api.AddLoyaltyCounters(
                ReadCard(context.GetArgument(index)),
                ReadInt(context, index + 1)));
        });

        gameTable["GetCounters"] = CreateFunction("Game.GetCounters", context =>
        {
            int index = FirstUserArgumentIndex(context);
            return context.Return(api.GetCounters(
                ReadCard(context.GetArgument(index)),
                ReadString(context, index + 1)));
        });

        gameTable["AddCounters"] = CreateFunction("Game.AddCounters", context =>
        {
            int index = FirstUserArgumentIndex(context);
            return context.Return(api.AddCounters(
                ReadCard(context.GetArgument(index)),
                ReadString(context, index + 1),
                ReadInt(context, index + 2)));
        });

        gameTable["GetOwner"] = CreateFunction("Game.GetOwner", context =>
        {
            int index = FirstUserArgumentIndex(context);
            return context.Return(api.GetOwner(ReadCard(context.GetArgument(index))));
        });

        gameTable["GetController"] = CreateFunction("Game.GetController", context =>
        {
            int index = FirstUserArgumentIndex(context);
            return context.Return(api.GetController(ReadCard(context.GetArgument(index))));
        });

        gameTable["ChangeController"] = CreateFunction("Game.ChangeController", context =>
        {
            int index = FirstUserArgumentIndex(context);
            return context.Return(api.ChangeController(
                ReadCard(context.GetArgument(index)),
                ReadInt(context, index + 1)));
        });

        gameTable["GetOpponent"] = CreateFunction("Game.GetOpponent", context =>
        {
            int index = FirstUserArgumentIndex(context);
            return context.Return(api.GetOpponent(ReadInt(context, index)));
        });

        gameTable["GetCardName"] = CreateFunction("Game.GetCardName", context =>
        {
            int index = FirstUserArgumentIndex(context);
            return context.Return(api.GetCardName(ReadCard(context.GetArgument(index))));
        });

        gameTable["IsOnBattlefield"] = CreateFunction("Game.IsOnBattlefield", context =>
        {
            int index = FirstUserArgumentIndex(context);
            return context.Return(api.IsOnBattlefield(ReadCard(context.GetArgument(index))));
        });

        gameTable["PreventNextDamage"] = CreateFunction("Game.PreventNextDamage", context =>
        {
            int index = FirstUserArgumentIndex(context);
            api.PreventNextDamage(ReadCard(context.GetArgument(index)), ReadInt(context, index + 1));
            return context.Return();
        });

        gameTable["PreventAllDamage"] = CreateFunction("Game.PreventAllDamage", context =>
        {
            int index = FirstUserArgumentIndex(context);
            api.PreventAllDamage(ReadCard(context.GetArgument(index)));
            return context.Return();
        });

        gameTable["PreventNextDamageToPlayer"] = CreateFunction("Game.PreventNextDamageToPlayer", context =>
        {
            int index = FirstUserArgumentIndex(context);
            api.PreventNextDamageToPlayer(ReadInt(context, index), ReadInt(context, index + 1));
            return context.Return();
        });

        gameTable["PreventAllDamageToPlayer"] = CreateFunction("Game.PreventAllDamageToPlayer", context =>
        {
            int index = FirstUserArgumentIndex(context);
            api.PreventAllDamageToPlayer(ReadInt(context, index));
            return context.Return();
        });

        gameTable["PreventNextDamageBySource"] = CreateFunction("Game.PreventNextDamageBySource", context =>
        {
            int index = FirstUserArgumentIndex(context);
            api.PreventNextDamageBySource(ReadCard(context.GetArgument(index)), ReadInt(context, index + 1));
            return context.Return();
        });

        gameTable["PreventAllDamageBySource"] = CreateFunction("Game.PreventAllDamageBySource", context =>
        {
            int index = FirstUserArgumentIndex(context);
            api.PreventAllDamageBySource(ReadCard(context.GetArgument(index)));
            return context.Return();
        });

        gameTable["ModifyOutgoingDamage"] = CreateFunction("Game.ModifyOutgoingDamage", context =>
        {
            int index = FirstUserArgumentIndex(context);
            return context.Return(api.ModifyOutgoingDamage(
                ReadCard(context.GetArgument(index)),
                ReadCard(context.GetArgument(index + 1)),
                ReadString(context, index + 2),
                ReadInt(context, index + 3),
                ReadBool(context, index + 4),
                ReadBool(context, index + 5)));
        });

        gameTable["ModifyIncomingDamage"] = CreateFunction("Game.ModifyIncomingDamage", context =>
        {
            int index = FirstUserArgumentIndex(context);
            return context.Return(api.ModifyIncomingDamage(
                ReadCard(context.GetArgument(index)),
                ReadCard(context.GetArgument(index + 1)),
                ReadString(context, index + 2),
                ReadInt(context, index + 3),
                ReadBool(context, index + 4),
                ReadBool(context, index + 5)));
        });

        gameTable["ModifyPlayerDamage"] = CreateFunction("Game.ModifyPlayerDamage", context =>
        {
            int index = FirstUserArgumentIndex(context);
            return context.Return(api.ModifyPlayerDamage(
                ReadCard(context.GetArgument(index)),
                ReadInt(context, index + 1),
                ReadString(context, index + 2),
                ReadInt(context, index + 3),
                ReadBool(context, index + 4),
                ReadBool(context, index + 5)));
        });

        gameTable["RemoveDamageModifier"] = CreateFunction("Game.RemoveDamageModifier", context =>
        {
            int index = FirstUserArgumentIndex(context);
            return context.Return(api.RemoveDamageModifier(ReadInt(context, index)));
        });

        gameTable["Regenerate"] = CreateFunction("Game.Regenerate", context =>
        {
            int index = FirstUserArgumentIndex(context);
            api.Regenerate(ReadCard(context.GetArgument(index)));
            return context.Return();
        });

        gameTable["CanTargetCard"] = CreateFunction("Game.CanTargetCard", context =>
        {
            int index = FirstUserArgumentIndex(context);
            return context.Return(api.CanTargetCard(ReadCard(context.GetArgument(index)), ReadCard(context.GetArgument(index + 1))));
        });

        gameTable["CanAttachToCard"] = CreateFunction("Game.CanAttachToCard", context =>
        {
            int index = FirstUserArgumentIndex(context);
            return context.Return(api.CanAttachToCard(ReadCard(context.GetArgument(index)), ReadCard(context.GetArgument(index + 1))));
        });

        gameTable["AttachToCard"] = CreateFunction("Game.AttachToCard", context =>
        {
            int index = FirstUserArgumentIndex(context);
            return context.Return(api.AttachToCard(ReadCard(context.GetArgument(index)), ReadCard(context.GetArgument(index + 1))));
        });

        gameTable["DetachCard"] = CreateFunction("Game.DetachCard", context =>
        {
            int index = FirstUserArgumentIndex(context);
            return context.Return(api.DetachCard(ReadCard(context.GetArgument(index))));
        });

        gameTable["GetTargetCard"] = CreateFunction("Game.GetTargetCard", context =>
        {
            int index = FirstUserArgumentIndex(context);
            RuntimeCard target = api.GetTargetCard(ReadCard(context.GetArgument(index)));
            return context.Return(LuaValue.FromObject(target));
        });

        gameTable["GetTargetPlayer"] = CreateFunction("Game.GetTargetPlayer", context =>
        {
            int index = FirstUserArgumentIndex(context);
            return context.Return(api.GetTargetPlayer(ReadCard(context.GetArgument(index))));
        });

        gameTable["GetTargetCount"] = CreateFunction("Game.GetTargetCount", context =>
        {
            int index = FirstUserArgumentIndex(context);
            return context.Return(api.GetTargetCount(ReadCard(context.GetArgument(index))));
        });

        gameTable["GetTargetCardAt"] = CreateFunction("Game.GetTargetCardAt", context =>
        {
            int index = FirstUserArgumentIndex(context);
            RuntimeCard target = api.GetTargetCardAt(
                ReadCard(context.GetArgument(index)),
                ReadInt(context, index + 1));
            return context.Return(LuaValue.FromObject(target));
        });

        gameTable["GetTargetPlayerAt"] = CreateFunction("Game.GetTargetPlayerAt", context =>
        {
            int index = FirstUserArgumentIndex(context);
            return context.Return(api.GetTargetPlayerAt(
                ReadCard(context.GetArgument(index)),
                ReadInt(context, index + 1)));
        });

        gameTable["AddTargetCard"] = CreateFunction("Game.AddTargetCard", context =>
        {
            int index = FirstUserArgumentIndex(context);
            return context.Return(api.AddTargetCard(
                ReadCard(context.GetArgument(index)),
                ReadCard(context.GetArgument(index + 1))));
        });

        gameTable["AddTargetPlayer"] = CreateFunction("Game.AddTargetPlayer", context =>
        {
            int index = FirstUserArgumentIndex(context);
            return context.Return(api.AddTargetPlayer(
                ReadCard(context.GetArgument(index)),
                ReadInt(context, index + 1)));
        });

        gameTable["ConfigureCastModes"] =
            CreateFunction("Game.ConfigureCastModes", context =>
        {
            int index = FirstUserArgumentIndex(context);
            return context.Return(api.ConfigureCastModes(
                ReadCard(context.GetArgument(index)),
                ReadInt(context, index + 1),
                ReadInt(context, index + 2)));
        });

        gameTable["AddCastMode"] = CreateFunction("Game.AddCastMode", context =>
        {
            int index = FirstUserArgumentIndex(context);
            return context.Return(api.AddCastMode(
                ReadCard(context.GetArgument(index)),
                ReadString(context, index + 1),
                ReadString(context, index + 2)));
        });

        gameTable["ConfigureCastX"] = CreateFunction("Game.ConfigureCastX", context =>
        {
            int index = FirstUserArgumentIndex(context);
            return context.Return(api.ConfigureCastX(
                ReadCard(context.GetArgument(index)),
                ReadInt(context, index + 1),
                ReadInt(context, index + 2)));
        });

        gameTable["AddCastCost"] = CreateFunction("Game.AddCastCost", context =>
        {
            int index = FirstUserArgumentIndex(context);
            return context.Return(api.AddCastCost(
                ReadCard(context.GetArgument(index)),
                ReadString(context, index + 1),
                ReadString(context, index + 2),
                ReadEnumName<SpellCastCostKind>(context, index + 3),
                ReadOptionalString(context, index + 4),
                context.HasArgument(index + 5) && ReadBool(context, index + 5)));
        });

        gameTable["AddCastTargetGroup"] =
            CreateFunction("Game.AddCastTargetGroup", context =>
        {
            int index = FirstUserArgumentIndex(context);
            return context.Return(api.AddCastTargetGroup(
                ReadCard(context.GetArgument(index)),
                ReadString(context, index + 1),
                ReadString(context, index + 2),
                ReadEnumName<CardTargetMode>(context, index + 3),
                ReadInt(context, index + 4),
                ReadInt(context, index + 5),
                ReadOptionalString(context, index + 6),
                !context.HasArgument(index + 7) || ReadBool(context, index + 7)));
        });

        gameTable["AddCastTargetGroupAdvanced"] =
            CreateFunction("Game.AddCastTargetGroupAdvanced", context =>
        {
            int index = FirstUserArgumentIndex(context);
            return context.Return(api.AddCastTargetGroupAdvanced(
                ReadCard(context.GetArgument(index)),
                ReadString(context, index + 1),
                ReadString(context, index + 2),
                ReadInt(context, index + 3),
                ReadInt(context, index + 4),
                ReadInt(context, index + 5),
                ReadInt(context, index + 6),
                ReadInt(context, index + 7),
                ReadInt(context, index + 8),
                ReadInt(context, index + 9),
                ReadOptionalString(context, index + 10),
                !context.HasArgument(index + 11) || ReadBool(context, index + 11),
                ReadInt(context, index + 12),
                ReadOptionalString(context, index + 13)));
        });

        gameTable["GetCastX"] = CreateFunction("Game.GetCastX", context =>
        {
            int index = FirstUserArgumentIndex(context);
            return context.Return(api.GetCastX(
                ReadCard(context.GetArgument(index))));
        });

        gameTable["IsCastModeSelected"] =
            CreateFunction("Game.IsCastModeSelected", context =>
        {
            int index = FirstUserArgumentIndex(context);
            return context.Return(api.IsCastModeSelected(
                ReadCard(context.GetArgument(index)),
                ReadString(context, index + 1)));
        });

        gameTable["WasAdditionalCostPaid"] =
            CreateFunction("Game.WasAdditionalCostPaid", context =>
        {
            int index = FirstUserArgumentIndex(context);
            return context.Return(api.WasAdditionalCostPaid(
                ReadCard(context.GetArgument(index)),
                ReadString(context, index + 1)));
        });

        gameTable["GetTargetGroupCount"] =
            CreateFunction("Game.GetTargetGroupCount", context =>
        {
            int index = FirstUserArgumentIndex(context);
            return context.Return(api.GetTargetGroupCount(
                ReadCard(context.GetArgument(index)),
                ReadString(context, index + 1)));
        });

        gameTable["GetTargetCardInGroup"] =
            CreateFunction("Game.GetTargetCardInGroup", context =>
        {
            int index = FirstUserArgumentIndex(context);
            return context.Return(LuaValue.FromObject(api.GetTargetCardInGroup(
                ReadCard(context.GetArgument(index)),
                ReadString(context, index + 1),
                ReadInt(context, index + 2))));
        });

        gameTable["GetTargetPlayerInGroup"] =
            CreateFunction("Game.GetTargetPlayerInGroup", context =>
        {
            int index = FirstUserArgumentIndex(context);
            return context.Return(api.GetTargetPlayerInGroup(
                ReadCard(context.GetArgument(index)),
                ReadString(context, index + 1),
                ReadInt(context, index + 2)));
        });

        gameTable["ClearTargets"] = CreateFunction("Game.ClearTargets", context =>
        {
            int index = FirstUserArgumentIndex(context);
            api.ClearTargets(ReadCard(context.GetArgument(index)));
            return context.Return();
        });

        gameTable["ChooseOptions"] = CreateFunction("Game.ChooseOptions", context =>
        {
            int index = FirstUserArgumentIndex(context);
            return context.Return(api.ChooseOptions(
                ReadCard(context.GetArgument(index)),
                ReadString(context, index + 1),
                ReadStringList(context.GetArgument(index + 2)),
                ReadInt(context, index + 3),
                ReadInt(context, index + 4),
                ReadBool(context, index + 5),
                ReadString(context, index + 6),
                ReadOptionalString(context, index + 7)));
        });

        gameTable["ChooseMode"] = CreateFunction("Game.ChooseMode", context =>
        {
            int index = FirstUserArgumentIndex(context);
            return context.Return(api.ChooseMode(
                ReadCard(context.GetArgument(index)),
                ReadString(context, index + 1),
                ReadStringList(context.GetArgument(index + 2)),
                ReadBool(context, index + 3),
                ReadString(context, index + 4),
                ReadOptionalString(context, index + 5)));
        });

        gameTable["OrderOptions"] = CreateFunction("Game.OrderOptions", context =>
        {
            int index = FirstUserArgumentIndex(context);
            return context.Return(api.OrderOptions(
                ReadCard(context.GetArgument(index)),
                ReadString(context, index + 1),
                ReadStringList(context.GetArgument(index + 2)),
                ReadString(context, index + 3)));
        });

        gameTable["ChooseNumber"] = CreateFunction("Game.ChooseNumber", context =>
        {
            int index = FirstUserArgumentIndex(context);
            return context.Return(api.ChooseNumber(
                ReadCard(context.GetArgument(index)),
                ReadString(context, index + 1),
                ReadInt(context, index + 2),
                ReadInt(context, index + 3),
                ReadBool(context, index + 4),
                ReadString(context, index + 5),
                ReadOptionalString(context, index + 6)));
        });

        gameTable["ChooseRepeatedOptions"] =
            CreateFunction("Game.ChooseRepeatedOptions", context =>
        {
            int index = FirstUserArgumentIndex(context);
            return context.Return(api.ChooseRepeatedOptions(
                ReadCard(context.GetArgument(index)),
                ReadString(context, index + 1),
                ReadStringList(context.GetArgument(index + 2)),
                ReadInt(context, index + 3),
                ReadInt(context, index + 4),
                ReadInt(context, index + 5),
                ReadBool(context, index + 6),
                ReadString(context, index + 7),
                ReadOptionalString(context, index + 8)));
        });

        gameTable["DistributeAmount"] = CreateFunction("Game.DistributeAmount", context =>
        {
            int index = FirstUserArgumentIndex(context);
            return context.Return(api.DistributeAmount(
                ReadCard(context.GetArgument(index)),
                ReadString(context, index + 1),
                ReadStringList(context.GetArgument(index + 2)),
                ReadInt(context, index + 3),
                ReadInt(context, index + 4),
                ReadInt(context, index + 5),
                ReadBool(context, index + 6),
                ReadString(context, index + 7),
                ReadOptionalString(context, index + 8)));
        });

        gameTable["GetLastChoiceValue"] = CreateFunction("Game.GetLastChoiceValue", context =>
        {
            return context.Return(api.GetLastChoiceValue());
        });

        gameTable["GetLastChoiceCount"] = CreateFunction("Game.GetLastChoiceCount", context =>
        {
            return context.Return(api.GetLastChoiceCount());
        });

        gameTable["GetLastChoiceOption"] = CreateFunction("Game.GetLastChoiceOption", context =>
        {
            int index = FirstUserArgumentIndex(context);
            return context.Return(api.GetLastChoiceOption(ReadInt(context, index)));
        });

        gameTable["GetLastChoiceAllocation"] = CreateFunction("Game.GetLastChoiceAllocation", context =>
        {
            int index = FirstUserArgumentIndex(context);
            return context.Return(api.GetLastChoiceAllocation(ReadInt(context, index)));
        });

        gameTable["DealDamageToCard"] = CreateFunction("Game.DealDamageToCard", context =>
        {
            int index = FirstUserArgumentIndex(context);
            bool result = api.DealDamageToCard(ReadCard(context.GetArgument(index)), ReadCard(context.GetArgument(index + 1)), ReadInt(context, index + 2));
            return context.Return(result);
        });

        gameTable["DealDamageToTarget"] = CreateFunction("Game.DealDamageToTarget", context =>
        {
            int index = FirstUserArgumentIndex(context);
            bool result = api.DealDamageToTarget(ReadCard(context.GetArgument(index)), ReadInt(context, index + 1));
            return context.Return(result);
        });

        gameTable["Destroy"] = CreateFunction("Game.Destroy", context =>
        {
            int index = FirstUserArgumentIndex(context);
            return context.Return(api.Destroy(ReadCard(context.GetArgument(index))));
        });

        gameTable["DestroyTarget"] = CreateFunction("Game.DestroyTarget", context =>
        {
            int index = FirstUserArgumentIndex(context);
            return context.Return(api.DestroyTarget(ReadCard(context.GetArgument(index))));
        });

        gameTable["Exile"] = CreateFunction("Game.Exile", context =>
        {
            int index = FirstUserArgumentIndex(context);
            return context.Return(api.Exile(ReadCard(context.GetArgument(index))));
        });

        gameTable["ExileTarget"] = CreateFunction("Game.ExileTarget", context =>
        {
            int index = FirstUserArgumentIndex(context);
            return context.Return(api.ExileTarget(ReadCard(context.GetArgument(index))));
        });

        gameTable["PutIntoGraveyard"] = CreateFunction("Game.PutIntoGraveyard", context =>
        {
            int index = FirstUserArgumentIndex(context);
            return context.Return(api.PutIntoGraveyard(ReadCard(context.GetArgument(index))));
        });

        gameTable["CreateTokens"] = CreateFunction("Game.CreateTokens", context =>
        {
            int index = FirstUserArgumentIndex(context);
            return context.Return(api.CreateTokens(
                ReadString(context, index),
                ReadString(context, index + 1),
                ReadInt(context, index + 2),
                ReadInt(context, index + 3)));
        });

        gameTable["CreateEmblem"] = CreateFunction("Game.CreateEmblem", context =>
        {
            int index = FirstUserArgumentIndex(context);
            return context.Return(LuaValue.FromObject(api.CreateEmblem(
                ReadString(context, index),
                ReadString(context, index + 1),
                ReadInt(context, index + 2))));
        });

        gameTable["CopySpell"] = CreateFunction("Game.CopySpell", context =>
        {
            int index = FirstUserArgumentIndex(context);
            return context.Return(LuaValue.FromObject(api.CopySpell(
                ReadCard(context.GetArgument(index)),
                ReadInt(context, index + 1))));
        });

        gameTable["CopyStackAbility"] = CreateFunction("Game.CopyStackAbility", context =>
        {
            int index = FirstUserArgumentIndex(context);
            return context.Return(api.CopyStackAbility(
                ReadInt(context, index),
                ReadInt(context, index + 1)));
        });

        gameTable["Tap"] = CreateFunction("Game.Tap", context =>
        {
            int index = FirstUserArgumentIndex(context);
            return context.Return(api.Tap(ReadCard(context.GetArgument(index))));
        });

        gameTable["Untap"] = CreateFunction("Game.Untap", context =>
        {
            int index = FirstUserArgumentIndex(context);
            return context.Return(api.Untap(ReadCard(context.GetArgument(index))));
        });

        gameTable["RegisterFetchBasicLandAbility"] = CreateFunction("Game.RegisterFetchBasicLandAbility", context =>
        {
            int index = FirstUserArgumentIndex(context);
            return context.Return(api.RegisterFetchBasicLandAbility(ReadCard(context.GetArgument(index))));
        });

        gameTable["RegisterManaAbility"] = CreateFunction("Game.RegisterManaAbility", context =>
        {
            int index = FirstUserArgumentIndex(context);
            return context.Return(api.RegisterManaAbility(ReadCard(context.GetArgument(index)), ReadString(context, index + 1)));
        });

        gameTable["GetFeatureRole"] = CreateFunction("Game.GetFeatureRole", context =>
        {
            int index = FirstUserArgumentIndex(context);
            return context.Return(api.GetFeatureRole(ReadCard(context.GetArgument(index))));
        });

        gameTable["GetFeatureIndex"] = CreateFunction("Game.GetFeatureIndex", context =>
        {
            int index = FirstUserArgumentIndex(context);
            return context.Return(api.GetFeatureIndex(ReadCard(context.GetArgument(index))));
        });

        gameTable["GetFeatureFace"] = CreateFunction("Game.GetFeatureFace", context =>
        {
            int index = FirstUserArgumentIndex(context);
            return context.Return(api.GetFeatureFace(ReadCard(context.GetArgument(index))));
        });

        gameTable["GetFeatureName"] = CreateFunction("Game.GetFeatureName", context =>
        {
            int index = FirstUserArgumentIndex(context);
            return context.Return(api.GetFeatureName(ReadCard(context.GetArgument(index))));
        });

        gameTable["GetFeatureRoleValue"] = CreateFunction("Game.GetFeatureRoleValue", context =>
        {
            int index = FirstUserArgumentIndex(context);
            return context.Return(api.GetFeatureRoleValue(ReadCard(context.GetArgument(index))));
        });

        gameTable["GetResolvingChapter"] = CreateFunction("Game.GetResolvingChapter", context =>
        {
            int index = FirstUserArgumentIndex(context);
            return context.Return(api.GetResolvingChapter(ReadCard(context.GetArgument(index))));
        });

        gameTable["GetClassLevel"] = CreateFunction("Game.GetClassLevel", context =>
        {
            int index = FirstUserArgumentIndex(context);
            return context.Return(api.GetClassLevel(ReadCard(context.GetArgument(index))));
        });

        gameTable["IsCaseSolved"] = CreateFunction("Game.IsCaseSolved", context =>
        {
            int index = FirstUserArgumentIndex(context);
            return context.Return(api.IsCaseSolved(ReadCard(context.GetArgument(index))));
        });

        gameTable["IsFeatureActive"] = CreateFunction("Game.IsFeatureActive", context =>
        {
            int index = FirstUserArgumentIndex(context);
            return context.Return(api.IsFeatureActive(
                ReadCard(context.GetArgument(index)),
                ReadInt(context, index + 1),
                ReadInt(context, index + 2)));
        });

        gameTable["IsRoomUnlocked"] = CreateFunction("Game.IsRoomUnlocked", context =>
        {
            int index = FirstUserArgumentIndex(context);
            return context.Return(api.IsRoomUnlocked(
                ReadCard(context.GetArgument(index)),
                ReadInt(context, index + 1),
                ReadInt(context, index + 2)));
        });

        gameTable["Prepare"] = CreateFunction("Game.Prepare", context =>
        {
            int index = FirstUserArgumentIndex(context);
            return context.Return(api.Prepare(
                ReadCard(context.GetArgument(index)),
                context.HasArgument(index + 1) ? ReadInt(context, index + 1) : -1));
        });

        gameTable["StopPreparing"] = CreateFunction("Game.StopPreparing", context =>
        {
            int index = FirstUserArgumentIndex(context);
            return context.Return(api.StopPreparing(ReadCard(context.GetArgument(index))));
        });

        gameTable["SolveCase"] = CreateFunction("Game.SolveCase", context =>
        {
            int index = FirstUserArgumentIndex(context);
            return context.Return(api.SolveCase(ReadCard(context.GetArgument(index))));
        });

        gameTable["SetMaxHandSize"] = CreateFunction("Game.SetMaxHandSize", context =>
        {
            int index = FirstUserArgumentIndex(context);
            api.SetMaxHandSize(ReadInt(context, index), ReadInt(context, index + 1));
            return context.Return();
        });

        gameTable["AddKeyword"] = CreateFunction("Game.AddKeyword", context =>
        {
            int index = FirstUserArgumentIndex(context);
            api.AddKeyword(ReadCard(context.GetArgument(index)), ReadString(context, index + 1));
            return context.Return();
        });

        gameTable["RemoveKeyword"] = CreateFunction("Game.RemoveKeyword", context =>
        {
            int index = FirstUserArgumentIndex(context);
            api.RemoveKeyword(ReadCard(context.GetArgument(index)), ReadString(context, index + 1));
            return context.Return();
        });

        gameTable["Buff"] = CreateFunction("Game.Buff", context =>
        {
            int index = FirstUserArgumentIndex(context);
            return context.Return(api.Buff(
                ReadCard(context.GetArgument(index)),
                ReadInt(context, index + 1),
                ReadInt(context, index + 2),
                ReadOptionalString(context, index + 3)));
        });

        gameTable["BuffUntilEndOfTurn"] = CreateFunction("Game.BuffUntilEndOfTurn", context =>
        {
            int index = FirstUserArgumentIndex(context);
            return context.Return(api.BuffUntilEndOfTurn(
                ReadCard(context.GetArgument(index)),
                ReadInt(context, index + 1),
                ReadInt(context, index + 2),
                ReadOptionalString(context, index + 3)));
        });

        gameTable["SetPowerToughness"] = CreateFunction("Game.SetPowerToughness", context =>
        {
            int index = FirstUserArgumentIndex(context);
            return context.Return(api.SetPowerToughness(
                ReadCard(context.GetArgument(index)),
                ReadInt(context, index + 1),
                ReadInt(context, index + 2),
                ReadOptionalString(context, index + 3)));
        });

        gameTable["SetPowerToughnessUntilEndOfTurn"] = CreateFunction("Game.SetPowerToughnessUntilEndOfTurn", context =>
        {
            int index = FirstUserArgumentIndex(context);
            return context.Return(api.SetPowerToughnessUntilEndOfTurn(
                ReadCard(context.GetArgument(index)),
                ReadInt(context, index + 1),
                ReadInt(context, index + 2),
                ReadOptionalString(context, index + 3)));
        });

        gameTable["SwitchPowerToughness"] = CreateFunction("Game.SwitchPowerToughness", context =>
        {
            int index = FirstUserArgumentIndex(context);
            return context.Return(api.SwitchPowerToughness(ReadCard(context.GetArgument(index))));
        });

        gameTable["SwitchPowerToughnessUntilEndOfTurn"] = CreateFunction("Game.SwitchPowerToughnessUntilEndOfTurn", context =>
        {
            int index = FirstUserArgumentIndex(context);
            return context.Return(api.SwitchPowerToughnessUntilEndOfTurn(ReadCard(context.GetArgument(index))));
        });

        gameTable["AddCardType"] = CreateFunction("Game.AddCardType", context =>
        {
            int index = FirstUserArgumentIndex(context);
            return context.Return(api.AddCardType(
                ReadCard(context.GetArgument(index)),
                ReadInt(context, index + 1),
                ReadOptionalString(context, index + 2),
                context.HasArgument(index + 3) && ReadBool(context, index + 3)));
        });

        gameTable["RemoveCardType"] = CreateFunction("Game.RemoveCardType", context =>
        {
            int index = FirstUserArgumentIndex(context);
            return context.Return(api.RemoveCardType(
                ReadCard(context.GetArgument(index)),
                ReadInt(context, index + 1),
                ReadOptionalString(context, index + 2),
                context.HasArgument(index + 3) && ReadBool(context, index + 3)));
        });

        gameTable["SetColors"] = CreateFunction("Game.SetColors", context =>
        {
            int index = FirstUserArgumentIndex(context);
            return context.Return(api.SetColors(
                ReadCard(context.GetArgument(index)),
                ReadInt(context, index + 1),
                ReadOptionalString(context, index + 2),
                context.HasArgument(index + 3) && ReadBool(context, index + 3)));
        });

        gameTable["GrantKeyword"] = CreateFunction("Game.GrantKeyword", context =>
        {
            int index = FirstUserArgumentIndex(context);
            return context.Return(api.GrantKeyword(
                ReadCard(context.GetArgument(index)),
                ReadString(context, index + 1),
                ReadOptionalString(context, index + 2),
                context.HasArgument(index + 3) && ReadBool(context, index + 3)));
        });

        gameTable["SuppressKeyword"] = CreateFunction("Game.SuppressKeyword", context =>
        {
            int index = FirstUserArgumentIndex(context);
            return context.Return(api.SuppressKeyword(
                ReadCard(context.GetArgument(index)),
                ReadString(context, index + 1),
                ReadOptionalString(context, index + 2),
                context.HasArgument(index + 3) && ReadBool(context, index + 3)));
        });

        gameTable["RegisterTriggeredAbility"] =
            CreateFunction("Game.RegisterTriggeredAbility", context =>
        {
            int index = FirstUserArgumentIndex(context);
            return context.Return(api.RegisterTriggeredAbility(
                ReadCard(context.GetArgument(index)),
                ReadEnumName<GameEventType>(context, index + 1),
                ReadString(context, index + 2),
                ReadOptionalString(context, index + 3),
                ReadOptionalEnumName<RuntimeEffectDuration>(context, index + 4),
                context.HasArgument(index + 5) && ReadBool(context, index + 5),
                ReadOptionalString(context, index + 6)));
        });

        gameTable["RegisterDelayedTriggeredAbility"] =
            CreateFunction("Game.RegisterDelayedTriggeredAbility", context =>
        {
            int index = FirstUserArgumentIndex(context);
            return context.Return(api.RegisterDelayedTriggeredAbility(
                ReadCard(context.GetArgument(index)),
                ReadEnumName<GameEventType>(context, index + 1),
                ReadString(context, index + 2),
                ReadOptionalString(context, index + 3),
                context.HasArgument(index + 4) && ReadBool(context, index + 4),
                ReadOptionalString(context, index + 5)));
        });

        gameTable["RegisterReplacementEffect"] =
            CreateFunction("Game.RegisterReplacementEffect", context =>
        {
            int index = FirstUserArgumentIndex(context);
            return context.Return(api.RegisterReplacementEffect(
                ReadCard(context.GetArgument(index)),
                ReadEnumName<GameEventType>(context, index + 1),
                ReadEnumName<GameEventReplacementOperation>(context, index + 2),
                ReadOptionalString(context, index + 3),
                context.HasArgument(index + 4) ? ReadInt(context, index + 4) : 0,
                ReadOptionalEnumName<RuntimeEffectDuration>(context, index + 5),
                ReadOptionalString(context, index + 6),
                ReadOptionalString(context, index + 7),
                ReadOptionalEnumName<ReplacementEffectCategory>(context, index + 8)));
        });

        gameTable["RegisterContinuousEffect"] =
            CreateFunction("Game.RegisterContinuousEffect", context =>
        {
            int index = FirstUserArgumentIndex(context);
            return context.Return(api.RegisterContinuousEffect(
                ReadCard(context.GetArgument(index)),
                ReadEnumName<ContinuousEffectOperation>(context, index + 1),
                ReadOptionalString(context, index + 2),
                ReadOptionalString(context, index + 3),
                context.HasArgument(index + 4) ? ReadInt(context, index + 4) : 0,
                context.HasArgument(index + 5) ? ReadInt(context, index + 5) : 0,
                ReadOptionalString(context, index + 6),
                context.HasArgument(index + 7) ? ReadInt(context, index + 7) : 0,
                context.HasArgument(index + 8) ? ReadInt(context, index + 8) : 0,
                ReadOptionalEnumName<RuntimeEffectDuration>(context, index + 9),
                ReadOptionalString(context, index + 10),
                context.HasArgument(index + 11) && ReadBool(context, index + 11),
                context.HasArgument(index + 12) ? ReadInt(context, index + 12) : 0,
                context.HasArgument(index + 13) ? ReadInt(context, index + 13) : 0,
                context.HasArgument(index + 14)
                    ? ReadCard(context.GetArgument(index + 14))
                    : null,
                context.HasArgument(index + 15) ? ReadInt(context, index + 15) : 0));
        });

        gameTable["RegisterRuleEffect"] =
            CreateFunction("Game.RegisterRuleEffect", context =>
        {
            int index = FirstUserArgumentIndex(context);
            return context.Return(api.RegisterRuleEffect(
                ReadCard(context.GetArgument(index)),
                ReadString(context, index + 1),
                ReadEnumName<RuleEffectOperation>(context, index + 2),
                ReadInt(context, index + 3),
                ReadInt(context, index + 4),
                ReadOptionalString(context, index + 5),
                ReadEnumName<RuntimeEffectDuration>(context, index + 6),
                ReadOptionalString(context, index + 7)));
        });

        gameTable["GetRuleValue"] = CreateFunction("Game.GetRuleValue", context =>
        {
            int index = FirstUserArgumentIndex(context);
            return context.Return(api.GetRuleValue(
                ReadString(context, index),
                ReadInt(context, index + 1),
                context.HasArgument(index + 2)
                    ? ReadCard(context.GetArgument(index + 2))
                    : null,
                ReadInt(context, index + 3)));
        });

        gameTable["GetRulePermission"] =
            CreateFunction("Game.GetRulePermission", context =>
        {
            int index = FirstUserArgumentIndex(context);
            return context.Return(api.GetRulePermission(
                ReadString(context, index),
                ReadInt(context, index + 1),
                context.HasArgument(index + 2)
                    ? ReadCard(context.GetArgument(index + 2))
                    : null,
                ReadBool(context, index + 3)));
        });

        gameTable["RemoveRuntimeEffect"] =
            CreateFunction("Game.RemoveRuntimeEffect", context =>
        {
            int index = FirstUserArgumentIndex(context);
            return context.Return(api.RemoveRuntimeEffect(ReadLong(context, index)));
        });

        gameTable["RemoveLinkedEffects"] =
            CreateFunction("Game.RemoveLinkedEffects", context =>
        {
            int index = FirstUserArgumentIndex(context);
            return context.Return(api.RemoveLinkedEffects(
                ReadCard(context.GetArgument(index)),
                ReadString(context, index + 1)));
        });

        gameTable["RegisterActivatedAbility"] =
            CreateFunction("Game.RegisterActivatedAbility", context =>
        {
            int index = FirstUserArgumentIndex(context);
            return context.Return(api.RegisterActivatedAbility(
                ReadCard(context.GetArgument(index)),
                ReadString(context, index + 1),
                ReadOptionalString(context, index + 2),
                ReadString(context, index + 3),
                ReadString(context, index + 4),
                ReadOptionalString(context, index + 5),
                ReadOptionalEnumName<ScriptAbilityTiming>(context, index + 6),
                ReadInt(context, index + 7)));
        });

        gameTable["GetEventType"] = CreateFunction("Game.GetEventType", context =>
        {
            int index = FirstUserArgumentIndex(context);
            return context.Return(api.GetEventType(ReadCard(context.GetArgument(index))));
        });

        gameTable["GetEventAmount"] = CreateFunction("Game.GetEventAmount", context =>
        {
            int index = FirstUserArgumentIndex(context);
            return context.Return(api.GetEventAmount(ReadCard(context.GetArgument(index))));
        });

        gameTable["GetEventPlayer"] = CreateFunction("Game.GetEventPlayer", context =>
        {
            int index = FirstUserArgumentIndex(context);
            return context.Return(api.GetEventPlayer(ReadCard(context.GetArgument(index))));
        });

        gameTable["GetEventSource"] = CreateFunction("Game.GetEventSource", context =>
        {
            int index = FirstUserArgumentIndex(context);
            return context.Return(LuaValue.FromObject(
                api.GetEventSource(ReadCard(context.GetArgument(index)))));
        });

        gameTable["GetEventCard"] = CreateFunction("Game.GetEventCard", context =>
        {
            int index = FirstUserArgumentIndex(context);
            return context.Return(LuaValue.FromObject(
                api.GetEventCard(ReadCard(context.GetArgument(index)))));
        });

        gameTable["GetEventFromZone"] = CreateFunction("Game.GetEventFromZone", context =>
        {
            int index = FirstUserArgumentIndex(context);
            return context.Return(api.GetEventFromZone(ReadCard(context.GetArgument(index))));
        });

        gameTable["GetEventToZone"] = CreateFunction("Game.GetEventToZone", context =>
        {
            int index = FirstUserArgumentIndex(context);
            return context.Return(api.GetEventToZone(ReadCard(context.GetArgument(index))));
        });

        gameTable["SetEventAmount"] = CreateFunction("Game.SetEventAmount", context =>
        {
            int index = FirstUserArgumentIndex(context);
            return context.Return(api.SetEventAmount(
                ReadCard(context.GetArgument(index)),
                ReadInt(context, index + 1)));
        });

        gameTable["SetEventDamageTarget"] =
            CreateFunction("Game.SetEventDamageTarget", context =>
        {
            int index = FirstUserArgumentIndex(context);
            return context.Return(api.SetEventDamageTarget(
                ReadCard(context.GetArgument(index)),
                ReadCard(context.GetArgument(index + 1))));
        });

        gameTable["SetEventDamagePlayer"] =
            CreateFunction("Game.SetEventDamagePlayer", context =>
        {
            int index = FirstUserArgumentIndex(context);
            return context.Return(api.SetEventDamagePlayer(
                ReadCard(context.GetArgument(index)),
                ReadInt(context, index + 1)));
        });

        gameTable["PreventEvent"] = CreateFunction("Game.PreventEvent", context =>
        {
            int index = FirstUserArgumentIndex(context);
            return context.Return(api.PreventEvent(ReadCard(context.GetArgument(index))));
        });

        gameTable["SetEventDestination"] =
            CreateFunction("Game.SetEventDestination", context =>
        {
            int index = FirstUserArgumentIndex(context);
            return context.Return(api.SetEventDestination(
                ReadCard(context.GetArgument(index)),
                ReadEnumName<GameZone>(context, index + 1)));
        });

        gameTable["GetZoneCount"] = CreateFunction("Game.GetZoneCount", context =>
        {
            int index = FirstUserArgumentIndex(context);
            return context.Return(api.GetZoneCount(
                ReadInt(context, index),
                ReadEnumName<GameZone>(context, index + 1)));
        });

        gameTable["GetZoneCard"] = CreateFunction("Game.GetZoneCard", context =>
        {
            int index = FirstUserArgumentIndex(context);
            return context.Return(LuaValue.FromObject(api.GetZoneCard(
                ReadInt(context, index),
                ReadEnumName<GameZone>(context, index + 1),
                ReadInt(context, index + 2))));
        });

        gameTable["GetMatchingZoneCards"] =
            CreateFunction("Game.GetMatchingZoneCards", context =>
        {
            int index = FirstUserArgumentIndex(context);
            List<RuntimeCard> cards = api.GetMatchingZoneCards(
                ReadCard(context.GetArgument(index)),
                ReadInt(context, index + 1),
                ReadEnumName<GameZone>(context, index + 2),
                ReadString(context, index + 3));
            return context.Return(CreateCardListTable(cards));
        });

        gameTable["MoveToZone"] = CreateFunction("Game.MoveToZone", context =>
        {
            int index = FirstUserArgumentIndex(context);
            return context.Return(api.MoveToZone(
                ReadCard(context.GetArgument(index)),
                ReadEnumName<GameZone>(context, index + 1),
                context.HasArgument(index + 2) && ReadBool(context, index + 2)));
        });

        gameTable["MoveCardsToZone"] = CreateFunction("Game.MoveCardsToZone", context =>
        {
            int index = FirstUserArgumentIndex(context);
            return context.Return(api.MoveCardsToZone(
                ReadCardList(context.GetArgument(index)),
                ReadEnumName<GameZone>(context, index + 1),
                context.HasArgument(index + 2) && ReadBool(context, index + 2)));
        });

        gameTable["ShuffleLibrary"] = CreateFunction("Game.ShuffleLibrary", context =>
        {
            int index = FirstUserArgumentIndex(context);
            return context.Return(api.ShuffleLibrary(ReadInt(context, index)));
        });

        gameTable["Mill"] = CreateFunction("Game.Mill", context =>
        {
            int index = FirstUserArgumentIndex(context);
            return context.Return(api.Mill(
                ReadInt(context, index),
                ReadInt(context, index + 1)));
        });

        gameTable["Discard"] = CreateFunction("Game.Discard", context =>
        {
            int index = FirstUserArgumentIndex(context);
            return context.Return(api.Discard(ReadCard(context.GetArgument(index))));
        });

        gameTable["ReturnToHand"] = CreateFunction("Game.ReturnToHand", context =>
        {
            int index = FirstUserArgumentIndex(context);
            return context.Return(api.ReturnToHand(ReadCard(context.GetArgument(index))));
        });

        gameTable["CounterSpell"] = CreateFunction("Game.CounterSpell", context =>
        {
            int index = FirstUserArgumentIndex(context);
            return context.Return(api.CounterSpell(ReadCard(context.GetArgument(index))));
        });

        gameTable["CastWithoutPayingMana"] =
            CreateFunction("Game.CastWithoutPayingMana", context =>
        {
            int index = FirstUserArgumentIndex(context);
            return context.Return(api.CastWithoutPayingMana(
                ReadCard(context.GetArgument(index)),
                context.HasArgument(index + 1) ? ReadInt(context, index + 1) : -1));
        });

        gameTable["GetObjectId"] = CreateFunction("Game.GetObjectId", context =>
        {
            int index = FirstUserArgumentIndex(context);
            return context.Return(api.GetObjectId(ReadCard(context.GetArgument(index))));
        });

        gameTable["GetZone"] = CreateFunction("Game.GetZone", context =>
        {
            int index = FirstUserArgumentIndex(context);
            return context.Return(api.GetZone(ReadCard(context.GetArgument(index))));
        });

        gameTable["HasType"] = CreateFunction("Game.HasType", context =>
        {
            int index = FirstUserArgumentIndex(context);
            return context.Return(api.HasType(
                ReadCard(context.GetArgument(index)),
                ReadInt(context, index + 1)));
        });

        gameTable["HasSubtype"] = CreateFunction("Game.HasSubtype", context =>
        {
            int index = FirstUserArgumentIndex(context);
            return context.Return(api.HasSubtype(
                ReadCard(context.GetArgument(index)),
                ReadString(context, index + 1)));
        });

        gameTable["HasSupertype"] = CreateFunction("Game.HasSupertype", context =>
        {
            int index = FirstUserArgumentIndex(context);
            return context.Return(api.HasSupertype(
                ReadCard(context.GetArgument(index)),
                ReadInt(context, index + 1)));
        });

        gameTable["HasKeyword"] = CreateFunction("Game.HasKeyword", context =>
        {
            int index = FirstUserArgumentIndex(context);
            return context.Return(api.HasKeyword(
                ReadCard(context.GetArgument(index)),
                ReadString(context, index + 1)));
        });

        gameTable["GetManaValue"] = CreateFunction("Game.GetManaValue", context =>
        {
            int index = FirstUserArgumentIndex(context);
            return context.Return(api.GetManaValue(ReadCard(context.GetArgument(index))));
        });

        gameTable["GetCardTypes"] = CreateFunction("Game.GetCardTypes", context =>
        {
            int index = FirstUserArgumentIndex(context);
            return context.Return(api.GetCardTypes(ReadCard(context.GetArgument(index))));
        });

        gameTable["GetColors"] = CreateFunction("Game.GetColors", context =>
        {
            int index = FirstUserArgumentIndex(context);
            return context.Return(api.GetColors(ReadCard(context.GetArgument(index))));
        });

        gameTable["IsTapped"] = CreateFunction("Game.IsTapped", context =>
        {
            int index = FirstUserArgumentIndex(context);
            return context.Return(api.IsTapped(ReadCard(context.GetArgument(index))));
        });
    }

    private void RegisterConstants()
    {
        gameTable["ApiVersion"] = SupportedApiVersion;
        gameTable["Zone"] = CreateEnumTable<GameZone>();
        gameTable["Event"] = CreateEnumTable<GameEventType>();
        gameTable["Replacement"] = CreateEnumTable<GameEventReplacementOperation>();
        gameTable["ReplacementCategory"] =
            CreateEnumTable<ReplacementEffectCategory>();
        gameTable["Continuous"] = CreateEnumTable<ContinuousEffectOperation>();
        gameTable["ContinuousCharacteristic"] =
            CreateEnumTable<ContinuousEffectCharacteristic>();
        gameTable["Layer"] = CreateEnumTable<ContinuousEffectLayer>();
        gameTable["RuleOperation"] = CreateEnumTable<RuleEffectOperation>();
        gameTable["Duration"] = CreateEnumTable<RuntimeEffectDuration>();
        gameTable["CardType"] = CreateEnumTable<CardType>();
        gameTable["Supertype"] = CreateEnumTable<CardSupertype>();
        gameTable["Color"] = CreateEnumTable<ManaColorFlags>();
        gameTable["Timing"] = CreateEnumTable<ScriptAbilityTiming>();
        gameTable["AbilityTrait"] =
            CreateEnumTable<ActivatedAbilityRuleTrait>();
        gameTable["TargetMode"] = CreateEnumTable<CardTargetMode>();
        gameTable["TargetKind"] = CreateEnumTable<SpellTargetKind>();
        gameTable["TargetZone"] = CreateEnumTable<SpellTargetZone>();
        gameTable["TargetController"] =
            CreateEnumTable<SpellTargetControllerRelation>();
        gameTable["TargetRelation"] = CreateEnumTable<SpellTargetRelation>();
        gameTable["CastCostKind"] = CreateEnumTable<SpellCastCostKind>();
        gameTable["FeatureRole"] = CreateEnumTable<CardFeatureRole>();
        var ruleKeys = new LuaTable(0, 3);
        ruleKeys["MaxHandSize"] = "MaxHandSize";
        ruleKeys["LandPlaysPerTurn"] = "LandPlaysPerTurn";
        ruleKeys["CastAsThoughFlash"] = "CastAsThoughFlash";
        gameTable["Rule"] = ruleKeys;
    }

    private static LuaTable CreateEnumTable<T>() where T : struct, Enum
    {
        string[] names = Enum.GetNames(typeof(T));
        var table = new LuaTable(0, names.Length);
        for (int i = 0; i < names.Length; i++)
        {
            object value = Enum.Parse(typeof(T), names[i]);
            table[names[i]] = Convert.ToInt32(value);
        }
        return table;
    }

    private LuaTable CreateCardProxy(RuntimeCard card)
    {
        CardDefinition data = card.Data;
        LuaTable proxy = new LuaTable(0, 28);
        proxy[RuntimeCardKey] = LuaValue.FromObject(card);
        proxy["Name"] = data?.cardName ?? string.Empty;
        proxy["ID"] = data?.cardID ?? 0;
        proxy["OracleID"] = data?.oracleID ?? string.Empty;
        proxy["SetCode"] = data?.setCode ?? string.Empty;
        proxy["CollectorNumber"] = data?.CollectorNumber ?? string.Empty;
        proxy["TypeLine"] = data?.GetTypeLine() ?? string.Empty;
        proxy["ManaCost"] = data?.manaCost ?? string.Empty;
        RefreshCardProxy(proxy, card);

        LuaTable dataProxy = new LuaTable(0, 8);
        dataProxy["cardName"] = data?.cardName ?? string.Empty;
        dataProxy["cardID"] = data?.cardID ?? 0;
        dataProxy["setCode"] = data?.setCode ?? string.Empty;
        dataProxy["collectorNumber"] = data?.CollectorNumber ?? string.Empty;
        dataProxy["manaCost"] = data?.manaCost ?? string.Empty;
        proxy["Data"] = dataProxy;
        return proxy;
    }

    private LuaTable CreateCardListTable(IReadOnlyList<RuntimeCard> cards)
    {
        int count = cards?.Count ?? 0;
        var table = new LuaTable(count, 0);
        for (int i = 0; i < count; i++)
        {
            table[i + 1] = CreateCardProxy(cards[i]);
        }
        return table;
    }

    private void RefreshCardProxy(LuaTable proxy, RuntimeCard card)
    {
        if (proxy == null || card == null) return;
        CardFeatureRuntime contextFeature =
            card.ResolvingFeature ?? card.SpellFeature ?? card.PrimaryFeature;

        proxy["Owner"] = card.OwnerPlayerID;
        proxy["ObjectID"] = card.ObjectId;
        proxy["Zone"] = (int)card.CurrentZone;
        proxy["OwnerPlayerID"] = card.OwnerPlayerID;
        proxy["Controller"] = card.ControllerPlayerID;
        proxy["ControllerPlayerID"] = card.ControllerPlayerID;
        proxy["CurrentAttack"] = card.CurrentAttack;
        proxy["CurrentDefence"] = card.CurrentDefence;
        proxy["CurrentDefense"] = card.CurrentDefence;
        proxy["DamageTaken"] = card.DamageTaken;
        proxy["IsTapped"] = card.IsTapped;
        proxy["IsDead"] = card.IsDead;
        proxy["CardTypes"] = (int)(contextFeature?.CardTypes ?? CardType.None);
        proxy["Colors"] = (int)(contextFeature?.GetColors() ?? ManaColorFlags.None);
        proxy["ManaValue"] = contextFeature?.ManaCost.Total ?? 0;
        proxy["Name"] = contextFeature?.CardName ?? card.Data?.cardName ?? string.Empty;
        proxy["FeatureRole"] = (int)(contextFeature?.Role ?? CardFeatureRole.Primary);
        proxy["FeatureIndex"] = contextFeature?.FeatureIndex ?? 0;
        proxy["FeatureFace"] = contextFeature?.FaceIndex ?? 0;
        proxy["ClassLevel"] = card.ClassLevel;
        proxy["IsCaseSolved"] = card.IsCaseSolved;
        proxy["IsPrepared"] = card.IsPrepared;
        proxy["ResolvingChapter"] = card.ResolvingChapter;

        GameEventContext gameEvent = card.ResolvingGameEvent;
        proxy["EventType"] = (int)(gameEvent?.type ?? GameEventType.None);
        proxy["EventAmount"] = gameEvent?.amount ?? 0;
        proxy["EventPlayer"] = gameEvent?.playerId ?? 0;
        proxy["EventFromZone"] = (int)(gameEvent?.fromZone ?? GameZone.None);
        proxy["EventToZone"] = (int)(gameEvent?.toZone ?? GameZone.None);
        proxy["EventPrevented"] = gameEvent?.prevented ?? false;
    }

    private LuaFunction CreateFunction(string name, Func<LuaFunctionExecutionContext, int> function)
    {
        return new LuaFunction(name, (context, _) => new ValueTask<int>(function(context)));
    }

    private int FirstUserArgumentIndex(LuaFunctionExecutionContext context)
    {
        if (context.ArgumentCount == 0) return 0;
        LuaValue first = context.GetArgument(0);
        return first.TryRead(out LuaTable table) && ReferenceEquals(table, gameTable) ? 1 : 0;
    }

    private RuntimeCard ReadCard(LuaValue value)
    {
        if (value.Type == LuaValueType.Nil) return null;
        if (value.TryRead(out RuntimeCard card)) return card;
        if (value.TryRead(out LuaTable table))
        {
            LuaValue runtimeValue = table[RuntimeCardKey];
            if (runtimeValue.TryRead(out card)) return card;
        }
        return null;
    }

    private int ReadInt(LuaFunctionExecutionContext context, int index)
    {
        if (!context.HasArgument(index)) return 0;
        LuaValue value = context.GetArgument(index);
        if (value.TryRead(out int intValue)) return intValue;
        if (value.TryRead(out double doubleValue)) return Mathf.RoundToInt((float)doubleValue);
        return 0;
    }

    private long ReadLong(LuaFunctionExecutionContext context, int index)
    {
        LuaValue value = context.GetArgument(index);
        if (value.TryRead(out long longValue)) return longValue;
        if (value.TryRead(out int intValue)) return intValue;
        if (value.TryRead(out double doubleValue)) return (long)Math.Round(doubleValue);
        return 0;
    }

    private string ReadString(LuaFunctionExecutionContext context, int index)
    {
        if (!context.HasArgument(index)) return string.Empty;
        LuaValue value = context.GetArgument(index);
        if (value.TryRead(out string stringValue)) return stringValue;
        return value.ToString();
    }

    private string ReadOptionalString(LuaFunctionExecutionContext context, int index)
    {
        return context.HasArgument(index) ? ReadString(context, index) : string.Empty;
    }

    private string ReadEnumName<T>(
        LuaFunctionExecutionContext context,
        int index) where T : struct, Enum
    {
        if (!context.HasArgument(index)) return string.Empty;
        LuaValue value = context.GetArgument(index);
        if (value.TryRead(out int intValue))
        {
            return Enum.IsDefined(typeof(T), intValue)
                ? Enum.GetName(typeof(T), intValue)
                : string.Empty;
        }
        if (value.TryRead(out double doubleValue))
        {
            intValue = (int)Math.Round(doubleValue);
            return Enum.IsDefined(typeof(T), intValue)
                ? Enum.GetName(typeof(T), intValue)
                : string.Empty;
        }
        return ReadString(context, index);
    }

    private string ReadOptionalEnumName<T>(
        LuaFunctionExecutionContext context,
        int index) where T : struct, Enum
    {
        return context.HasArgument(index)
            ? ReadEnumName<T>(context, index)
            : string.Empty;
    }

    private static string[] ReadStringList(LuaValue value)
    {
        if (!value.TryRead(out LuaTable table) || table.ArrayLength <= 0)
        {
            return Array.Empty<string>();
        }

        var values = new string[table.ArrayLength];
        for (int i = 0; i < values.Length; i++)
        {
            LuaValue item = table[(double)(i + 1)];
            values[i] = item.TryRead(out string text) ? text : item.ToString();
        }

        return values;
    }

    private List<RuntimeCard> ReadCardList(LuaValue value)
    {
        var cards = new List<RuntimeCard>();
        if (!value.TryRead(out LuaTable table) || table.ArrayLength <= 0)
        {
            return cards;
        }

        for (int i = 1; i <= table.ArrayLength; i++)
        {
            RuntimeCard card = ReadCard(table[(double)i]);
            if (card != null) cards.Add(card);
        }
        return cards;
    }

    private bool ReadBool(LuaFunctionExecutionContext context, int index)
    {
        if (!context.HasArgument(index)) return false;
        LuaValue value = context.GetArgument(index);
        if (value.TryRead(out bool boolValue)) return boolValue;
        if (value.TryRead(out int intValue)) return intValue != 0;
        return false;
    }

    private static T Complete<T>(ValueTask<T> task)
    {
        return task.IsCompletedSuccessfully ? task.Result : task.AsTask().GetAwaiter().GetResult();
    }

    private T ExecuteWithInstructionBudget<T>(Func<LuaState, ValueTask<T>> operation)
    {
        if (executionDepth >= MaximumExecutionDepth)
            throw new InvalidOperationException($"Lua callback depth exceeded ({MaximumExecutionDepth}).");
        bool outermost = executionDepth == 0;
        // Nested callbacks share script tables, but must not overwrite the suspended VM stack.
        LuaState thread = outermost ? state : state.CreateThread();
        if (outermost) remainingHookInstructions = HookInstructionBudget;
        else ConfigureInstructionGuard(thread);
        executionDepth++;
        try
        {
            // Charge short callbacks too, so crossing threads cannot reset or evade the budget.
            remainingHookInstructions -= HookInstructionInterval;
            if (remainingHookInstructions <= 0)
                throw new InvalidOperationException($"Lua instruction budget exceeded ({HookInstructionBudget}).");
            return Complete(operation(thread));
        }
        finally
        {
            executionDepth--;
            if (outermost) remainingHookInstructions = int.MaxValue;
            else thread.Dispose();
        }
    }

    private static void EnforceResourceQuota(
        LuaCSharpCardScriptInstance instance,
        string hookName)
    {
        if (instance == null) return;
        LuaResourceUsage usage = LuaResourceUsage.Measure(instance.Table);
        if (usage.EstimatedBytes <= MaximumRetainedLuaBytes
            && usage.TableCount <= MaximumRetainedLuaTables
            && usage.EntryCount <= MaximumRetainedLuaEntries)
        {
            return;
        }

        throw new LuaResourceQuotaException(
            $"Lua retained-state quota exceeded in '{hookName}': " +
            $"{usage.EstimatedBytes}/{MaximumRetainedLuaBytes} bytes, " +
            $"{usage.TableCount}/{MaximumRetainedLuaTables} tables, " +
            $"{usage.EntryCount}/{MaximumRetainedLuaEntries} entries.");
    }

    private static void Quarantine(
        LuaCSharpCardScriptInstance instance,
        string hookName,
        Exception exception)
    {
        if (instance == null) return;
        instance.Quarantine(exception?.Message);
        Debug.LogError(
            $"Lua card hook '{hookName}' quarantined " +
            $"[{GetCardKey(instance.Card)}]: {exception}");
    }

    private static string GetCardKey(RuntimeCard card)
    {
        if (card?.Data == null) return "unknown";
        return !string.IsNullOrWhiteSpace(card.Data.PrintingKey)
            ? card.Data.PrintingKey
            : $"{card.Data.setCode}/{card.Data.CollectorNumber}";
    }

    private sealed class LuaCSharpCardScriptInstance : ICardScriptInstance
    {
        public RuntimeCard Card { get; }
        public LuaTable Table { get; }
        public LuaTable CardProxy { get; }
        public bool IsQuarantined { get; private set; }
        public string QuarantineReason { get; private set; } = string.Empty;

        public LuaCSharpCardScriptInstance(RuntimeCard card, LuaTable table, LuaTable cardProxy)
        {
            Card = card;
            Table = table;
            CardProxy = cardProxy;
        }

        public bool HasFunction(string functionName)
        {
            if (IsQuarantined || string.IsNullOrEmpty(functionName)) return false;
            return Table[functionName].TryRead(out LuaFunction _);
        }

        public void Quarantine(string reason)
        {
            IsQuarantined = true;
            QuarantineReason = reason ?? string.Empty;
        }

        public void Dispose()
        {
            CardProxy[RuntimeCardKey] = LuaValue.Nil;
            Table["Card"] = LuaValue.Nil;
        }
    }

    private sealed class LuaResourceQuotaException : InvalidOperationException
    {
        public LuaResourceQuotaException(string message) : base(message)
        {
        }
    }

    private readonly struct LuaResourceUsage
    {
        public int EstimatedBytes { get; }
        public int TableCount { get; }
        public int EntryCount { get; }

        private LuaResourceUsage(
            int estimatedBytes,
            int tableCount,
            int entryCount)
        {
            EstimatedBytes = estimatedBytes;
            TableCount = tableCount;
            EntryCount = entryCount;
        }

        public static LuaResourceUsage Measure(LuaTable root)
        {
            int bytes = 0;
            int tableCount = 0;
            int entryCount = 0;
            var visited = new HashSet<LuaTable>(LuaTableReferenceComparer.Instance);
            var pending = new Stack<LuaTable>();
            if (root != null) pending.Push(root);

            while (pending.Count > 0)
            {
                LuaTable table = pending.Pop();
                if (table == null || !visited.Add(table)) continue;

                tableCount++;
                bytes = SaturatingAdd(bytes, 64);
                foreach (KeyValuePair<LuaValue, LuaValue> pair in table)
                {
                    entryCount++;
                    bytes = SaturatingAdd(bytes, 32);
                    MeasureValue(pair.Key, pending, ref bytes);
                    MeasureValue(pair.Value, pending, ref bytes);
                    if (bytes > MaximumRetainedLuaBytes
                        || tableCount > MaximumRetainedLuaTables
                        || entryCount > MaximumRetainedLuaEntries)
                    {
                        return new LuaResourceUsage(bytes, tableCount, entryCount);
                    }
                }

                if (table.Metatable != null) pending.Push(table.Metatable);
            }

            return new LuaResourceUsage(bytes, tableCount, entryCount);
        }

        private static void MeasureValue(
            LuaValue value,
            Stack<LuaTable> pending,
            ref int bytes)
        {
            bytes = SaturatingAdd(bytes, 16);
            if (value.TryRead(out string text))
            {
                bytes = SaturatingAdd(bytes, Encoding.UTF8.GetByteCount(text));
            }
            else if (value.TryRead(out LuaTable table) && table != null)
            {
                pending.Push(table);
            }
            else if (value.Type != LuaValueType.Nil)
            {
                bytes = SaturatingAdd(bytes, 32);
            }
        }

        private static int SaturatingAdd(int left, int right)
        {
            if (right <= 0) return left;
            return left >= int.MaxValue - right ? int.MaxValue : left + right;
        }
    }

    private sealed class LuaTableReferenceComparer : IEqualityComparer<LuaTable>
    {
        public static readonly LuaTableReferenceComparer Instance =
            new LuaTableReferenceComparer();

        public bool Equals(LuaTable left, LuaTable right)
        {
            return ReferenceEquals(left, right);
        }

        public int GetHashCode(LuaTable value)
        {
            return System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(value);
        }
    }
}
