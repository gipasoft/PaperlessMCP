using FluentAssertions;
using ModelContextProtocol.AspNetCore;
using PaperlessMCP.Configuration;
using Xunit;

namespace PaperlessMCP.Tests.Configuration;

public class McpHttpTransportConfigurationTests
{
    [Fact]
    public void Configure_AllowsIndependentRequestsWithoutSessionHeaders()
    {
        var options = new HttpServerTransportOptions();

        McpHttpTransportConfiguration.Configure(options);

        options.Stateless.Should().BeTrue();
        options.IdleTimeout.Should().Be(Timeout.InfiniteTimeSpan);
    }
}
