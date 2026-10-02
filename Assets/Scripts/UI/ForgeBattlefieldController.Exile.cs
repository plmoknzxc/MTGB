using System;
using System.Linq;

public sealed partial class ForgeBattlefieldController
{
    private ForgeCardView[] AvailableExileCards() => (view?.playableGraveyard??Array.Empty<ForgeCardView>()).Concat(view?.playableExile??Array.Empty<ForgeCardView>())
        .Concat((view?.players??Array.Empty<ForgePlayerView>()).Where(p=>p.seat==seat)
            .SelectMany(p=>p.battlefield??Array.Empty<ForgeCardView>()).Where(HasPreparedSpell).Select(c=>c.preparedSpell.ToCard()))
        .GroupBy(c=>c.id).Select(g=>g.First()).ToArray();

}
