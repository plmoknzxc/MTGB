-- TST/13 可控放电：显式测试 X=0、可支付上限与声明锁定。
local Card = { ApiVersion = 2 }

function Card.OnInit(self)
    Game.ConfigureCastX(self.Card, 0, 10)
    Game.AddCastTargetGroup(self.Card, "player", "选择目标玩家",
        Game.TargetMode.AnyPlayer, 1, 1, "", true)
end

function Card.OnPlay(self)
    local player = Game.GetTargetPlayerInGroup(self.Card, "player", 1)
    if player > 0 then
        Game.DealDamageToPlayer(self.Card, player, Game.GetCastX(self.Card))
    end
end

return Card
