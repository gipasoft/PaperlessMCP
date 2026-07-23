namespace PaperlessMCP.Configuration;

/// <summary>
/// Configuration options for connecting to the Paperless-ngx API.
/// </summary>
public class PaperlessOptions
{
    public const int DefaultMaxPageSize = 100;
    public const long DefaultMaxDownloadSizeBytes = 10 * 1024 * 1024;
    public const long MaximumDownloadSizeBytes = 100 * 1024 * 1024;

    /// <summary>
    /// Base URL of the Paperless-ngx instance (e.g., https://docs.example.com).
    /// </summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// API token for authentication.
    /// </summary>
    public string ApiToken { get; set; } = string.Empty;

    /// <summary>
    /// Maximum page size for paginated requests.
    /// </summary>
    public int MaxPageSize { get; set; } = DefaultMaxPageSize;

    /// <summary>
    /// HTTP request timeout in seconds for calls to the Paperless-ngx API.
    /// Large full-text searches over big libraries can exceed the default.
    /// </summary>
    public int HttpTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Maximum binary document size returned by content tools.
    /// </summary>
    public long MaxDownloadSizeBytes { get; set; } = DefaultMaxDownloadSizeBytes;
}
