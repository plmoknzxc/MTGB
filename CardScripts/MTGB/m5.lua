local Card = {}

function Card.OnInit(self)
    Game:RegisterManaAbility(self.Card, "{W}")
end

return Card
