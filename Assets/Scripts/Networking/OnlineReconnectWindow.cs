using System;

public sealed class OnlineReconnectWindow
{
    public double GraceSeconds { get; }
    public double Deadline { get; private set; }
    public bool IsActive { get; private set; }

    public OnlineReconnectWindow(double graceSeconds)
    {
        if (graceSeconds <= 0d) throw new ArgumentOutOfRangeException(nameof(graceSeconds));
        GraceSeconds = graceSeconds;
    }

    public void Begin(double now)
    {
        Deadline = now + GraceSeconds;
        IsActive = true;
    }

    public bool CanResume(double now)
    {
        return IsActive && now < Deadline;
    }

    public bool IsExpired(double now)
    {
        return IsActive && now >= Deadline;
    }

    public double RemainingSeconds(double now)
    {
        return IsActive ? Math.Max(0d, Deadline - now) : 0d;
    }

    public bool TryComplete(double now)
    {
        if (!CanResume(now)) return false;
        Reset();
        return true;
    }

    public void Reset()
    {
        Deadline = 0d;
        IsActive = false;
    }
}
