local Card = {}

function Card.OnInit(self)
    Game:RegisterManaAbility(self.Card, "{R}")
end

return Card
