using FlowBoardVideoCalls.Configuration;
using Microsoft.Extensions.Configuration;

namespace FlowBoard.Tests;

/// <summary>
/// WebRtcOptions tests (Architecture Consistency Conventions: STUN/ICE
/// server config lives in appsettings.json under "WebRtc", bound via
/// IOptions&lt;WebRtcOptions&gt;, never hardcoded in a .cs or .js file).
/// </summary>
public class WebRtcOptionsTests
{
    [Fact]
    public void Bind_WebRtcSection_PopulatesIceServersFromConfiguration()
    {
        var configData = new Dictionary<string, string?>
        {
            ["WebRtc:IceServers:0:Urls"] = "stun:stun.l.google.com:19302",
        };
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        var options = configuration.GetSection(WebRtcOptions.SectionName).Get<WebRtcOptions>();

        Assert.NotNull(options);
        var iceServer = Assert.Single(options!.IceServers);
        Assert.Equal("stun:stun.l.google.com:19302", iceServer.Urls);
    }

    [Fact]
    public void Bind_MultipleIceServers_PreservesOrderAndAllEntries()
    {
        var configData = new Dictionary<string, string?>
        {
            ["WebRtc:IceServers:0:Urls"] = "stun:stun.l.google.com:19302",
            ["WebRtc:IceServers:1:Urls"] = "stun:stun2.example.com:19302",
        };
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        var options = configuration.GetSection(WebRtcOptions.SectionName).Get<WebRtcOptions>();

        Assert.NotNull(options);
        Assert.Equal(2, options!.IceServers.Count);
        Assert.Equal("stun:stun.l.google.com:19302", options.IceServers[0].Urls);
        Assert.Equal("stun:stun2.example.com:19302", options.IceServers[1].Urls);
    }

    [Fact]
    public void Bind_MissingWebRtcSection_DefaultsToEmptyIceServersList()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();

        var options = new WebRtcOptions();
        configuration.GetSection(WebRtcOptions.SectionName).Bind(options);

        Assert.Empty(options.IceServers);
    }

    [Fact]
    public void SectionName_IsWebRtc()
    {
        // Guards the exact appsettings.json section name the Consistency
        // Conventions table names -- a silent rename here would desync
        // Program.cs's Configure<WebRtcOptions> from appsettings.json.
        Assert.Equal("WebRtc", WebRtcOptions.SectionName);
    }
}
