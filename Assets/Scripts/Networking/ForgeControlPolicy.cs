using System;
using System.Linq;

public enum ForgeControlMode { On, Auto, Off }

/// <summary>Only proposes options already offered by the authoritative engine.</summary>
public static class ForgeControlPolicy
{
    public static int AutomaticOption(ForgeSeatView view, int seat, ForgeControlMode mode, bool paymentAttempted)
    {
        if(mode==ForgeControlMode.On || view?.awaitingInput!=true || view.request==null || view.request.seat!=seat)return -1;
        var request=view.request;
        int ok=Array.FindIndex(request.options??Array.Empty<ForgeOption>(),o=>o.id=="ok");
        if(ok<0)return -1;
        if(request.kind.Contains("PayMana"))return paymentAttempted?-1:ok;
        if(request.kind!="InputPassPriority")return -1;
        if(mode==ForgeControlMode.Off)return ok;
        if(view.stackCount>0) {
            // An unanswerable trigger is not a reason to ask for priority.
            // Preserve a response window when the engine offers a real response.
            return view.canRespond?-1:ok;
        }
        if(view.phase=="UNTAP" || view.phase=="UPKEEP" || view.phase=="DRAW")return ok;
        if(view.turnSeat==seat && (view.phase=="MAIN1" || view.phase=="MAIN2"))return -1;
        return view.canRespond?-1:ok;
    }
}
