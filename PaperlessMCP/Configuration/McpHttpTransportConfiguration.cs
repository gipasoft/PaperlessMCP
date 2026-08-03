using ModelContextProtocol.AspNetCore;

namespace PaperlessMCP.Configuration;

public static class McpHttpTransportConfiguration
{
    public static void Configure(HttpServerTransportOptions options)
    {
        options.Stateless = true;
        options.IdleTimeout = Timeout.InfiniteTimeSpan;
    }
}
