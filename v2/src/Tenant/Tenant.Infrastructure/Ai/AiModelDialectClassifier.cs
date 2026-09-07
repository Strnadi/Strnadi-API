using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Tenant.Domain.Services;

namespace Tenant.Infrastructure.Ai;

public class AiModelDialectClassifier(HttpClient httpClient, ILogger<AiModelDialectClassifier> logger) : IDialectClassifier
{
    // Wire format of the external classification service's response — kept private to
    // Infrastructure; Domain only ever sees the mapped DialectPrediction[].
    private class PredictionResultDto
    {
        public SegmentDto[] Segments { get; set; } = [];
    }

    private class SegmentDto
    {
        public double[] Interval { get; set; } = [];

        public string? Label { get; set; }

        [JsonPropertyName("isRepresentant")]
        public bool IsRepresentant { get; set; }
    }

    public async Task<DialectPrediction[]?> ClassifyAsync(byte[] normalizedAudio, string fileName, CancellationToken cancellationToken = default)
    {
        httpClient.Timeout = TimeSpan.FromHours(1);

        using var form = new MultipartFormDataContent();
        using var fileContent = new ByteArrayContent(normalizedAudio);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");
        form.Add(fileContent, "file", fileName);

        try
        {
            var response = await httpClient.PostAsync("classify", form, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogError("Failed to request classification: {StatusCode}", response.StatusCode);
                return null;
            }

            var result = await response.Content.ReadFromJsonAsync<PredictionResultDto>(
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true },
                cancellationToken);

            if (result?.Segments is null)
                return null;

            return result.Segments
                .Where(s => s.Interval.Length >= 2)
                .Select(s => new DialectPrediction(s.Interval[0], s.Interval[1], s.Label, s.IsRepresentant))
                .ToArray();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to request classification");
            return null;
        }
    }
}