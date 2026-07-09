namespace FlowBoardVideoCalls.Configuration;

/// <summary>
/// Bound from the "WebRtc" appsettings.json section (Architecture Consistency
/// Conventions). Never hardcoded in a .cs or .js file -- passed to JS as a
/// parameter of the initializeCall() interop call (Epic 2).
/// </summary>
public sealed class WebRtcOptions
{
    public const string SectionName = "WebRtc";

    public IReadOnlyList<IceServerOptions> IceServers { get; set; } = Array.Empty<IceServerOptions>();
}

public sealed class IceServerOptions
{
    public required string Urls { get; set; }
}
