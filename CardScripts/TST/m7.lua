-- TST/7 杰斯，机巧心智法师：+1：抓一张牌；-3：对对手造成3点伤害
local Card = { ApiVersion = 1 }

function Card.OnInit(self)
    Game.RegisterActivatedAbility(
        self.Card,
        "+1 抓牌",
        "+1：抓一张牌。",
        "",
        "ResolveLoyaltyDraw",
        "CanLoyaltyDraw",
        Game.Timing.Sorcery,
        Game.AbilityTrait.LoyaltyAbility
    )
    Game.RegisterActivatedAbility(
        self.Card,
        "-3 伤害",
        "-3：对对手造成3点伤害。",
        "Counter:loyalty:3",
        "ResolveLoyaltyDamage",
        "CanLoyaltyDamage",
        Game.Timing.Sorcery,
        Game.AbilityTrait.LoyaltyAbility
    )
end

function Card.CanLoyaltyDraw(self)
    return Game.IsOnBattlefield(self.Card)
end

function Card.ResolveLoyaltyDraw(self)
    Game.DrawForOwner(self.Card, 1)
    Game.AddLoyaltyCounters(self.Card, 1)
end

function Card.CanLoyaltyDamage(self)
    return Game.IsOnBattlefield(self.Card)
        and Game.GetLoyaltyCounters(self.Card) >= 3
end

function Card.ResolveLoyaltyDamage(self)
    local owner = Game.GetOwner(self.Card)
    Game.DealDamageToPlayer(self.Card, Game.GetOpponent(owner), 3)
end

return Card
