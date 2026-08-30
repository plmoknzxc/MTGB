local Card = {}

function Card.OnInit(self)
    Game:RegisterManaAbility(self.Card, "{C}")
end

return Card
