local Card = {}

function Card.OnPlay(self)
    Game:DrawForOwner(self.Card, 1)
end

return Card
