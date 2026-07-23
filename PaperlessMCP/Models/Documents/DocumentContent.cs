using System.Text.Json.Serialization;

namespace PaperlessMCP.Models.Documents;

public enum DocumentContentVariant
{
    Download,
    Preview
}

public enum DocumentContentErrorKind
{
    Validation,
    NotFound,
    UpstreamError,
    DocumentTooLarge,
    Timeout,
    UnsupportedContentType
}

public sealed record DocumentContentPayload(
    byte[] Data,
    string MimeType,
    string FileName,
    long Size);

public readonly record struct DocumentContentResult
{
    public bool IsSuccess { get; init; }
    public DocumentContentPayload? Content { get; init; }
    public DocumentContentErrorKind? ErrorKind { get; init; }
    public string? ErrorMessage { get; init; }

    public static DocumentContentResult Success(DocumentContentPayload content) =>
        new() { IsSuccess = true, Content = content };

    public static DocumentContentResult Failure(
        DocumentContentErrorKind kind,
        string message) =>
        new() { IsSuccess = false, ErrorKind = kind, ErrorMessage = message };
}

public sealed record DocumentContentToolPayload
{
    [JsonPropertyName("data")]
    public required string Data { get; init; }

    [JsonPropertyName("mime_type")]
    public required string MimeType { get; init; }

    [JsonPropertyName("filename")]
    public required string FileName { get; init; }

    [JsonPropertyName("size")]
    public long Size { get; init; }
}
