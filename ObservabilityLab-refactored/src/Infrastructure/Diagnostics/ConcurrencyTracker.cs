using System.Collections.Concurrent;

namespace ObservabilityLab.Infrastructure.Diagnostics;

/// <summary>
/// Rastreia contenção de lock em seções críticas.
/// Uso: using var _ = ConcurrencyTracker.Enter("section-name");
/// </summary>
public static class ConcurrencyTracker
{
    private static readonly ConcurrentDictionary<string, ConcurrencyStats> Stats = new();

    public static IDisposable Enter(string section)
        => Stats.GetOrAdd(section, _ => new ConcurrencyStats(section)).Enter();

    public static ConcurrencyStats? GetStats(string section)
        => Stats.TryGetValue(section, out var s) ? s : null;

    public static IReadOnlyDictionary<string, ConcurrencyStats> GetAll() => Stats;
}

/// <summary>Estatísticas de concorrência por seção: atual, pico, total e contenções.</summary>
public sealed class ConcurrencyStats(string name)
{
    private long _current;
    private long _peak;
    private long _total;
    private long _contentionCount;

    public string Name      { get; } = name;
    public long   Current   => Interlocked.Read(ref _current);
    public long   Peak      => Interlocked.Read(ref _peak);
    public long   Total     => Interlocked.Read(ref _total);
    public long   Contention => Interlocked.Read(ref _contentionCount);

    public IDisposable Enter()
    {
        var curr = Interlocked.Increment(ref _current);
        Interlocked.Increment(ref _total);

        // Atualiza o pico via CAS (Compare-And-Swap)
        long snapshot;
        do
        {
            snapshot = Interlocked.Read(ref _peak);
            if (curr <= snapshot) break;
        } while (Interlocked.CompareExchange(ref _peak, curr, snapshot) != snapshot);

        if (curr > 1) Interlocked.Increment(ref _contentionCount);

        return new ConcurrencyScope(() => Interlocked.Decrement(ref _current));
    }

    private sealed class ConcurrencyScope(Action onDispose) : IDisposable
    {
        public void Dispose() => onDispose();
    }
}
