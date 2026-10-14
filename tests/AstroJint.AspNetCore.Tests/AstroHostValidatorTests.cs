using Microsoft.Extensions.Primitives;

namespace AstroJint.AspNetCore.Tests;

public class AstroHostValidatorTests
{
    private static AstroAllowedDomain Domain(string? hostname = null, string? protocol = null, string? port = null) =>
        new() { Hostname = hostname, Protocol = protocol, Port = port };

    [Theory]
    [InlineData("", null)]
    [InlineData("  ", null)]
    [InlineData("https", "https")]
    [InlineData(" https ", "https")]
    [InlineData("https, http", "https")]
    [InlineData("https,http", "https")]
    [InlineData(", http", null)]
    public void GetFirstForwardedValue_ReadsTheFirstValueOfAProxyChain(string header, string? expected) =>
        Assert.Equal(expected, AstroHostValidator.GetFirstForwardedValue(header));

    [Fact]
    public void GetFirstForwardedValue_ReadsTheFirstOfSeveralHeaderLines()
    {
        Assert.Equal("a", AstroHostValidator.GetFirstForwardedValue(new StringValues(["a", "b"])));
        Assert.Null(AstroHostValidator.GetFirstForwardedValue(StringValues.Empty));
    }

    [Fact]
    public void ValidateHost_TrustsNothingWhenThereAreNoAllowedDomains() =>
        Assert.Null(AstroHostValidator.ValidateHost("example.com", "https", []));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void ValidateHost_RejectsAMissingHost(string? host) =>
        Assert.Null(AstroHostValidator.ValidateHost(host, "https", [Domain("example.com")]));

    [Fact]
    public void ValidateHost_ReturnsAMatchingHostAsItWasWritten()
    {
        Assert.Equal("example.com", AstroHostValidator.ValidateHost("example.com", "https", [Domain("example.com")]));
        Assert.Equal("EXAMPLE.com:8080", AstroHostValidator.ValidateHost("EXAMPLE.com:8080", "https", [Domain("example.com")]));
    }

    [Fact]
    public void ValidateHost_MatchesAnyOfTheAllowedDomains()
    {
        AstroAllowedDomain[] domains = [Domain("a.example"), Domain("b.example")];

        Assert.Equal("b.example", AstroHostValidator.ValidateHost("b.example", "http", domains));
        Assert.Null(AstroHostValidator.ValidateHost("c.example", "http", domains));
    }

    [Fact]
    public void ValidateHost_AnAllowedDomainWithoutAHostnameMatchesEveryHost() =>
        Assert.Equal("anything.test", AstroHostValidator.ValidateHost("anything.test", "https", [Domain(port: null, protocol: "https")]));

    [Theory]
    [InlineData("abc.xyz:444", true)]
    [InlineData("abc.xyz:8080", false)]
    [InlineData("abc.xyz", false)]
    public void ValidateHost_ComparesThePortWhenTheAllowedDomainHasOne(string host, bool allowed) =>
        Assert.Equal(allowed ? host : null, AstroHostValidator.ValidateHost(host, "https", [Domain("abc.xyz", port: "444")]));

    [Theory]
    [InlineData("localhost")]
    [InlineData("localhost:3000")]
    [InlineData("localhost:8080")]
    public void ValidateHost_AcceptsAnyPortWhenTheAllowedDomainHasNone(string host) =>
        Assert.Equal(host, AstroHostValidator.ValidateHost(host, "http", [Domain("localhost")]));

    [Fact]
    public void ValidateHost_ReadsTheDefaultPortOfTheProtocolAsNoPort()
    {
        Assert.Equal("example.com:443", AstroHostValidator.ValidateHost("example.com:443", "https", [Domain("example.com")]));
        Assert.Null(AstroHostValidator.ValidateHost("example.com:443", "https", [Domain("example.com", port: "443")]));
        Assert.Equal("example.com:443", AstroHostValidator.ValidateHost("example.com:443", "http", [Domain("example.com", port: "443")]));
    }

    [Theory]
    [InlineData("https", true)]
    [InlineData("http", false)]
    public void ValidateHost_ComparesTheProtocolWhenTheAllowedDomainHasOne(string protocol, bool allowed) =>
        Assert.Equal(allowed ? "example.com" : null, AstroHostValidator.ValidateHost("example.com", protocol, [Domain("example.com", protocol: "https")]));

    [Theory]
    [InlineData("a.example.com", true)]
    [InlineData("a.b.example.com", false)]
    [InlineData("example.com", false)]
    [InlineData("aexample.com", false)]
    [InlineData("a.example.org", false)]
    public void ValidateHost_AWildcardOfOneStarMatchesOneSubdomain(string host, bool allowed) =>
        Assert.Equal(allowed ? host : null, AstroHostValidator.ValidateHost(host, "https", [Domain("*.example.com")]));

    [Theory]
    [InlineData("a.example.com", true)]
    [InlineData("a.b.example.com", true)]
    [InlineData("example.com", false)]
    [InlineData("aexample.com", false)]
    [InlineData("a.example.org", false)]
    public void ValidateHost_AWildcardOfTwoStarsMatchesAnySubdomain(string host, bool allowed) =>
        Assert.Equal(allowed ? host : null, AstroHostValidator.ValidateHost(host, "https", [Domain("**.example.com")]));

    [Theory]
    [InlineData("*")]
    [InlineData("**")]
    [InlineData("*example.com")]
    [InlineData("a.*.example.com")]
    public void ValidateHost_AHostnameWithAMisplacedWildcardMatchesNothing(string pattern)
    {
        Assert.Null(AstroHostValidator.ValidateHost("a.example.com", "https", [Domain(pattern)]));
        Assert.Null(AstroHostValidator.ValidateHost("*", "https", [Domain(pattern)]));
    }

    [Theory]
    [InlineData("example.com/admin")]
    [InlineData("example.com\\admin")]
    [InlineData("example.com:3000/admin")]
    [InlineData("/example.com")]
    [InlineData("example.com:1:2")]
    [InlineData("[::1]")]
    [InlineData("[::1]:3000")]
    [InlineData("user@example.com")]
    [InlineData("example.com@evil.test")]
    [InlineData("example.com?x=1")]
    [InlineData("example.com#x")]
    [InlineData("exam ple.com")]
    [InlineData("example.com:99999")]
    [InlineData("example.com:abc")]
    public void ValidateHost_RejectsAHostThatIsNotANameWithAnOptionalPort(string host) =>
        Assert.Null(AstroHostValidator.ValidateHost(host, "https", [Domain(), Domain("example.com"), Domain("evil.test")]));

    [Theory]
    [InlineData("http")]
    [InlineData("https")]
    public void ValidateForwardedProtocol_AllowsHttpAndHttpsWhenNoAllowedDomainNamesAProtocol(string protocol) =>
        Assert.Equal(protocol, AstroHostValidator.ValidateForwardedProtocol(protocol, [Domain("example.com")]));

    [Theory]
    [InlineData("HTTPS")]
    [InlineData("ftp")]
    [InlineData("javascript")]
    [InlineData("ht tp")]
    [InlineData("")]
    [InlineData(null)]
    public void ValidateForwardedProtocol_RejectsOtherProtocolsWhenNoAllowedDomainNamesAProtocol(string? protocol) =>
        Assert.Null(AstroHostValidator.ValidateForwardedProtocol(protocol, [Domain("example.com")]));

    [Fact]
    public void ValidateForwardedProtocol_TrustsNothingWhenThereAreNoAllowedDomains() =>
        Assert.Null(AstroHostValidator.ValidateForwardedProtocol("https", []));

    [Theory]
    [InlineData("https", "https")]
    [InlineData("HTTPS", "HTTPS")]
    [InlineData("http", null)]
    [InlineData("ftp", null)]
    public void ValidateForwardedProtocol_AllowsTheProtocolsThatAnAllowedDomainNames(string protocol, string? expected) =>
        Assert.Equal(expected, AstroHostValidator.ValidateForwardedProtocol(protocol, [Domain("example.com", protocol: "https")]));

    [Fact]
    public void ValidateForwardedProtocol_AnAllowedDomainWithoutAProtocolAllowsEveryProtocol() =>
        Assert.Equal("http", AstroHostValidator.ValidateForwardedProtocol("http", [Domain("a.example", protocol: "https"), Domain("b.example")]));
}
