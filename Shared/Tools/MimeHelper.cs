using Microsoft.AspNetCore.StaticFiles;

namespace Shared.Tools;

public static class MimeHelper
{
    private static readonly FileExtensionContentTypeProvider _provider 
        = new FileExtensionContentTypeProvider();

    /// <summary>
    /// Infer the content‐type (MIME type) of a file by its path or extension.
    /// Falls back to "application/octet-stream" when unknown.
    /// </summary>
    public static string GetMimeType(string filePath)
    {
        if (!_provider.TryGetContentType(filePath, out var contentType))
        {
            contentType = "application/octet-stream";
        }
        return contentType;
    } 
}