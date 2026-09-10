local Card = { ApiVersion = 2 }
function Card.OnInit(self)
    Game.AddCastTargetGroup(self.Card, "subject", "选择目标生物", Game.TargetMode.AnyCreature, 1, 1, "", true)
end
function Card.OnPlay(self)
    local target = Game.GetTargetCardInGroup(self.Card, "subject", 1)
    if target ~= nil then Game.SetPowerToughnessUntilEndOfTurn(target, 2, 3, "基准重塑") end
end
return Card
