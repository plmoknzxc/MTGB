local Card = { ApiVersion = 2 }
function Card.OnPlay(self)
    if Game.GetFeatureRole(self.Card) == Game.FeatureRole.Adventure then
        -- The selected Adventure feature owns its single creature target.
        -- Do not register a shared target group on the primary creature feature.
        local target = Game.GetTargetCard(self.Card)
        if target ~= nil then Game.ReturnToHand(target) end
    end
end
return Card
