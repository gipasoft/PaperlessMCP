using System.Net;
using System.Net.Http.Headers;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using PaperlessMCP.Client;
using PaperlessMCP.Configuration;
using PaperlessMCP.Models.Documents;
using Xunit;

namespace PaperlessMCP.Tests.Client;

public class PaperlessContentAuthenticationTests
{
    [Fact]
    public async Task GetDocumentContentAsync_UsesTheExistingTokenAuthenticationHandler()
    {
        var options = Substitute.For<IOptions<PaperlessOptions>>();
        options.Value.Returns(new PaperlessOptions
        {
            BaseUrl = "https://paperless.example.com",
            ApiToken = "secret-api-token",
            MaxDownloadSizeBytes = 1024
        });
        var upstream = new CaptureHandler();
        using var auth = new PaperlessAuthHandler(options)
        {
            InnerHandler = upstream
        };
        using var http = new HttpClient(auth)
        {
            BaseAddress = new Uri("https://paperless.example.com/")
        };
        var client = new PaperlessClient(
            http,
            options,
            Substitute.For<ILogger<PaperlessClient>>());

        var result = await client.GetDocumentContentAsync(
            9,
            DocumentContentVariant.Download);

        result.IsSuccess.Should().BeTrue();
        upstream.Authorization.Should().NotBeNull();
        upstream.Authorization!.Scheme.Should().Be("Token");
        upstream.Authorization.Parameter.Should().Be("secret-api-token");
        upstream.RequestUri.Should().Be(
            new Uri("https://paperless.example.com/api/documents/9/download/"));
    }

    private sealed class CaptureHandler : HttpMessageHandler
    {
        public AuthenticationHeaderValue? Authorization { get; private set; }
        public Uri? RequestUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Authorization = request.Headers.Authorization;
            RequestUri = request.RequestUri;
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent("pdf"u8.ToArray())
            };
            response.Content.Headers.ContentType =
                new MediaTypeHeaderValue("application/pdf");
            return Task.FromResult(response);
        }
    }
}
