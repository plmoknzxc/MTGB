using System;

public enum ForgeControlMode { On, Auto, Off }

/// <summary>Only proposes options already offered by the authoritative engine.</summary>
public static class ForgeControlPolicy
{
    public static int AutomaticOption(ForgeSeatView view, int seat, ForgeControlMode mode, bool paymentAttempted)
    {
        if(mode==ForgeControlMode.On || view?.awaitingInput!=true || view.request==null || view.request.seat!=seat)return -1;
        var request=view.request;
        // Casting automation never commits a payment. The explicit action
        // button starts native payment, in every priority-control mode.
        if(request.kind!="InputPassPriority")return -1;
        int ok=Array.FindIndex(request.options??Array.Empty<ForgeOption>(),o=>o!=null&&o.id=="ok"&&!o.disabled);
        if(ok<0)return -1;
        int playerSeat=request.playerSeat>0?request.playerSeat:seat;
        // Resolving an unanswerable stack is allowed; ending our empty main
        // phase always needs a deliberate click, including the skip mode.
        if(view.stackCount==0 && view.turnSeat==playerSeat && (view.phase=="MAIN1" || view.phase=="MAIN2"))return -1;
        if(mode==ForgeControlMode.Off)return ok;
        // Native canRespond excludes mana-only actions. A playable spell or
        // non-mana ability always keeps priority, even during draw/upkeep or
        // combat with no legal attackers. Phase shortcuts must not override it.
        return view.canRespond?-1:ok;
    }
}
