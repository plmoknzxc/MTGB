local Card = { ApiVersion = 2 }
function Card.OnInit(self)
    Game.AddCastCost(self.Card, "offering", "牺牲一个永久物", Game.CastCostKind.Additional, "SacrificePermanent:1", false)
    Game.AddCastTargetGroup(self.Card, "victim", "选择目标生物", Game.TargetMode.AnyCreature, 1, 1, "", true)
end
function Card.OnPlay(self)
    local target = Game.GetTargetCardInGroup(self.Card, "victim", 1)
    if target ~= nil then Game.DealDamageToCard(self.Card, target, 3) end
end
return Card
