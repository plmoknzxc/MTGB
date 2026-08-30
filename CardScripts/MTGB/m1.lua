local Card = {}

function Card.OnInit(self)
    Game:RegisterManaAbility(self.Card, "{U}")
end

return Card
