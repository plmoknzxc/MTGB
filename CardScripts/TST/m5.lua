-- TST/5 太阳戒：{T}：加{C}{C}
local Card = { ApiVersion = 1 }

function Card.OnInit(self)
    Game.RegisterManaAbility(self.Card, "{C}{C}")
end

return Card
