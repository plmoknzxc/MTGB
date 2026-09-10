-- TST/3 变巨：目标生物获得+3/+3直到回合结束
local Card = { ApiVersion = 1 }

function Card.OnInit(self)
    Game.AddCastTargetGroup(
        self.Card,
        "target",
        "选择目标生物",
        Game.TargetMode.FriendlyCreature,
        1,
        1,
        "",
        false
    )
end

function Card.OnPlay(self)
    local target = Game.GetTargetCardInGroup(self.Card, "target", 1)
    if target ~= nil then
        Game.BuffUntilEndOfTurn(target, 3, 3, "变巨")
    end
end

return Card
