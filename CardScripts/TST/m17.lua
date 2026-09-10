local Card = { ApiVersion = 2 }
function Card.OnInit(self)
    Game.RegisterReplacementEffect(self.Card, Game.Event.Damage, Game.Replacement.MultiplyAmount, "", 2,
        Game.Duration.WhileSourceRemains, "", "double-damage")
end
return Card
