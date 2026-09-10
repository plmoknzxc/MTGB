local Card = { ApiVersion = 2 }
function Card.OnInit(self)
    Game.RegisterContinuousEffect(self.Card, Game.Continuous.ModifyPowerToughness, "FriendlyCreature", "",
        Game.CardType.None, Game.Color.None, "", 1, 1, Game.Duration.WhileSourceRemains, "anthem", false,
        Game.ContinuousCharacteristic.Type + Game.ContinuousCharacteristic.Control,
        Game.ContinuousCharacteristic.PowerToughness)
end
function Card.FriendlyCreature(self)
    local card = Game.GetEventCard(self.Card)
    return card ~= nil and Game.HasType(card, Game.CardType.Creature)
        and Game.GetController(card) == Game.GetController(self.Card)
end
return Card
