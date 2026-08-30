using System;

public enum OnlineClientInteractionState
{
    Disconnected = 0,
    Idle = 1,
    Submitting = 2,
    Resyncing = 3
}

public sealed class OnlineClientActionGate
{
    public OnlineClientInteractionState State { get; private set; } = OnlineClientInteractionState.Disconnected;
    public long PendingRequestId { get; private set; }
    public double Deadline { get; private set; }

    public bool CanSubmit => State == OnlineClientInteractionState.Idle;

    public void SetConnected(bool connected)
    {
        PendingRequestId = 0;
        Deadline = 0;
        State = connected
            ? OnlineClientInteractionState.Idle
            : OnlineClientInteractionState.Disconnected;
    }

    public bool TryBeginSubmit(long requestId, double now, double timeoutSeconds)
    {
        if (!CanSubmit || requestId <= 0) return false;

        PendingRequestId = requestId;
        Deadline = now + Math.Max(0.1d, timeoutSeconds);
        State = OnlineClientInteractionState.Submitting;
        return true;
    }

    public bool CompleteSubmit(long requestId)
    {
        if (State != OnlineClientInteractionState.Submitting || requestId != PendingRequestId) return false;

        PendingRequestId = 0;
        Deadline = 0;
        State = OnlineClientInteractionState.Idle;
        return true;
    }

    public bool BeginResync()
    {
        if (State == OnlineClientInteractionState.Disconnected) return false;

        bool changed = State != OnlineClientInteractionState.Resyncing;
        PendingRequestId = 0;
        Deadline = 0;
        State = OnlineClientInteractionState.Resyncing;
        return changed;
    }

    public bool CompleteResync()
    {
        if (State != OnlineClientInteractionState.Resyncing) return false;

        State = OnlineClientInteractionState.Idle;
        return true;
    }

    public bool Tick(double now)
    {
        if (State != OnlineClientInteractionState.Submitting || now < Deadline) return false;

        BeginResync();
        return true;
    }
}
