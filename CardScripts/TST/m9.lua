-- TST/9 换体变形：放逐目标生物
local Card = { ApiVersion = 1 }

function Card.OnInit(self)
    Game.AddCastTargetGroup(
        self.Card,
        "target",
        "选择目标生物",
        Game.TargetMode.AnyCreature,
        1,
        1,
        "",
        false
    )
end

function Card.OnPlay(self)
    Game.ExileTarget(self.Card)
end

return Card
