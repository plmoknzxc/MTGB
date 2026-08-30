local Card = {}

function Card.OnInit(self)
    if Game and Game.RegisterFetchBasicLandAbility then
        Game:RegisterFetchBasicLandAbility(self.Card)
    elseif Game and Game.Log then
        Game:Log("RegisterFetchBasicLandAbility is not available in the current script runtime.")
    end
end

return Card
