-- TST/1 罗堰妖精：{T}：加{G}
local Card = { ApiVersion = 1 }

function Card.OnInit(self)
    Game.RegisterManaAbility(self.Card, "{G}")
end

return Card
