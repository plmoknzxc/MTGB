local Card = {}

function Card.OnInit(self)
    Game:RegisterManaAbility(self.Card, "{G}")
end

return Card
