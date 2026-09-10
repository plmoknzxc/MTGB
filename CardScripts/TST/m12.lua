-- TST/12 双生增幅：允许零目标；部分失效时仅处理剩余合法目标。
local Card = { ApiVersion = 2 }

function Card.OnInit(self)
    Game.AddCastTargetGroup(self.Card, "creatures", "选择至多两个不同的目标生物",
        Game.TargetMode.AnyCreature, 0, 2, "", true)
end

function Card.OnPlay(self)
    for index = 1, Game.GetTargetGroupCount(self.Card, "creatures") do
        local target = Game.GetTargetCardInGroup(self.Card, "creatures", index)
        if target ~= nil then
            Game.BuffUntilEndOfTurn(target, 1, 1, "双生增幅")
        end
    end
end

return Card
