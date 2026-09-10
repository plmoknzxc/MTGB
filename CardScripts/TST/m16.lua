local Card = { ApiVersion = 2 }
function Card.OnInit(self)
    -- CardDied is the engine's precise battlefield-creature -> graveyard event.
    Game.RegisterReplacementEffect(self.Card, Game.Event.CardDied, Game.Replacement.ChangeDestination, "", Game.Zone.Exile,
        Game.Duration.WhileSourceRemains, "", "grave-exile")
end
return Card
