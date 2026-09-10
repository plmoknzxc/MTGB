#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class SpecialistBoundaryValidationMenu
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    [MenuItem("MTGB/Validate Specialist Boundary Cards")]
    public static void ValidateForAutomation()
    {
        Require(!EditorApplication.isPlaying, "Stop Play Mode before validation.");
        var errors = new List<string>();
        Application.LogCallback capture = (message, trace, type) =>
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors.Add(message);
        };
        Application.logMessageReceived += capture;
        try { ValidateCases(); }
        finally { Application.logMessageReceived -= capture; }
        Require(errors.Count == 0, "Specialist validation logged errors: " + string.Join("; ", errors));
        Debug.Log("Specialist boundary cards TST/14-21 passed: costs, cancellation, stale targets, replacements, layers, Adventure and Saga.");
    }

    private static void ValidateCases()
    {
        CardDatabaseRepository.LoadAll(true);
        DeckRoundTrip();
        foreach (int seat in new[] { 1, 2 })
        {
            SacrificeCost(seat);
            OptionalCost(seat, false);
            OptionalCost(seat, true);
            Adventure(seat, false);
            Adventure(seat, true);
        }
        ReplacementZones();
        DamageReplacement();
        ContinuousLayers();
        Saga(false);
        Saga(true);
        ValidateNestedCallbacks();
    }

    [MenuItem("MTGB/Validate Nested Lua Callbacks")]
    public static void ValidateNestedCallbacks()
    {
        var errors = new List<string>();
        Application.LogCallback capture = (message, trace, type) =>
        {
            if (type == LogType.Error || type == LogType.Exception) errors.Add(message);
        };
        Application.logMessageReceived += capture;
        try
        {
            using var f = new Fixture();
            var replacement = f.Add(new CardDefinition
            {
                cardName = "Nested Lua Replacement", setCode = "VALIDATION", collectorNumberText = "nested",
                cardTypes = CardType.Enchantment,
                luaScript = @"local Card = { ApiVersion = 2 }
function Card.OnInit(self)
 Game.RegisterReplacementEffect(self.Card, Game.Event.Damage, Game.Replacement.InvokeHook, 'Replace', 0, Game.Duration.WhileSourceRemains, 'Filter')
 Game.RegisterReplacementEffect(self.Card, Game.Event.LifeChanged, Game.Replacement.InvokeHook, 'Inner', 0, Game.Duration.WhileSourceRemains, 'LifeFilter')
end
function Card.Filter(self) return Game.GetEventAmount(self.Card) == 2 end
function Card.LifeFilter(self) return Game.GetEventAmount(self.Card) > 0 end
function Card.Inner(self) Game.SetEventAmount(self.Card, Game.GetEventAmount(self.Card) + 1) end
function Card.Replace(self)
 Game.SetEventAmount(self.Card, 3)
 Game.GainLifeForOwner(self.Card, 1)
 self.restored = Game.GetEventType(self.Card) == Game.Event.Damage and Game.GetEventAmount(self.Card) == 3
end
function Card.Restored(self) return self.restored end
return Card"
            }, 1);
            Require(f.Rules.MoveCardToZone(replacement, GameZone.Battlefield), "Cannot arrange Lua replacement.");
            var spell = f.Add(new CardDefinition
            {
                cardName = "Nested Lua Damage", setCode = "VALIDATION", collectorNumberText = "damage",
                cardTypes = CardType.Instant, manaCost = "{R}",
                luaScript = @"local Card = { ApiVersion = 2 }
function Card.OnPlay(self)
 Game.DealDamageToPlayer(self.Card, 2, 2)
 Game.GainLifeForOwner(self.Card, 1)
end
return Card"
            }, 1);
            int before = f.Rules.GetLife(2), ownerBefore = f.Rules.GetLife(1);
            f.Cast(spell); f.Resolve();
            Require(f.Rules.GetLife(2) == before - 3, "Lua-originated damage did not apply custom replacement callback.");
            Require(f.Rules.GetLife(1) == ownerBefore + 4, "Outer Lua execution did not resume after nested callback.");
            Require(CardScriptRuntimeManager.Instance.InvokeBoolean(replacement.ScriptInstance, "Restored", false),
                "Same-source nested callback did not restore the outer event context.");
            Require(replacement.ResolvingGameEvent == null, "Nested callback left a stale event context.");
            Require(errors.Count == 0, "Nested Lua callback errors: " + string.Join("; ", errors));
        }
        finally { Application.logMessageReceived -= capture; }
        Debug.Log("Nested Lua callback validation passed.");
    }

    private static CardDefinition Find(int number) => CardDatabaseRepository.LoadAll()
        .Single(c => c.setCode == "TST" && c.collectorNumber == number);

    private static void DeckRoundTrip()
    {
        var deck = DeckRepository.Load(Path.Combine(PrintingKeyUtility.ApplicationRoot, "Decks", "BoundarySpecialists.mdk"));
        Require(DeckRepository.BuildRuntimeDeck(deck, CardDatabaseRepository.LoadAll(), out string warning).Count == 60
            && string.IsNullOrEmpty(warning), "Specialist deck must contain 60 resolvable cards.");
        var empty = new DeckDefinition { name = "__SpecialistValidation_" + Guid.NewGuid().ToString("N") };
        string path = null;
        try
        {
            for (int n = 14; n <= 21; n++) Require(DeckRepository.AddCard(empty, Find(n), 4) == 4, "Cannot add TST/" + n);
            path = DeckRepository.Save(empty);
            Require(DeckRepository.BuildRuntimeDeck(DeckRepository.Load(path), CardDatabaseRepository.LoadAll(), out warning).Count == 32
                && string.IsNullOrEmpty(warning), "Specialist deck save/reload lost cards.");
        }
        finally { if (path != null) File.Delete(path); }
    }

    private static void SacrificeCost(int seat)
    {
        using var f = new Fixture(seat);
        var spell = f.Card(14, seat);
        var victim = f.Creature(3 - seat);
        Require(!f.Rules.CanPlayCard(seat, f.HandIndex(spell)), "Mandatory sacrifice allowed with no permanent.");
        var offering = f.Creature(seat);
        string mana = f.Rules.GetManaPool(seat).ToDisplayString();
        f.Begin(spell);
        f.Select(victim); f.Confirm();
        Require(f.Choice?.kind == GameChoiceKind.SelectOptions
            && f.Choice.choosingPlayerId == seat, $"P{seat} must choose its sacrifice, not auto-pay it.");
        f.Select(offering); f.Confirm();
        Require(f.Choice?.kind == GameChoiceKind.CastConfirmation, "Missing final casting confirmation.");
        f.Cancel();
        Require(offering.CurrentZone == GameZone.Battlefield && spell.CurrentZone == GameZone.Hand
            && f.Rules.GetManaPool(seat).ToDisplayString() == mana, "Cancelled sacrifice paid a cost.");
        f.Cast(spell, victim, offering);
        Require(offering.CurrentZone == GameZone.Graveyard, "Sacrifice not paid on commit.");
        Require(f.Rules.ReturnCardToHand(victim), "Cannot invalidate target.");
        Require(f.Rules.MoveCardToZone(victim, GameZone.Battlefield), "Cannot re-enter target.");
        f.Resolve();
        Require(victim.DamageTaken == 0 && offering.CurrentZone == GameZone.Graveyard
            && spell.CurrentZone == GameZone.Graveyard, "Stale target resolved or paid sacrifice was refunded.");
        f.Views();
    }

    private static void OptionalCost(int seat, bool pay)
    {
        using var f = new Fixture(seat);
        var spell = f.Card(15, seat); var victim = f.Creature(3 - seat);
        int life = f.Rules.GetLife(seat);
        f.Begin(spell);
        Require(f.Choice?.kind == GameChoiceKind.SelectOptions, "Optional cost choice missing.");
        if (pay) f.Toggle(f.Choice.options[0].optionId);
        f.Confirm(); f.Select(victim); f.Confirm();
        f.Cancel();
        Require(f.Rules.GetLife(seat) == life && victim.DamageTaken == 0, "Cancellation spent optional life or resolved damage.");
        f.Cast(spell, victim, optional: pay);
        Require(f.Rules.GetLife(seat) == life - (pay ? 2 : 0), "Optional life payment wrong.");
        f.Resolve();
        Require(victim.DamageTaken == (pay ? 3 : 1), "Additional cost declaration was not preserved at resolution.");
        if (pay)
        {
            var second = f.Card(15, seat); int before = f.Rules.GetLife(seat);
            f.Cast(second, victim, optional: true);
            f.Rules.ReturnCardToHand(victim); f.Rules.MoveCardToZone(victim, GameZone.Battlefield);
            f.Resolve();
            Require(f.Rules.GetLife(seat) == before - 2 && victim.DamageTaken == 0
                && second.CurrentZone == GameZone.Graveyard, "Fizzled spell refunded life or hit a new target identity.");
        }
        f.Views();
    }

    private static void ReplacementZones()
    {
        using var f = new Fixture();
        var source = f.Card(16); f.Cast(source); f.Resolve();
        var victim = f.Creature(2);
        Require(f.Rules.MoveCardToZone(victim, GameZone.Graveyard) && victim.CurrentZone == GameZone.Exile,
            "Zone replacement failed to exile creature.");
        Require(f.Rules.ReturnCardToHand(source), "Cannot remove replacement source.");
        var next = f.Creature(2);
        Require(f.Rules.MoveCardToZone(next, GameZone.Graveyard) && next.CurrentZone == GameZone.Graveyard,
            "Replacement survived source leaving.");
        f.Cast(source); f.Resolve();
        var third = f.Creature(2);
        Require(f.Rules.MoveCardToZone(third, GameZone.Graveyard) && third.CurrentZone == GameZone.Exile,
            "Re-entered replacement source did not register anew.");
        f.Views();
    }

    private static void DamageReplacement()
    {
        using var f = new Fixture();
        var first = f.Card(17); var second = f.Card(17);
        f.Cast(first); f.Resolve(); f.Cast(second); f.Resolve();
        int life = f.Rules.GetLife(2);
        f.Rules.DealDamageToPlayer(first, 2, 1);
        if (f.Choice != null)
        {
            Require(f.Choice.kind == GameChoiceKind.ReplacementEffect && f.Choice.choosingPlayerId == 2,
                "Replacement order must belong to affected player.");
            Require(!f.TryCancel().accepted, "Mandatory replacement ordering was cancelled.");
            f.Toggle(f.Choice.options.First(o => o.legal).optionId); f.Confirm();
        }
        Require(f.Rules.GetLife(2) == life - 4, "Two damage doublers must apply once each.");
        Require(f.Rules.ReturnCardToHand(first), "Cannot remove first doubler.");
        f.Rules.DealDamageToPlayer(second, 2, 1);
        Require(f.Rules.GetLife(2) == life - 6, "One removed doubler still applied.");
        Require(f.Rules.ReturnCardToHand(second), "Cannot remove second doubler.");
        f.Rules.DealDamageToPlayer(second, 2, 1);
        Require(f.Rules.GetLife(2) == life - 7, "Damage replacements survived source removal.");
        f.Views();
    }

    private static void ContinuousLayers()
    {
        using var f = new Fixture();
        var creature = f.Creature(1); var first = f.Card(18); var second = f.Card(18);
        f.Cast(first); f.Resolve(); f.Cast(second); f.Resolve();
        Require(creature.CurrentAttack == 5 && creature.CurrentDefence == 7, "Anthems did not stack on 3/5.");
        var reshape = f.Card(19); f.Cast(reshape, creature); f.Resolve();
        Require(creature.CurrentAttack == 4 && creature.CurrentDefence == 5, "Base-setting layer did not precede two +1/+1 effects.");
        Require(f.Rules.ReturnCardToHand(first), "Cannot remove anthem.");
        Require(creature.CurrentAttack == 3 && creature.CurrentDefence == 4, "Anthem removal did not clean its modifier.");
        f.Submit(new GameAction { type = GameActionType.EndTurn, playerId = 1 });
        while (f.Choice != null)
        {
            if (!f.Choice.CanConfirm) f.Toggle(f.Choice.options.First(o => o.legal && !f.Choice.IsSelected(o.optionId)).optionId);
            else f.Confirm();
        }
        Require(creature.CurrentAttack == 4 && creature.CurrentDefence == 6, "Temporary base-setting survived cleanup.");
        Require(f.Rules.ReturnCardToHand(creature) && f.Rules.MoveCardToZone(creature, GameZone.Battlefield), "Cannot reset creature identity.");
        Require(creature.CurrentAttack == 4 && creature.CurrentDefence == 6, "New object retained temporary base-setting.");
        Require(f.Rules.ReturnCardToHand(second), "Cannot remove final anthem.");
        Require(creature.CurrentAttack == 3 && creature.CurrentDefence == 5, "Persistent anthem effect leaked.");
        f.Views();
    }

    private static void Adventure(int seat, bool invalidate)
    {
        using var f = new Fixture(seat);
        var card = f.Card(20, seat); var target = f.Creature(3 - seat);
        f.Begin(card, 1); f.Select(target); f.Confirm(); f.Cancel();
        Require(card.CurrentZone == GameZone.Hand && !card.CanCastPrimaryFromAdventureExile, "Cancelled Adventure granted exile permission.");
        f.Cast(card, target, feature: 1);
        if (invalidate) { f.Rules.ReturnCardToHand(target); f.Rules.MoveCardToZone(target, GameZone.Battlefield); }
        f.Resolve();
        if (invalidate)
        {
            Require(card.CurrentZone == GameZone.Graveyard && !card.CanCastPrimaryFromAdventureExile
                && target.CurrentZone == GameZone.Battlefield, "Fizzled Adventure granted exile casting permission.");
        }
        else
        {
            Require(card.CurrentZone == GameZone.Exile && card.CanCastPrimaryFromAdventureExile
                && target.CurrentZone == GameZone.Hand, "Resolved Adventure did not exile with permission.");
            f.Begin(card); f.Cancel();
            Require(card.CurrentZone == GameZone.Exile && card.CanCastPrimaryFromAdventureExile, "Cancelling exile cast consumed permission.");
            f.Cast(card); f.Resolve();
            Require(card.CurrentZone == GameZone.Battlefield && card.HasType(CardType.Creature)
                && !card.CanCastPrimaryFromAdventureExile, "Exile cast did not consume Adventure permission.");
            f.Rules.MoveCardToZone(card, GameZone.Exile);
            Require(!card.CanCastPrimaryFromAdventureExile, "Unrelated exile restored Adventure permission.");
        }
        f.Views();
    }

    private static void Saga(bool removeEarly)
    {
        using var f = new Fixture();
        var saga = f.Card(21); int life = f.Rules.GetLife(1);
        f.Begin(saga); f.Cancel();
        Require(saga.GetCounters("lore") == 0 && f.Rules.GetStackCount() == 0, "Cancelled Saga created chapter state.");
        f.Cast(saga); f.Resolve();
        Require(saga.GetCounters("lore") == 1 && f.Rules.GetStackCount() == 1, "Saga entry did not queue chapter I.");
        if (removeEarly) f.Rules.ReturnCardToHand(saga);
        f.Resolve();
        Require(f.Rules.GetLife(1) == life + 2, "Chapter I failed (including when source left after triggering).");
        if (removeEarly)
        {
            Require(saga.GetCounters("lore") == 0, "Saga lore survived zone change.");
            f.Cast(saga); f.Resolve(); f.Resolve();
            Require(saga.GetCounters("lore") == 1 && f.Rules.GetLife(1) == life + 4, "Saga re-entry did not restart chapter I exactly once.");
        }
        int hand = f.Rules.GetHandCount(1);
        f.AdvanceToNextMain(1);
        Require(saga.GetCounters("lore") == 2, "Next precombat main did not advance saga to II.");
        f.Resolve();
        Require(f.Rules.GetHandCount(1) == hand + 2, "Normal draw plus chapter II must draw exactly two.");
        f.AdvanceToNextMain(1);
        Require(saga.GetCounters("lore") == 3, "Next precombat main did not advance saga to III.");
        Require(saga.CurrentZone == GameZone.Battlefield, "Saga sacrificed before final chapter resolved.");
        f.Resolve();
        Require(saga.CurrentZone == GameZone.Graveyard && saga.GetCounters("lore") == 0
            && f.Rules.GetLife(1) == life + (removeEarly ? 7 : 5), "Saga final chapter/cleanup failed.");
        f.Views();
    }

    private sealed class Fixture : IDisposable
    {
        public readonly GameRules Rules;
        public GameChoiceRequest Choice => Rules.GetActiveChoice();
        private readonly GameObject root;
        private readonly IDisposable scope;
        private readonly Dictionary<FieldInfo, object> events = new Dictionary<FieldInfo, object>();
        private long nextId = 100000;
        public Fixture(int seat = 1)
        {
            foreach (var field in typeof(GameEvents).GetFields(BindingFlags.Static | BindingFlags.NonPublic))
                if (typeof(Delegate).IsAssignableFrom(field.FieldType)) { events.Add(field, field.GetValue(null)); field.SetValue(null, null); }
            root = new GameObject("Specialist Boundary Fixture"); Rules = root.AddComponent<GameRules>();
            scope = CardScriptRuntimeManager.UseRules(Rules);
            var deck = DeckRepository.Load(Path.Combine(PrintingKeyUtility.ApplicationRoot, "Decks", "BoundaryResponses.mdk"));
            Rules.InitializeForOnlineSession(deck, deck, 7241, 20, seat);
            while (Choice != null)
            {
                if (Choice.CanConfirm) Confirm(); else Toggle(1);
            }
            typeof(GameRules).GetField("player1FullControl", Private).SetValue(Rules, true);
            typeof(GameRules).GetField("player2FullControl", Private).SetValue(Rules, true);
            foreach (ManaColor color in new[] { ManaColor.White, ManaColor.Blue, ManaColor.Black, ManaColor.Red, ManaColor.Green })
                for (int p = 1; p <= 2; p++) Rules.AddMana(p, color, 20);
        }
        public RuntimeCard Card(int number, int seat = 1) => Add(Find(number), seat);
        public RuntimeCard Creature(int seat)
        {
            var card = Add(new CardDefinition { cardName = "Boundary 3/5", cardTypes = CardType.Creature, attack = 3, defence = 5 }, seat);
            Require(Rules.MoveCardToZone(card, GameZone.Battlefield), "Cannot arrange creature."); return card;
        }
        public RuntimeCard Add(CardDefinition definition, int seat)
        {
            var card = new RuntimeCard(definition, seat); card.ReassignObjectId(nextId++); card.SetCurrentZone(GameZone.Hand);
            Require(string.IsNullOrEmpty(definition.luaScript) || card.ScriptInstance != null, "Specialist Lua failed to load: " + definition.cardName);
            ((List<RuntimeCard>)typeof(GameRules).GetField("player" + seat + "Hand", Private).GetValue(Rules)).Insert(0, card);
            return card;
        }
        public int HandIndex(RuntimeCard card) => Enumerable.Range(0, Rules.GetHandCount(card.ControllerPlayerID))
            .Single(i => Rules.GetHandCard(card.ControllerPlayerID, i) == card);
        public void Begin(RuntimeCard card, int feature = 0)
        {
            int seat = card.ControllerPlayerID;
            if (card.CurrentZone == GameZone.Exile)
            {
                int index = ((List<RuntimeCard>)typeof(GameRules).GetField("player" + seat + "Exile", Private).GetValue(Rules)).IndexOf(card);
                Submit(new GameAction { type = GameActionType.BeginExileCast, playerId = seat, zoneIndex = index });
            }
            else Submit(new GameAction { type = GameActionType.BeginHandCast, playerId = seat, zoneIndex = HandIndex(card) });
            if (Choice?.kind == GameChoiceKind.CastFeature)
                Submit(new GameAction { type = GameActionType.SelectCastFeature, playerId = seat, featureIndex = feature });
        }
        public void Cast(RuntimeCard card, RuntimeCard target = null, RuntimeCard sacrifice = null, bool optional = false, int feature = 0)
        {
            Begin(card, feature);
            for (int guard = 0; Choice != null && guard < 16; guard++)
            {
                if (Choice.kind == GameChoiceKind.SpellTarget) Select(target);
                else if (Choice.kind == GameChoiceKind.SelectOptions && Choice.minimumSelections > 0) Select(sacrifice);
                else if (Choice.kind == GameChoiceKind.SelectOptions && optional) Toggle(Choice.options[0].optionId);
                Confirm();
            }
            Require(Choice == null && card.CurrentZone == GameZone.Stack, "Casting failed to put spell on stack.");
        }
        public void Select(RuntimeCard card)
        {
            Require(card != null, "Missing selected card for " + Choice?.kind);
            var option = Choice.options.FirstOrDefault(o => o.legal && (o.objectId == card.ObjectId
                || (o.objectId == 0 && o.ownerPlayerId == card.ControllerPlayerID
                    && o.zone == GameChoiceZone.Battlefield && Rules.GetBattlefieldCard(o.ownerPlayerId, o.zoneIndex) == card)));
            Require(option != null, "Card not offered by " + Choice.kind + ": " + card.Data.cardName);
            Toggle(option.optionId);
        }
        public void Toggle(int id) => Submit(new GameAction { type = GameActionType.ToggleChoiceOption,
            playerId = Choice.choosingPlayerId, choiceRequestId = Choice.requestId, optionId = id });
        public void Confirm() => Submit(new GameAction { type = GameActionType.ConfirmChoice,
            playerId = Choice.choosingPlayerId, choiceRequestId = Choice.requestId });
        public GameActionResult TryCancel() => Rules.ApplyAction(new GameAction { type =
            Choice.kind == GameChoiceKind.CastConfirmation || Choice.kind == GameChoiceKind.SpellTarget || Choice.kind == GameChoiceKind.CastFeature
                ? GameActionType.CancelCast : GameActionType.CancelChoice,
            playerId = Choice.choosingPlayerId, choiceRequestId = Choice.requestId, expectedStateSequence = Rules.GetAppliedActionSequence() });
        public void Cancel() { Require(TryCancel().accepted, "Choice cancellation rejected."); }
        public void Submit(GameAction action)
        {
            action.expectedStateSequence = Rules.GetAppliedActionSequence(); var result = Rules.ApplyAction(action);
            Require(result.accepted, $"P{action.playerId} {action.type} rejected: {result.error}; choice={Choice?.kind}");
        }
        public void Resolve()
        {
            var stack = (List<GameStackItem>)typeof(GameRules).GetField("stack", Private).GetValue(Rules);
            Require(stack.Count > 0, "No stack item to resolve.");
            var top = stack[stack.Count - 1];
            for (int guard = 0; stack.Contains(top) && guard < 3; guard++)
                Submit(new GameAction { type = GameActionType.PassPriority, playerId = Rules.GetPriorityPlayer() });
            Require(!stack.Contains(top), "Two priority passes did not resolve the stack item.");
        }
        public void AdvanceToNextMain(int seat)
        {
            int turn = Rules.GetTurnNumber();
            for (int guard = 0; guard < 150; guard++)
            {
                if (Rules.GetTurnNumber() > turn && Rules.GetCurrentPlayer() == seat && Rules.GetCurrentPhase() == TurnPhase.Main1) return;
                if (Choice != null)
                {
                    Require(Choice.kind == GameChoiceKind.DiscardToHandSize, "Unexpected choice during phase progression: " + Choice.kind);
                    if (Choice.CanConfirm) Confirm();
                    else Toggle(Choice.options.First(o => o.legal && !Choice.IsSelected(o.optionId)).optionId);
                }
                else
                {
                    int actor = Rules.IsAwaitingPriority() ? Rules.GetPriorityPlayer() : Rules.GetCurrentPlayer();
                    Submit(new GameAction { playerId = actor, type = Rules.GetPrimaryAdvanceActionType(actor) });
                }
            }
            throw new InvalidOperationException("Could not reach next precombat main phase.");
        }
        public void Views()
        {
            for (int seat = 1; seat <= 2; seat++)
            {
                var obj = new GameObject("Specialist View"); var client = obj.AddComponent<GameRules>();
                try
                {
                    var view = JsonUtility.FromJson<OnlinePlayerViewState>(JsonUtility.ToJson(Rules.CreateOnlinePlayerView(seat)));
                    Require(client.ApplyOnlinePlayerView(view, out string error), error);
                    Require(client.GetLife(1) == Rules.GetLife(1) && client.GetLife(2) == Rules.GetLife(2)
                        && client.GetStackCount() == Rules.GetStackCount(), "Specialist player view differs.");
                    foreach (int player in new[] { 1, 2 })
                    {
                        var expected = Rules.GetBattlefieldCards(player); var actual = client.GetBattlefieldCards(player);
                        Require(expected.Count == actual.Count, "Client battlefield count differs.");
                        for (int i = 0; i < expected.Count; i++)
                            Require(expected[i].CurrentAttack == actual[i].CurrentAttack
                                && expected[i].CurrentDefence == actual[i].CurrentDefence
                                && expected[i].GetCounters("lore") == actual[i].GetCounters("lore"), "Client lost layered stats or Saga lore.");
                    }
                }
                finally { typeof(GameRules).GetMethod("OnDestroy", Private).Invoke(client, null); UnityEngine.Object.DestroyImmediate(obj); }
            }
        }
        public void Dispose()
        {
            typeof(GameRules).GetMethod("OnDestroy", Private).Invoke(Rules, null);
            UnityEngine.Object.DestroyImmediate(root); scope.Dispose(); MatchStartConfig.ClearOnlineSession();
            foreach (var entry in events) entry.Key.SetValue(null, entry.Value);
        }
    }
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
#endif
