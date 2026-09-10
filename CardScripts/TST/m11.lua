-- TST/11 堆叠截断：只选择咒语，不选择启动式或触发式异能。
local Card = { ApiVersion = 2 }

function Card.OnInit(self)
    Game.AddCastTargetGroupAdvanced(self.Card, "spell", "选择要反击的咒语",
        Game.TargetKind.Spell, Game.TargetZone.Stack, Game.TargetController.Any,
        Game.CardType.None, Game.Color.None, 1, 1, "", true,
        Game.TargetRelation.None, "")
end

function Card.OnPlay(self)
    local target = Game.GetTargetCardInGroup(self.Card, "spell", 1)
    if target ~= nil then
        Game.CounterSpell(target)
    end
end

return Card
