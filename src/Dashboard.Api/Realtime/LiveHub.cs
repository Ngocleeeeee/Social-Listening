using Microsoft.AspNetCore.SignalR;

namespace Dashboard.Api.Realtime;

/// <summary>Pushes live "mention" and "alert" events to connected browsers.</summary>
public sealed class LiveHub : Hub;
