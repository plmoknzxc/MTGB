local Card = { ApiVersion = 2 }
function Card.OnChapter1(self) Game.GainLife(Game.GetController(self.Card), 2) end
function Card.OnChapter2(self) Game.DrawCards(Game.GetController(self.Card), 1) end
function Card.OnChapter3(self) Game.GainLife(Game.GetController(self.Card), 3) end
return Card
