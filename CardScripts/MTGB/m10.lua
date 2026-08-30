local Card = {}

function Card.OnPlay(self)
    Game:Log(self.Card.Data.cardName .. " deals 4 damage to its target")
    Game:DealDamageToTarget(self.Card, 4)
end

return Card
