local Card = {}

function Card.OnPlay(self)
    -- self.Card 是 C# 的 RuntimeCard 实例
    local myPlayerID = self.Card.OwnerPlayerID
    
    -- 简单的目标选择逻辑：攻击对手
    local targetPlayerID = 1
    if myPlayerID == 1 then
        targetPlayerID = 2
    end
    
    Game.Log(self.Card.Data.cardName .. " 造成 4 点伤害给玩家 " .. targetPlayerID)
    Game.DealDamage(targetPlayerID, 4)
end

return Card
