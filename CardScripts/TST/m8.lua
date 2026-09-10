-- TST/8 卡德海姆之战：正面战役；背面「世界树薪火」{T}：加{U}
-- 战役被攻破后的放逐与背面施放由引擎内建流程处理，脚本只负责背面产费。
local Card = { ApiVersion = 1 }

function Card.OnInit(self)
    if Game.HasType(self.Card, Game.CardType.Land) then
        Game.RegisterManaAbility(self.Card, "{U}")
    end
end

return Card
