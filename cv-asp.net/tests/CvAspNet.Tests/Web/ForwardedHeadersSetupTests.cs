using System.Net;
using CvAspNet.Web;
using Microsoft.AspNetCore.Builder;

namespace CvAspNet.Tests.Web;

public class ForwardedHeadersSetupTests
{
    [Fact]
    public void ParseKnownProxies_ReadsACommaSeparatedList()
    {
        Assert.Equal([IPAddress.Parse("192.168.1.7"), IPAddress.Parse("::1")], ForwardedHeadersSetup.ParseKnownProxies(" 192.168.1.7, ::1 "));
        Assert.Empty(ForwardedHeadersSetup.ParseKnownProxies(null));
        Assert.Empty(ForwardedHeadersSetup.ParseKnownProxies(""));
    }

    [Fact]
    public void ParseKnownProxies_RejectsAName()
    {
        Assert.Throws<FormatException>(() => ForwardedHeadersSetup.ParseKnownProxies("gateway.local"));
    }

    [Fact]
    public void Configure_TrustsOnlyTheListedProxies()
    {
        var options = new ForwardedHeadersOptions();

        ForwardedHeadersSetup.Configure(options, [IPAddress.Parse("192.168.1.7")]);

        Assert.Equal([IPAddress.Parse("192.168.1.7")], options.KnownProxies);
        Assert.Empty(options.KnownIPNetworks);
    }
}
