using Dashboard.Api.Search;

namespace Dashboard.Api.ReadModel;

/// <summary>Holds the latest precomputed dashboard snapshot (read-model) in memory.</summary>
public interface ISnapshotStore
{
    DashboardSnapshot? Current { get; set; }
}

public sealed class SnapshotStore : ISnapshotStore
{
    private volatile DashboardSnapshot? _current;
    public DashboardSnapshot? Current { get => _current; set => _current = value; }
}
