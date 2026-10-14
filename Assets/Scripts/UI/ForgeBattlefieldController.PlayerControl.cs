using System;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class ForgeBattlefieldController
{
    private int ActionPlayerSeat=>view?.awaitingInput==true&&view.request.seat==seat&&view.request.playerSeat is 1 or 2?view.request.playerSeat:seat;
    private int DisplayedHandSeat=>ActionPlayerSeat;
}
