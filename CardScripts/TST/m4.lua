-- TST/4 预视：抓两张牌
local Card = { ApiVersion = 1 }

function Card.OnPlay(self)
    Game.DrawForOwner(self.Card, 2)
end

return Card
