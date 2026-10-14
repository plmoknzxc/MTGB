using System;
using System.Linq;

public sealed partial class ForgeBattlefieldController
{
    // Complete the authority's finished one-card choice, including an optional
    // choice after the player selects its one card. Empty and multi-card choices
    // retain their normal confirmation contract.
    private static int CompletedSingleCardOption(ForgeRequest request)
    {
        if(request==null||request.kind is not ("InputSelectCardsFromList" or "InputSelectEntitiesFromList")
            ||(request.minCount<0||request.minCount>1)||request.maxCount!=1||request.selectedCount!=1||request.remainingCount!=0)return -1;
        var options=request.options??Array.Empty<ForgeOption>();
        if(options.Count(o=>o.selected&&o.card?.id>0)!=1)return -1;
        return Array.FindIndex(options,o=>o.id=="ok"&&!o.disabled);
    }
}
