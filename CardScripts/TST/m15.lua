local Card = { ApiVersion = 2 }
function Card.OnInit(self)
    Game.AddCastCost(self.Card, "blood", "支付2点生命", Game.CastCostKind.Additional, "Life:2", true)
    Game.AddCastTargetGroup(self.Card, "victim", "选择目标生物", Game.TargetMode.AnyCreature, 1, 1, "", true)
end
function Card.OnPlay(self)
    local target = Game.GetTargetCardInGroup(self.Card, "victim", 1)
    local amount = 1
    if Game.WasAdditionalCostPaid(self.Card, "blood") then amount = 3 end
    if target ~= nil then Game.DealDamageToCard(self.Card, target, amount) end
end
return Card
