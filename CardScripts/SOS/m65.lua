local Card = {}

function Card.OnPlay(self)
    local owner = self.Card.OwnerPlayerID
    Game:Log(self.Card.Data.cardName .. " draws 2 cards for player " .. owner)
    Game:DrawCards(owner, 2)
end

return Card
