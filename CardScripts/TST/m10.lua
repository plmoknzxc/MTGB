-- TST/10 回溯脉冲：区域变化后，旧目标身份必须失效。
local Card = { ApiVersion = 2 }

function Card.OnInit(self)
    Game.AddCastTargetGroup(self.Card, "creature", "选择要移回手上的生物",
        Game.TargetMode.AnyCreature, 1, 1, "", true)
end

function Card.OnPlay(self)
    local target = Game.GetTargetCardInGroup(self.Card, "creature", 1)
    if target ~= nil then
        Game.ReturnToHand(target)
    end
end

return Card
