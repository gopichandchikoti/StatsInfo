namespace StatsInfo.Services;

public sealed class CounterDelta
{
    private (string Id, long Received, long Sent, double Seconds)? _last;
    public (double DownMbps, double UpMbps, long Received, long Sent)? Update(
        string id, long received, long sent, double seconds)
    {
        var previous = _last;
        _last = (id, received, sent, seconds);
        if (previous is not { } p || p.Id != id || seconds <= p.Seconds || seconds - p.Seconds > 30 ||
            received < p.Received || sent < p.Sent) return null;
        long down = received - p.Received, up = sent - p.Sent;
        double elapsed = seconds - p.Seconds;
        return (down * 8.0 / 1000000 / elapsed, up * 8.0 / 1000000 / elapsed, down, up);
    }
    public void Reset() => _last = null;
}
