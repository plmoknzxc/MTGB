using System;
using System.Linq;

public sealed partial class ForgeBattlefieldController
{
    private ForgeCardView[] AvailableExileCards() => (view?.playableGraveyard??Array.Empty<ForgeCardView>()).Concat(view?.playableExile??Array.Empty<ForgeCardView>())
        .Concat((view?.playableCommand??Array.Empty<ForgeCardView>()).Where(c=>c.ownerSeat==ActionPlayerSeat))
        .Concat((view?.players??Array.Empty<ForgePlayerView>()).Where(p=>p.seat==ActionPlayerSeat)
            .SelectMany(p=>p.battlefield??Array.Empty<ForgeCardView>()).Where(HasPreparedSpell).Select(c=>c.preparedSpell.ToCard()))
        .GroupBy(c=>c.id).Select(g=>KnownExileCastingFace(g.First())).ToArray();

    private ForgeCardView KnownExileCastingFace(ForgeCardView card)
    {
        // The zone object remains face down. This recipient-only casting preview
        // uses its supplied known print, never the face-down creature's 2/2 stats.
        if(!card.faceDown || string.IsNullOrEmpty(card.knownFacePrintingKey))return card;
        catalog.TryGetValue(card.knownFacePrintingKey,out var definition);
        return new ForgeCardView {
            id=card.id,ownerSeat=card.ownerSeat,zone=card.zone,stateName="ExileCastingFace",
            name=card.knownFaceName,printingKey=card.knownFacePrintingKey,
            currentRules=card.knownFaceRules,currentType=definition?.GetTypeLine(),
            manaCost=definition?.manaCost,power=definition?.attack??0,toughness=definition?.defence??0,
            creature=definition?.HasType(CardType.Creature)??false,
            land=definition?.HasType(CardType.Land)??false,
            planeswalker=definition?.HasType(CardType.Planeswalker)??false,
            battle=definition?.HasType(CardType.Battle)??false
        };
    }

}
