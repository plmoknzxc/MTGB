using System.Collections.Generic;
using UnityEngine;

public class GameRules : MonoBehaviour
{
    public int startingLife = 20; 
    public int startingHandSize = 7; 
    public int maxHandSize = 7; 

    public List<CardData> player1Deck; 
    public List<CardData> player2Deck; 

    // Runtime State using RuntimeCard wrapper
    private List<RuntimeCard> player1Hand = new List<RuntimeCard>(); 
    private List<RuntimeCard> player2Hand = new List<RuntimeCard>(); 

    private List<RuntimeCard> player1Battlefield = new List<RuntimeCard>(); 
    private List<RuntimeCard> player2Battlefield = new List<RuntimeCard>(); 
    
    private List<RuntimeCard> player1Graveyard = new List<RuntimeCard>();
    private List<RuntimeCard> player2Graveyard = new List<RuntimeCard>();
    private List<RuntimeCard> stack = new List<RuntimeCard>();
    private readonly Dictionary<RuntimeCard, ManaColor> unspentLandMana = new Dictionary<RuntimeCard, ManaColor>();

    private int player1Life;
    private int player2Life;

    // Mana System
    private ManaPool player1ManaPool;
    private ManaPool player2ManaPool;
    private int landsPlayedThisTurn;
    private const int maxLandsPerTurn = 1;

    private int currentPlayer = 1; 
    private TurnPhase currentPhase = TurnPhase.Untap; 
    private int turnNumber = 1;
    private readonly List<RuntimeCard> emptyCards = new List<RuntimeCard>(0);
    private bool initialized;

    void Start()
    {
        InitializeGame();
    }

    private void InitializeGame()
    {
        initialized = true;
        GameEvents.OnCardPlayed -= HandleCardPlayedLua;
        GameEvents.OnCreatureDied -= HandleCreatureDiedLua;

        player1Life = startingLife;
        player2Life = startingLife;
        player1ManaPool.Clear();
        player2ManaPool.Clear();
        player1Hand.Clear();
        player2Hand.Clear();
        player1Battlefield.Clear();
        player2Battlefield.Clear();
        player1Graveyard.Clear();
        player2Graveyard.Clear();
        stack.Clear();
        unspentLandMana.Clear();
        currentPlayer = 1;
        currentPhase = TurnPhase.Untap;
        turnNumber = 1;
        landsPlayedThisTurn = 0;

        EnsurePlayableSampleDecks();

        // Register Lua listeners for game phases
        GameEvents.OnCardPlayed += HandleCardPlayedLua;
        GameEvents.OnCreatureDied += HandleCreatureDiedLua;

        ShuffleDeck(player1Deck);
        ShuffleDeck(player2Deck);

        DrawCards(player1Deck, player1Hand, startingHandSize, 1);
        DrawCards(player2Deck, player2Hand, startingHandSize, 2);
        SeedOpponentStartingBattlefield();

        SetCurrentPhase(TurnPhase.Main1);
        MainPhase();

        Debug.Log($"Game Initialized! P1 hand: {player1Hand.Count}, P2 hand: {player2Hand.Count}, current phase: P{currentPlayer} {currentPhase}.");
    }

    private void EnsurePlayableSampleDecks()
    {
        if (player1Deck == null) player1Deck = new List<CardData>();
        if (player2Deck == null) player2Deck = new List<CardData>();
        if (player1Deck.Count > 0 && player2Deck.Count > 0) return;

        CardData land = CardDatabaseRepository.FindById(1001)
            ?? CreateRuntimeCardData("Training Island", 1001, CardType.Land, "Land\n{T}: Add {U} to your mana pool.", 0, 0, 0, string.Empty, string.Empty);
        CardData bear = CardDatabaseRepository.FindById(1002)
            ?? CreateRuntimeCardData("Arena Bear", 1002, CardType.Creature, "Creature\nA simple 2/2 creature for combat testing.", 2, 2, 2, string.Empty, string.Empty);
        CardData lightning = CardDatabaseRepository.FindById(1003)
            ?? CreateRuntimeCardData("Whitebox Lightning", 1003, CardType.Sorcery, "Sorcery\nDeal 4 damage to the opponent.", 1, 0, 0, string.Empty, "Deal4ToOpponent");
        CardData insight = CardDatabaseRepository.FindById(1004)
            ?? CreateRuntimeCardData("Arena Insight", 1004, CardType.Sorcery, "Sorcery\nDraw 1 card.", 1, 0, 0, string.Empty, "Draw1");
        CardData adept = CardDatabaseRepository.FindById(1005)
            ?? CreateRuntimeCardData("Rune Adept", 1005, CardType.Creature, "Creature\nWhen enters battlefield, gets +1/+1.", 2, 1, 1, string.Empty, "SelfPlus1Plus1");

        if (player1Deck.Count == 0)
        {
            AddSampleDeck(player1Deck, land, bear, lightning, insight, adept);
        }

        if (player2Deck.Count == 0)
        {
            AddSampleDeck(player2Deck, land, bear, lightning, insight, adept);
        }
    }

    private static CardData CreateRuntimeCardData(string cardName, int cardID, CardType cardTypes, string description, int cost, int attack, int defence, string luaScript, string effectKey)
    {
        CardData card = ScriptableObject.CreateInstance<CardData>();
        card.name = cardName;
        card.cardName = cardName;
        card.cardID = cardID;
        card.setCode = "MTGB";
        card.collectorNumber = cardID >= 2000 ? cardID - 1000 : cardID;
        card.collectorNumberText = card.collectorNumber.ToString();
        card.cardTypes = cardTypes;
        card.description = description;
        card.manaCost = cost > 0 ? $"{{{cost}}}" : string.Empty;
        card.attack = attack;
        card.defence = defence;
        card.luaScript = luaScript;
        card.effectKey = effectKey;
        return card;
    }

    private static void AddSampleDeck(List<CardData> deck, CardData land, CardData bear, CardData lightning, CardData insight, CardData adept)
    {
        deck.Add(land);
        deck.Add(land);
        deck.Add(land);
        deck.Add(bear);
        deck.Add(lightning);
        deck.Add(land);
        deck.Add(insight);
        deck.Add(adept);
        deck.Add(land);
        deck.Add(bear);
    }

    private void SeedOpponentStartingBattlefield()
    {
        CardData land = CardDatabaseRepository.FindById(1001)
            ?? CreateRuntimeCardData("Training Island", 2001, CardType.Land, "Land\n{T}: Add {U} to your mana pool.", 0, 0, 0, string.Empty, string.Empty);
        CardData bear = CardDatabaseRepository.FindById(1002)
            ?? CreateRuntimeCardData("Arena Bear", 2002, CardType.Creature, "Creature\nA simple 2/2 creature for combat testing.", 2, 2, 2, string.Empty, string.Empty);

        RuntimeCard startingLand = new RuntimeCard(land, 2);
        RuntimeCard startingCreature = new RuntimeCard(bear, 2);
        GrantLandAbilities(startingLand);
        player2Battlefield.Add(startingLand);
        player2Battlefield.Add(startingCreature);
    }

    private void HandleCardPlayedLua(RuntimeCard card)
    {
        if (CardEffectRegistry.TryGet(card.Data.effectKey, out ICardEffect effect))
        {
            effect.OnPlay(this, card);
        }

        // Trigger Lua OnPlay only when card still provides lua script.
        card.EnsureScriptInstance();
        if (card.ScriptInstance != null)
        {
            var onPlay = card.ScriptInstance.Get<System.Action<XLua.LuaTable>>("OnPlay");
            if (onPlay != null)
            {
                onPlay(card.ScriptInstance);
            }
        }
        
        NotifyBattlefield("OnOtherCardPlayed", card);
    }
    
    private void HandleCreatureDiedLua(RuntimeCard card)
    {
        card.EnsureScriptInstance();
        if (card.ScriptInstance != null)
        {
            var onDie = card.ScriptInstance.Get<System.Action<XLua.LuaTable>>("OnDie");
            if (onDie != null)
            {
                onDie(card.ScriptInstance);
            }
        }
        NotifyBattlefield("OnOtherCreatureDied", card);
    }

    private void NotifyBattlefield(string functionName, params object[] args)
    {
        // Notifying all cards is expensive, so we should optimize later.
        // For XLua, calling generic functions with variable args needs care.
        // We will simplify for now by not passing complex args or using specific delegates.
        
        foreach (var c in player1Battlefield) 
        {
             c.EnsureScriptInstance();
             if (c.ScriptInstance != null)
             {
                 var func = c.ScriptInstance.Get<System.Action<XLua.LuaTable>>(functionName);
                 if (func != null) func(c.ScriptInstance);
             }
        }
                
        foreach (var c in player2Battlefield)
        {
             c.EnsureScriptInstance();
             if (c.ScriptInstance != null)
             {
                 var func = c.ScriptInstance.Get<System.Action<XLua.LuaTable>>(functionName);
                 if (func != null) func(c.ScriptInstance);
             }
        }
    }

    private void ShuffleDeck(List<CardData> deck)
    {
        for (int i = 0; i < deck.Count; i++)
        {
            CardData temp = deck[i];
            int randomIndex = Random.Range(0, deck.Count);
            deck[i] = deck[randomIndex];
            deck[randomIndex] = temp;
        }
    }

    private void DrawCards(List<CardData> deck, List<RuntimeCard> hand, int count, int ownerID)
    {
        for (int i = 0; i < count; i++)
        {
            if (deck.Count > 0)
            {
                // Create Runtime Instance here
                RuntimeCard newCard = new RuntimeCard(deck[0], ownerID);
                hand.Add(newCard);
                deck.RemoveAt(0);
            }
            else
            {
                Debug.LogWarning("Deck is empty!");
                break;
            }
        }
    }

    public void NextPhase()
    {
        ProceedToNextPhase();
    }

    public void EndTurn()
    {
        currentPhase = TurnPhase.End;
        ProceedToNextPhase();
    }

    public int GetCurrentPlayer()
    {
        return currentPlayer;
    }

    public bool IsInitialized()
    {
        return initialized;
    }

    public TurnPhase GetCurrentPhase()
    {
        return currentPhase;
    }

    public int GetTurnNumber()
    {
        return turnNumber;
    }

    public int GetDeckCount(int playerID)
    {
        if (playerID == 1) return player1Deck.Count;
        if (playerID == 2) return player2Deck.Count;
        return 0;
    }

    public int GetHandCount(int playerID)
    {
        if (playerID == 1) return player1Hand.Count;
        if (playerID == 2) return player2Hand.Count;
        return 0;
    }

    public int GetLife(int playerID)
    {
        if (playerID == 1) return player1Life;
        if (playerID == 2) return player2Life;
        return 0;
    }

    public int GetStackCount()
    {
        return stack.Count;
    }

    public RuntimeCard GetTopStackCard()
    {
        return stack.Count > 0 ? stack[stack.Count - 1] : null;
    }

    public bool HasPendingStack()
    {
        return stack.Count > 0;
    }

    public bool CanPlayCard(int player, int handIndex)
    {
        if (player != currentPlayer) return false;
        if (HasPendingStack()) return false;
        if (currentPhase != TurnPhase.Main1 && currentPhase != TurnPhase.Main2) return false;

        List<RuntimeCard> hand = player == 1 ? player1Hand : player2Hand;
        if (handIndex < 0 || handIndex >= hand.Count) return false;

        RuntimeCard card = hand[handIndex];
        if (card.Data.HasType(CardType.Land)) return landsPlayedThisTurn < maxLandsPerTurn;
        return GetManaPool(player).CanPay(card.Data.GetManaCost());
    }

    public RuntimeCard GetHandCard(int player, int handIndex)
    {
        List<RuntimeCard> hand = player == 1 ? player1Hand : player2Hand;
        if (handIndex < 0 || handIndex >= hand.Count) return null;
        return hand[handIndex];
    }

    public IReadOnlyList<RuntimeCard> GetHandCards(int player)
    {
        if (player == 1) return player1Hand;
        if (player == 2) return player2Hand;
        return emptyCards;
    }

    public IReadOnlyList<RuntimeCard> GetBattlefieldCards(int player)
    {
        if (player == 1) return player1Battlefield;
        if (player == 2) return player2Battlefield;
        return emptyCards;
    }

    public RuntimeCard GetBattlefieldCard(int player, int battlefieldIndex)
    {
        List<RuntimeCard> battlefield = player == 1 ? player1Battlefield : player2Battlefield;
        if (battlefieldIndex < 0 || battlefieldIndex >= battlefield.Count) return null;
        return battlefield[battlefieldIndex];
    }

    public bool CanDeclareAttacker(int player, int battlefieldIndex)
    {
        if (player != currentPlayer || currentPhase != TurnPhase.Combat || HasPendingStack()) return false;

        RuntimeCard card = GetBattlefieldCard(player, battlefieldIndex);
        return card != null && card.Data.HasType(CardType.Creature) && !card.IsTapped && !card.IsAttacking;
    }

    public bool ResolveTopOfStack()
    {
        if (stack.Count == 0) return false;

        RuntimeCard card = stack[stack.Count - 1];
        stack.RemoveAt(stack.Count - 1);

        List<RuntimeCard> battlefield = card.OwnerPlayerID == 1 ? player1Battlefield : player2Battlefield;
        List<RuntimeCard> graveyard = card.OwnerPlayerID == 1 ? player1Graveyard : player2Graveyard;

        if (card.Data.HasType(CardType.Land))
        {
            Debug.Log($"Resolving land: {card.Data.cardName}");
            battlefield.Add(card);
            GrantLandAbilities(card);
        }
        else if (card.Data.cardTypes.IsInstantOrSorcery())
        {
            Debug.Log($"Resolving spell: {card.Data.cardName}");
            GameEvents.TriggerCardPlayed(card);
            graveyard.Add(card);
        }
        else
        {
            Debug.Log($"Resolving permanent: {card.Data.cardName}");
            battlefield.Add(card);
            GrantLandAbilities(card);
            GameEvents.TriggerCardPlayed(card);
        }

        GameEvents.TriggerStackChanged(stack.Count);
        return true;
    }

    private int CountUntappedLands(int playerID)
    {
        List<RuntimeCard> battlefield = playerID == 1 ? player1Battlefield : player2Battlefield;
        int count = 0;
        foreach (var card in battlefield)
        {
            if (card.Data.HasType(CardType.Land) && !card.IsTapped)
            {
                count++;
            }
        }
        return count;
    }

    public void AddMana(int playerID, ManaColor color, int amount)
    {
        if (playerID == 1) player1ManaPool.Add(color, amount);
        else player2ManaPool.Add(color, amount);
        var pool = playerID == 1 ? player1ManaPool : player2ManaPool;
        Debug.Log($"Player {playerID} adds {color} x{amount}. Pool: {pool.ToDisplayString()}");
    }

    public ManaPool GetManaPool(int playerID)
    {
        return playerID == 1 ? player1ManaPool : player2ManaPool;
    }

    public int GetMana(int playerID)
    {
        return (playerID == 1 ? player1ManaPool : player2ManaPool).Total;
    }

    public bool GetPotentialMana(ManaCost cost, int playerID)
    {
        // Potential: current pool + each untapped land can produce 1 of its color
        ManaPool virtualPool = GetManaPool(playerID);
        List<RuntimeCard> battlefield = playerID == 1 ? player1Battlefield : player2Battlefield;
        foreach (var card in battlefield)
        {
            if (card.Data.HasType(CardType.Land) && !card.IsTapped)
            {
                virtualPool.Add(card.Data.GetProducedManaColor(), 1);
            }
        }
        return virtualPool.CanPay(cost);
    }

    private bool SpendMana(int playerID, ManaCost cost)
    {
        if (cost.IsEmpty) return true;

        ManaPool before = GetManaPool(playerID);
        bool paid = playerID == 1 ? player1ManaPool.Spend(cost) : player2ManaPool.Spend(cost);
        if (!paid) return false;

        DeductTrackedLandMana(playerID, before, GetManaPool(playerID));
        return true;
    }

    private void ClearManaPools()
    {
        if (!player1ManaPool.IsEmpty || !player2ManaPool.IsEmpty || unspentLandMana.Count > 0)
        {
            Debug.Log("Mana pools empty as phase ends.");
            player1ManaPool.Clear();
            player2ManaPool.Clear();
            unspentLandMana.Clear();
        }
    }

    private void DeductTrackedLandMana(int playerID, ManaPool before, ManaPool after)
    {
        RemoveSpentTrackedMana(playerID, ManaColor.White, before.White - after.White);
        RemoveSpentTrackedMana(playerID, ManaColor.Blue, before.Blue - after.Blue);
        RemoveSpentTrackedMana(playerID, ManaColor.Black, before.Black - after.Black);
        RemoveSpentTrackedMana(playerID, ManaColor.Red, before.Red - after.Red);
        RemoveSpentTrackedMana(playerID, ManaColor.Green, before.Green - after.Green);
        RemoveSpentTrackedMana(playerID, ManaColor.Colorless, before.Colorless - after.Colorless);
    }

    private void RemoveSpentTrackedMana(int playerID, ManaColor color, int spent)
    {
        if (spent <= 0 || unspentLandMana.Count == 0) return;

        List<RuntimeCard> consumed = new List<RuntimeCard>(spent);
        foreach (var entry in unspentLandMana)
        {
            if (spent <= 0) break;
            RuntimeCard source = entry.Key;
            if (source == null || source.OwnerPlayerID != playerID || entry.Value != color) continue;

            consumed.Add(source);
            spent--;
        }

        for (int i = 0; i < consumed.Count; i++)
        {
            unspentLandMana.Remove(consumed[i]);
        }
    }

    private static ActivatedAbility CreateLandManaAbility(RuntimeCard card)
    {
        ManaColor color = card.Data.GetProducedManaColor();
        string colorSymbol = color == ManaColor.Colorless ? "1" : color.ToString().Substring(0, 1);

        return new ActivatedAbility
        {
            Name = $"Tap for {color}",
            Description = $"{{T}}: Add {{{colorSymbol}}} to your mana pool.",
            CanActivate = (rules, c) => !c.IsTapped,
            Execute = (rules, c) =>
            {
                rules.TapLandForMana(c);
            }
        };
    }

    private void TapLandForMana(RuntimeCard land)
    {
        if (land == null || !land.Data.HasType(CardType.Land) || land.IsTapped) return;

        land.Tap();
        ManaColor color = land.Data.GetProducedManaColor();
        AddMana(land.OwnerPlayerID, color, 1);
        unspentLandMana[land] = color;
    }

    private bool CanUndoUnspentLandMana(RuntimeCard land)
    {
        if (land == null || !land.IsTapped || !land.Data.HasType(CardType.Land)) return false;
        if (!unspentLandMana.TryGetValue(land, out ManaColor color)) return false;
        return GetManaPool(land.OwnerPlayerID).GetAmount(color) > 0;
    }

    private bool UndoUnspentLandMana(RuntimeCard land)
    {
        if (!CanUndoUnspentLandMana(land)) return false;

        ManaColor color = unspentLandMana[land];
        bool removed = land.OwnerPlayerID == 1
            ? player1ManaPool.TryRemove(color, 1)
            : player2ManaPool.TryRemove(color, 1);
        if (!removed) return false;

        unspentLandMana.Remove(land);
        land.Untap();
        Debug.Log($"Player {land.OwnerPlayerID} rewinds {land.Data.cardName}. Removed {color} x1 from pool.");
        return true;
    }

    private void GrantLandAbilities(RuntimeCard card)
    {
        if (card.Data.HasType(CardType.Land))
        {
            card.Abilities.Add(CreateLandManaAbility(card));
        }
    }

    public bool CanActivateAnyAbility(RuntimeCard card)
    {
        return card != null && (card.HasActivatableAbility(this) || CanUndoUnspentLandMana(card));
    }

    public bool ActivateAbility(RuntimeCard card)
    {
        if (card == null) return false;
        if (UndoUnspentLandMana(card)) return true;

        var ability = card.GetFirstActivatableAbility(this);
        if (ability == null)
        {
            Debug.LogWarning("No activatable ability available.");
            return false;
        }

        ability.Activate(this, card);
        Debug.Log($"Activated ability: {ability.Name} on {card.Data.cardName}");
        return true;
    }

    private void SetCurrentPhase(TurnPhase phase)
    {
        ClearManaPools();
        currentPhase = phase;
        GameEvents.TriggerPhaseChanged(currentPhase);
    }

    private void ProceedToNextPhase()
    {
        if (HasPendingStack())
        {
            Debug.LogWarning("Resolve the stack before changing phase.");
            return;
        }

        switch (currentPhase)
        {
            case TurnPhase.Untap:
                UntapPhase();
                SetCurrentPhase(TurnPhase.Draw);
                DrawPhase();
                SetCurrentPhase(TurnPhase.Main1);
                MainPhase();
                break;
            case TurnPhase.Draw:
                DrawPhase();
                SetCurrentPhase(TurnPhase.Main1);
                MainPhase();
                break;
            case TurnPhase.Main1:
                SetCurrentPhase(TurnPhase.Combat);
                CombatPhase();
                break;
            case TurnPhase.Combat:
                ResolveCombat();
                ClearCombatStates();
                SetCurrentPhase(TurnPhase.Main2);
                MainPhase();
                break;
            case TurnPhase.Main2:
                SetCurrentPhase(TurnPhase.End);
                EndPhase();
                break;
            case TurnPhase.End:
                EndPhase();
                break;
        }
    }

    private void UntapPhase()
    {
        Debug.Log($"Player {currentPlayer} is in Untap Phase.");
        
        List<RuntimeCard> currentBattlefield = currentPlayer == 1 ? player1Battlefield : player2Battlefield;
        foreach (var card in currentBattlefield)
        {
            card.Untap();
        }
    }

    private void DrawPhase()
    {
        Debug.Log($"Player {currentPlayer} is in Draw Phase.");
        if (currentPlayer == 1)
        {
            DrawCards(player1Deck, player1Hand, 1, 1);
        }
        else
        {
            DrawCards(player2Deck, player2Hand, 1, 2);
        }
    }

    private void StartTurn()
    {
        landsPlayedThisTurn = 0;
        SetCurrentPhase(TurnPhase.Untap);
        Debug.Log($"Player {currentPlayer}'s turn starts!");
        ProceedToNextPhase();
    }

    private void MainPhase()
    {
        Debug.Log($"Player {currentPlayer} is in Main Phase.");
    }

    private void CombatPhase()
    {
        Debug.Log($"Player {currentPlayer} is in Combat Phase. Declare Attackers!");
        // Note: Real game would wait for UI input.
    }

    private void EndPhase()
    {
        Debug.Log($"Player {currentPlayer} is in End Phase.");
        
        // Remove all combat states at end of turn (or end of combat)
        // Ideally should be done at end of combat, but for now safe here
        ClearCombatStates();
        ClearDamageOnBattlefield();

        if (currentPlayer == 1)
        {
            if (player1Hand.Count > maxHandSize)
            {
                player1Hand.RemoveRange(maxHandSize, player1Hand.Count - maxHandSize);
                Debug.Log("Player 1 discarded excess cards.");
            }
        }
        else
        {
            if (player2Hand.Count > maxHandSize)
            {
                player2Hand.RemoveRange(maxHandSize, player2Hand.Count - maxHandSize);
                Debug.Log("Player 2 discarded excess cards.");
            }
        }

        currentPlayer = currentPlayer == 1 ? 2 : 1;
        turnNumber++;
        StartTurn();
    }

    private void ClearCombatStates()
    {
        foreach (var card in player1Battlefield) card.RemoveFromCombat();
        foreach (var card in player2Battlefield) card.RemoveFromCombat();
    }

    private void ClearDamageOnBattlefield()
    {
        foreach (var card in player1Battlefield) card.ClearDamage();
        foreach (var card in player2Battlefield) card.ClearDamage();
    }

    public bool PlayCard(int player, int handIndex)
    {
        if (player != currentPlayer)
        {
            Debug.LogWarning("Not your turn!");
            return false;
        }

        // Simplified Phase Check: Only allow playing in Main Phases
        if (currentPhase != TurnPhase.Main1 && currentPhase != TurnPhase.Main2)
        {
             // Instants/Flash could override this, but simplified for now
            Debug.LogWarning("Can only play cards in Main Phase!");
            return false;
        }

        if (HasPendingStack())
        {
            Debug.LogWarning("Resolve the stack before playing another card!");
            return false;
        }

        List<RuntimeCard> hand = player == 1 ? player1Hand : player2Hand;
        List<RuntimeCard> battlefield = player == 1 ? player1Battlefield : player2Battlefield;

        if (handIndex >= 0 && handIndex < hand.Count)
        {
            RuntimeCard card = hand[handIndex];
            
            // 1. Check Card Type & Costs
            if (card.Data.HasType(CardType.Land))
            {
                if (landsPlayedThisTurn >= maxLandsPerTurn)
                {
                    Debug.LogWarning("Already played a land this turn!");
                    return false;
                }
                landsPlayedThisTurn++;
            }
            else // Spells (Creature, Sorcery, etc.)
            {
                ManaCost manaCost = card.Data.GetManaCost();
                if (!TryPayMana(player, manaCost))
                {
                    if (GetPotentialMana(manaCost, player))
                        Debug.LogWarning($"Not enough mana in pool. Tap your lands first! (Need {manaCost.ToSymbolString()}, pool has {GetMana(player)})");
                    else
                        Debug.LogWarning($"Not enough mana to play {card.Data.cardName}. Cost: {manaCost.ToSymbolString()}");
                    return false;
                }
            }

            // 2. Move Card
            hand.RemoveAt(handIndex);
            if (card.Data.HasType(CardType.Land))
            {
                battlefield.Add(card);
                GrantLandAbilities(card);
                Debug.Log($"Player {player} played land: {card.Data.cardName}");
                GameEvents.TriggerCardPlayed(card);
                GameEvents.TriggerStackChanged(stack.Count);
                return true;
            }

            stack.Add(card);
            Debug.Log($"Player {player} puts {card.Data.cardName} on the stack.");
            GameEvents.TriggerStackChanged(stack.Count);
            return true;
        }

        return false;
    }

    private bool TryPayMana(int playerID, ManaCost cost)
    {
        if (cost.IsEmpty) return true;
        return SpendMana(playerID, cost);
    }

    // New Combat System: Step 1 - Declare Attacker
    public void DeclareAttacker(int attackerPlayerID, int attackerIndex)
    {
        if (currentPhase != TurnPhase.Combat)
        {
            Debug.LogWarning("Not in Combat Phase!");
            return;
        }

        if (HasPendingStack())
        {
            Debug.LogWarning("Resolve the stack before declaring attackers!");
            return;
        }

        if (attackerPlayerID != currentPlayer)
        {
            Debug.LogWarning("Not your turn!");
            return;
        }

        List<RuntimeCard> attackerBattlefield = attackerPlayerID == 1 ? player1Battlefield : player2Battlefield;
        
        if (attackerIndex >= 0 && attackerIndex < attackerBattlefield.Count)
        {
            RuntimeCard attacker = attackerBattlefield[attackerIndex];

            // Logic Check
            if (attacker.IsTapped)
            {
                Debug.LogWarning($"{attacker.Data.cardName} is tapped and cannot attack!");
                return;
            }
            if (attacker.IsAttacking)
            {
                Debug.LogWarning($"{attacker.Data.cardName} is already attacking!");
                return;
            }

            // Haste check: If just played this turn and no Haste? (Tracking 'just played' needs extra field)
            // Assuming simplified Haste for now or skipping 'summoning sickness' check if not implemented.

            // Tap Check (Vigilance)
            bool hasVigilance = attacker.HasKeyword("Vigilance");
            if (!hasVigilance)
            {
                attacker.Tap();
            }

            // Set State
            int defenderPlayerID = attackerPlayerID == 1 ? 2 : 1;
            attacker.DeclareAttack(defenderPlayerID); // Target is opponent player by default

            GameEvents.TriggerAttackDeclared(attacker, defenderPlayerID);
            Debug.Log($"{attacker.Data.cardName} attacks Player {defenderPlayerID}!");
        }
    }

    // New Combat System: Step 2 - Declare Blocker
    public void DeclareBlocker(int blockerPlayerID, int blockerIndex, RuntimeCard targetAttacker)
    {
        // Must be opponent's turn to block
        if (blockerPlayerID == currentPlayer)
        {
            Debug.LogWarning("Cannot block during your own turn!");
            return;
        }

        if (HasPendingStack())
        {
            Debug.LogWarning("Resolve the stack before declaring blockers!");
            return;
        }

        List<RuntimeCard> blockerBattlefield = blockerPlayerID == 1 ? player1Battlefield : player2Battlefield;

        if (blockerIndex >= 0 && blockerIndex < blockerBattlefield.Count)
        {
            RuntimeCard blocker = blockerBattlefield[blockerIndex];

            if (blocker.IsTapped)
            {
                Debug.LogWarning($"{blocker.Data.cardName} is tapped and cannot block!");
                return;
            }
            if (blocker.IsBlocking)
            {
                Debug.LogWarning($"{blocker.Data.cardName} is already blocking!");
                return;
            }

            // Evasion Checks (Flying/Reach)
            bool attackerHasFlying = targetAttacker.HasKeyword("Flying");
            bool blockerHasFlying = blocker.HasKeyword("Flying");
            bool blockerHasReach = blocker.HasKeyword("Reach");

            if (attackerHasFlying && !blockerHasFlying && !blockerHasReach)
            {
                Debug.LogWarning($"{blocker.Data.cardName} cannot block flying {targetAttacker.Data.cardName}!");
                return;
            }

            // Set State
            blocker.DeclareBlock(targetAttacker);
            GameEvents.TriggerBlockDeclared(blocker, targetAttacker);
            Debug.Log($"{blocker.Data.cardName} blocks {targetAttacker.Data.cardName}!");
        }
    }

    // New Combat System: Step 3 - Resolve Combat Damage
    public void ResolveCombat()
    {
        if (HasPendingStack())
        {
            Debug.LogWarning("Resolve the stack before combat damage!");
            return;
        }

        Debug.Log("Resolving Combat Damage...");
        
        // Iterate through all attackers of current player
        List<RuntimeCard> attackers = currentPlayer == 1 ? player1Battlefield : player2Battlefield;
        List<RuntimeCard> defenders = currentPlayer == 1 ? player2Battlefield : player1Battlefield;

        // Create a copy to modify safely if needed, though we just modify stats
        foreach (var attacker in attackers)
        {
            if (!attacker.IsAttacking) continue;

            if (attacker.Blockers.Count > 0)
            {
                // Blocked!
                // Simplified damage assignment: Attacker hits first blocker, Blocker hits attacker.
                // Full MTG allows ordering blockers.
                
                RuntimeCard firstBlocker = attacker.Blockers[0];
                
                Debug.Log($"Combat: {attacker.Data.cardName} vs {firstBlocker.Data.cardName}");

                // Deal Damage
                firstBlocker.TakeDamage(attacker.CurrentAttack);
                attacker.TakeDamage(firstBlocker.CurrentAttack);

                // Trample Check
                if (attacker.HasKeyword("Trample"))
                {
                    int excessDamage = attacker.CurrentAttack - firstBlocker.CurrentDefence; 
                    // Note: CurrentDefence might be < 0 now, so we need 'toughness before damage' ideally.
                    // But simplified: if blocker died (def <= 0), excess is attack - (original def).
                    // Or simplified: just remaining attack? No, trample is attack - blockerToughness.
                    // Since we already applied damage, we can't easily know original toughness unless we cached it.
                    // BETTER SIMPLIFIED TRAMPLE:
                    // If blocker dies, excess spills over?
                    // Let's do a proper calculation before TakeDamage:
                    
                    // (Re-logic for Trample to be correct)
                    // int damageToBlocker = Mathf.Min(attacker.CurrentAttack, firstBlocker.CurrentDefence);
                    // int damageToPlayer = attacker.CurrentAttack - damageToBlocker;
                    // BUT for this iteration, let's keep it simple: Trample not fully implemented in this pass.
                }
            }
            else
            {
                // Unblocked! Hit Player.
                if (attacker.AttackTarget is int targetPlayerID)
                {
                    DealDamageToPlayer(targetPlayerID, attacker.CurrentAttack);
                }
            }
        }

        // Cleanup dead creatures
        CleanUpDeadCreatures(player1Battlefield, player1Graveyard);
        CleanUpDeadCreatures(player2Battlefield, player2Graveyard);
        
        // End of Combat cleanup is done in EndPhase or manually called
    }

    private void CleanUpDeadCreatures(List<RuntimeCard> battlefield, List<RuntimeCard> graveyard)
    {
        for (int i = battlefield.Count - 1; i >= 0; i--)
        {
            var card = battlefield[i];
            if (!card.Data.HasType(CardType.Creature)) continue;

            if (card.IsDead || card.DamageTaken >= card.CurrentDefence)
            {
                if (!card.IsDead)
                {
                    Debug.Log($"{card.Data.cardName} dies!");
                    card.Die();
                }
                card.RemoveFromCombat();
                graveyard.Add(card);
                battlefield.RemoveAt(i);
            }
        }
    }

    public void DealDamageToPlayer(int player, int damage)
    {
        if (player == 1)
        {
            player1Life -= damage;
            Debug.Log($"Player 1 takes {damage} damage! Remaining life: {player1Life}");
            if (player1Life <= 0) Debug.Log("Player 2 wins!");
        }
        else if (player == 2)
        {
            player2Life -= damage;
            Debug.Log($"Player 2 takes {damage} damage! Remaining life: {player2Life}");
            if (player2Life <= 0) Debug.Log("Player 1 wins!");
        }
    }

    public void ForceDrawCard(int playerID, int count)
    {
        if (playerID == 1)
        {
            DrawCards(player1Deck, player1Hand, count, 1);
        }
        else if (playerID == 2)
        {
            DrawCards(player2Deck, player2Hand, count, 2);
        }
        Debug.Log($"Player {playerID} drew {count} card(s).");
    }

    private void OnDestroy()
    {
        GameEvents.OnCardPlayed -= HandleCardPlayedLua;
        GameEvents.OnCreatureDied -= HandleCreatureDiedLua;
    }
}

public enum TurnPhase
{
    Untap,
    Draw,
    Main1,
    Combat,
    Main2,
    End
}
