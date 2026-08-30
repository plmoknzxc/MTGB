local Card = {}

function Card.OnPlay(self)
    Game:Buff(self.Card, 1, 1, "MTGB/11 enter bonus")
end

return Card
