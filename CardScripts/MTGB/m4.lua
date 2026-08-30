local Card = {}

function Card.OnInit(self)
    Game:RegisterManaAbility(self.Card, "{B}")
end

return Card
