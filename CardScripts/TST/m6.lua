-- TST/6 地狱梦魇：每当一位对手抓一张牌时，该玩家失去1点生命
local Card = { ApiVersion = 1 }

function Card.OnInit(self)
    Game.RegisterTriggeredAbility(
        self.Card,
        Game.Event.CardDrawn,
        "ResolveOpponentDrawn",
        "IsOpponentDraw",
        Game.Duration.WhileSourceRemains,
        false,
        "underworld-dreams"
    )
end

function Card.IsOpponentDraw(self)
    local owner = Game.GetOwner(self.Card)
    local player = Game.GetEventPlayer(self.Card)
    if player == nil or player <= 0 then return false end
    return player == Game.GetOpponent(owner)
end

function Card.ResolveOpponentDrawn(self)
    Game.LoseLife(Game.GetEventPlayer(self.Card), 1)
end

return Card
