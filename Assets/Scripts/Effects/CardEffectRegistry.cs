using System.Collections.Generic;
using UnityEngine;

public static class CardEffectRegistry
{
    private static readonly Dictionary<string, ICardEffect> effects = new Dictionary<string, ICardEffect>
    {
        { "Deal4ToOpponent", new DealDamageToOpponentEffect(4) },
        { "Draw1", new DrawCardsEffect(1) },
        { "SelfPlus1Plus1", new SelfBuffEffect(1, 1) }
    };

    public static bool TryGet(string effectKey, out ICardEffect effect)
    {
        effect = null;
        if (string.IsNullOrEmpty(effectKey)) return false;
        return effects.TryGetValue(effectKey, out effect);
    }

    private sealed class DealDamageToOpponentEffect : ICardEffect
    {
        private readonly int damage;

        public DealDamageToOpponentEffect(int damage)
        {
            this.damage = damage;
        }

        public void OnPlay(GameRules rules, RuntimeCard card)
        {
            int target = card.OwnerPlayerID == 1 ? 2 : 1;
            rules.DealDamageToPlayer(target, damage);
        }
    }

    private sealed class DrawCardsEffect : ICardEffect
    {
        private readonly int drawCount;

        public DrawCardsEffect(int drawCount)
        {
            this.drawCount = drawCount;
        }

        public void OnPlay(GameRules rules, RuntimeCard card)
        {
            rules.ForceDrawCard(card.OwnerPlayerID, drawCount);
        }
    }

    private sealed class SelfBuffEffect : ICardEffect
    {
        private readonly int addAttack;
        private readonly int addDefence;

        public SelfBuffEffect(int addAttack, int addDefence)
        {
            this.addAttack = addAttack;
            this.addDefence = addDefence;
        }

        public void OnPlay(GameRules rules, RuntimeCard card)
        {
            card.CurrentAttack += addAttack;
            card.CurrentDefence += addDefence;
        }
    }
}
