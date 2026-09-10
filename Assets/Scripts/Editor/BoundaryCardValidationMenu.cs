#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class BoundaryCardValidationMenu
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

    [MenuItem("MTGB/Validate Boundary Cards")]
    public static void ValidateForAutomation()
    {
        ValidateDeckRoundTrip();
        ValidateBounce(false);
        ValidateBounce(true);
        ValidateCounter();
        ValidateOptionalTargets(0);
        ValidateOptionalTargets(1);
        ValidateOptionalTargets(2);
        ValidateX(0);
        ValidateX(3);
        ValidateCastChoices();
        Debug.Log("Boundary cards TST/10-13 passed: deck, resolution, target identity, optional targets, X and two-seat view round-trips.");
    }

    private static void ValidateDeckRoundTrip()
    {
        var cards = CardDatabaseRepository.LoadAll(true);
        var deck = new DeckDefinition { name = "__BoundaryValidation_" + Guid.NewGuid().ToString("N") };
        string saved = null;
        try
        {
            for (int number = 10; number <= 13; number++)
                Require(DeckRepository.AddCard(deck, Find(number), 4) == 4, "Could not add boundary card to an empty deck.");
            saved = DeckRepository.Save(deck);
            var restored = DeckRepository.Load(saved);
            var runtime = DeckRepository.BuildRuntimeDeck(restored, cards, out string warning);
            Require(runtime.Count == 16 && string.IsNullOrEmpty(warning), "Boundary deck save/load lost cards.");
            var sample = DeckRepository.Load(Path.Combine(PrintingKeyUtility.ApplicationRoot, "Decks", "BoundaryResponses.mdk"));
            Require(DeckRepository.BuildRuntimeDeck(sample, cards, out warning).Count == 60
                && string.IsNullOrEmpty(warning), "Boundary sample deck is not 60 resolvable cards.");
        }
        finally
        {
            if (saved != null && File.Exists(saved)) File.Delete(saved);
        }
    }

    private static void ValidateBounce(bool invalidate)
    {
        using var f = new Fixture();
        var target = f.Creature(2);
        var spell = f.Card(10);
        f.Stack(spell, "creature", SpellTargetReference.ForCard(target));
        if (invalidate)
        {
            Require(f.Rules.ReturnCardToHand(target), "Could not remove the bounce target.");
            Require(f.Rules.MoveCardToZone(target, GameZone.Battlefield), "Could not re-enter the target.");
        }
        Require(f.Rules.ResolveTopOfStack(), "Bounce did not resolve.");
        Require(target.CurrentZone == (invalidate ? GameZone.Battlefield : GameZone.Hand),
            "Bounce followed a stale target identity or failed to return its legal target.");
        Require(spell.CurrentZone == GameZone.Graveyard, "Bounce did not finish in graveyard.");
        f.CheckViews();
    }

    private static void ValidateCounter()
    {
        using var f = new Fixture();
        var counter = f.Card(11);
        f.Rules.AddMana(1, ManaColor.Blue, 2);
        Require(!f.Rules.CanPlayCard(1, 0), "Counterspell was playable with no spell on the stack.");
        var creature = f.Creature(2);
        f.Push(GameStackItem.ActivatedAbility(creature, "UnusedValidationHook"));
        Require(!f.Rules.CanPlayCard(1, 0), "Counterspell treated an activated ability as a spell.");
        f.ClearStack();
        var draw = f.Card(4, 2);
        var target = f.Stack(draw, null);
        int handBefore = f.Rules.GetHandCount(2);
        Require(f.Rules.CanPlayCard(1, 0), "Counterspell could not respond to an actual spell.");
        f.Stack(counter, "spell", SpellTargetReference.ForStackItem(target));
        f.CheckViews();
        Require(f.Rules.ResolveTopOfStack(), "Counterspell did not resolve.");
        Require(f.Rules.GetStackCount() == 0 && draw.CurrentZone == GameZone.Graveyard
            && counter.CurrentZone == GameZone.Graveyard && f.Rules.GetHandCount(2) == handBefore,
            "Countered draw spell still resolved or remained on the stack.");
        f.CheckViews();
    }

    private static void ValidateOptionalTargets(int invalidCount)
    {
        using var f = new Fixture();
        var spell = f.Card(12);
        if (invalidCount == 0)
        {
            f.Stack(spell, "creatures");
            Require(f.Rules.ResolveTopOfStack() && spell.CurrentZone == GameZone.Graveyard,
                "Zero-target optional spell failed to resolve.");
        }
        else
        {
            var first = f.Creature(1);
            var second = f.Creature(2);
            f.Stack(spell, "creatures", SpellTargetReference.ForCard(first), SpellTargetReference.ForCard(second));
            Require(f.Rules.ReturnCardToHand(first), "Could not invalidate first optional target.");
            if (invalidCount == 2) Require(f.Rules.ReturnCardToHand(second), "Could not invalidate second target.");
            Require(f.Rules.ResolveTopOfStack(), "Optional target spell did not finish resolving.");
            Require(first.CurrentAttack == 2 && second.CurrentAttack == (invalidCount == 1 ? 3 : 2),
                "Partial/all-target invalidation applied the wrong buffs.");
            second.ClearEndOfTurnEffects();
            Require(second.CurrentAttack == 2, "Optional target buff did not expire at end of turn.");
        }
        f.CheckViews();
    }

    private static void ValidateX(int x)
    {
        using var f = new Fixture();
        var spell = f.Card(13);
        f.StackWithX(spell, x, "player", SpellTargetReference.ForPlayer(2));
        Require(f.Rules.ResolveTopOfStack() && f.Rules.GetLife(2) == 20 - x,
            "X damage did not use the locked declaration value.");
        f.CheckViews();
    }

    private static void ValidateCastChoices()
    {
        using (var f = new Fixture())
        {
            f.Card(13);
            f.Rules.AddMana(1, ManaColor.Red, 1);
            f.Rules.AddMana(1, ManaColor.Colorless, 3);
            Require(f.Rules.BeginCastConfirmationChoice(1, 0), "Could not begin X cast declaration.");
            var choice = f.Rules.GetActiveChoice();
            Require(choice != null && choice.selectionMode == GameChoiceSelectionMode.Number
                && choice.minimumValue == 0 && choice.maximumValue == 3, "X chooser ignored available mana.");
            Require(!f.Rules.TrySetChoiceNumber(1, choice.requestId, 4), "X chooser accepted unaffordable X.");
            Require(f.Rules.TrySetChoiceNumber(1, choice.requestId, 3)
                && f.Rules.TryConfirmActiveChoice(1), "Could not lock affordable X.");
            choice = f.Rules.GetActiveChoice();
            var opponent = choice.options.Find(o => o.legal && o.zone == GameChoiceZone.Player && o.ownerPlayerId == 2);
            Require(opponent != null && f.Rules.TryToggleChoiceOption(1, opponent.optionId)
                && f.Rules.TryConfirmActiveChoice(1), "Could not select X spell player target.");
            Require(f.Rules.GetActiveChoice()?.kind == GameChoiceKind.CastConfirmation
                && f.Rules.TryConfirmActiveChoice(1), "Could not confirm X spell.");
            Require(f.Rules.GetManaPool(1).Total == 0, "X spell did not pay X plus colored mana.");
            Require(f.Rules.ResolveTopOfStack() && f.Rules.GetLife(2) == 17, "Declared X spell resolved incorrectly.");
            f.CheckViews();
        }
        using (var f = new Fixture())
        {
            f.Card(12);
            f.Creature(1); f.Creature(2); f.Creature(2);
            f.Rules.AddMana(1, ManaColor.Green, 1);
            Require(f.Rules.BeginCastConfirmationChoice(1, 0), "Could not begin optional target choice.");
            var choice = f.Rules.GetActiveChoice();
            Require(choice.minimumSelections == 0 && choice.maximumSelections == 2 && choice.CanConfirm,
                "Optional choice does not allow zero to two targets.");
            var options = choice.options.Where(o => o.legal).ToArray();
            Require(options.Length == 3 && f.Rules.TryToggleChoiceOption(1, options[0].optionId)
                && f.Rules.TryToggleChoiceOption(1, options[1].optionId)
                && !f.Rules.TryToggleChoiceOption(1, options[2].optionId), "Optional target choice exceeded two targets.");
            f.CheckViews();
        }
    }

    private static CardDefinition Find(int number) => CardDatabaseRepository.FindByPrintingKey("TST", number.ToString())
        ?? throw new InvalidOperationException("Missing boundary card TST/" + number);

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private sealed class Fixture : IDisposable
    {
        public readonly GameRules Rules;
        private readonly GameObject root = new GameObject("Boundary Card Validation");
        private readonly List<RuntimeCard> created = new List<RuntimeCard>();
        private readonly IDisposable scope;
        private readonly Action<RuntimeCard> playedHandler;

        public Fixture()
        {
            Rules = root.AddComponent<GameRules>();
            Set("player1Life", 20); Set("player2Life", 20);
            Set("matchOutcome", MatchOutcome.InProgress());
            Set("currentPhase", TurnPhase.Main1);
            Set("deterministicRandom", new DeterministicRandom(7241));
            Set("player1FullControl", true); Set("player2FullControl", true);
            ((PriorityState)Get("priorityState")).Begin(1);
            scope = CardScriptRuntimeManager.UseRules(Rules);
            CardScriptRuntimeManager.EnsureExists();
            playedHandler = (Action<RuntimeCard>)Delegate.CreateDelegate(typeof(Action<RuntimeCard>), Rules,
                typeof(GameRules).GetMethod("HandleCardPlayedLua", PrivateInstance));
            GameEvents.OnCardPlayed += playedHandler;
        }

        public RuntimeCard Card(int number, int owner = 1) => Add(Find(number), owner, GameZone.Hand);
        public RuntimeCard Creature(int owner) => Add(new CardDefinition
        { cardName = "Boundary target", cardTypes = CardType.Creature, attack = 2, defence = 3 }, owner, GameZone.Battlefield);

        private RuntimeCard Add(CardDefinition data, int owner, GameZone zone)
        {
            var card = new RuntimeCard(data, owner);
            card.ReassignObjectId(100000 + created.Count);
            card.SetCurrentZone(zone);
            created.Add(card);
            ((List<RuntimeCard>)Get("player" + owner + zone)).Add(card);
            return card;
        }

        public GameStackItem Stack(RuntimeCard card, string group, params SpellTargetReference[] targets)
            => StackWithX(card, 0, group, targets);

        public GameStackItem StackWithX(RuntimeCard card, int x, string group, params SpellTargetReference[] targets)
        {
            ((List<RuntimeCard>)Get("player" + card.OwnerPlayerID + "Hand")).Remove(card);
            card.SetCurrentZone(GameZone.Stack);
            card.SetSpellFeature(card.PrimaryFeature);
            var declaration = new SpellCastDeclaration(card, card.ControllerPlayerID, card.OwnerPlayerID,
                GameChoiceZone.Hand, 0, card.PrimaryFeature) { XValue = x };
            if (group != null) declaration.ReplaceTargets(group, targets);
            card.SetCurrentCastDeclaration(declaration);
            card.SetSpellTargets(targets);
            var item = GameStackItem.Spell(card, card.PrimaryFeature);
            Push(item);
            return item;
        }

        public void Push(GameStackItem item)
        {
            typeof(GameRules).GetMethod("PushStackItem", PrivateInstance).Invoke(Rules, new object[] { item });
            typeof(GameRules).GetMethod("NotifyStackChanged", PrivateInstance).Invoke(Rules, null);
        }

        public void ClearStack()
        {
            ((List<GameStackItem>)Get("stack")).Clear();
            ((List<RuntimeCard>)Get("stackCardView")).Clear();
        }

        public void CheckViews()
        {
            for (int seat = 1; seat <= 2; seat++)
            {
                var view = Rules.CreateOnlinePlayerView(seat);
                var restored = JsonUtility.FromJson<OnlinePlayerViewState>(JsonUtility.ToJson(view));
                var clientRoot = new GameObject("Boundary View Client");
                try
                {
                    var client = clientRoot.AddComponent<GameRules>();
                    Require(client.ApplyOnlinePlayerView(restored, out string error), "Boundary view failed: " + error);
                    Require(client.GetLife(2) == Rules.GetLife(2) && client.GetStackCount() == Rules.GetStackCount(),
                        "Boundary client view lost life or stack state.");
                }
                finally { UnityEngine.Object.DestroyImmediate(clientRoot); }
            }
        }

        private object Get(string name) => typeof(GameRules).GetField(name, PrivateInstance).GetValue(Rules);
        private void Set(string name, object value) => typeof(GameRules).GetField(name, PrivateInstance).SetValue(Rules, value);
        public void Dispose()
        {
            GameEvents.OnCardPlayed -= playedHandler;
            foreach (var card in created) card.OnDestroy();
            scope.Dispose();
            UnityEngine.Object.DestroyImmediate(root);
        }
    }
}
#endif
