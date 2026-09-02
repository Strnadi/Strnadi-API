using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Google.Apis.Auth.OAuth2;
using Tenant.Domain.Configuration;
using Tenant.Domain.Services;

namespace Tenant.Infrastructure.Notifications;

public class FirebaseNotificationService : IPushNotificationService
{
    private readonly HttpClient _httpClient;
    private readonly GoogleCredential _credential;
    private readonly string _projectId;

    public FirebaseNotificationService(HttpClient httpClient, IFirebaseSettings settings)
    {
        _httpClient = httpClient;

        _credential = GoogleCredential.FromJson(settings.ServiceAccountJson)
            .CreateScoped("https://www.googleapis.com/auth/firebase.messaging");

        using var parsed = JsonDocument.Parse(settings.ServiceAccountJson);
        _projectId = parsed.RootElement.GetProperty("project_id").GetString()!;
    }

    public Task SendVisibleNotificationAsync(string fcmToken, string title, string body, CancellationToken cancellationToken = default) =>
        SendAsync(new
        {
            message = new
            {
                token = fcmToken,
                notification = new { title, body },
                android = new { priority = "HIGH" },
                apns = new
                {
                    headers = new Dictionary<string, string> { { "apns-priority", "5" } },
                    payload = new { aps = new { content_available = 1 } }
                }
            }
        }, cancellationToken);

    public Task SendInvisibleNotificationAsync(string fcmToken, IReadOnlyDictionary<string, string?> data, CancellationToken cancellationToken = default) =>
        SendAsync(new
        {
            message = new
            {
                token = fcmToken,
                data,
                android = new { priority = "HIGH" },
                apns = new
                {
                    headers = new Dictionary<string, string> { { "apns-priority", "5" } },
                    payload = new { aps = new { content_available = 1 } }
                }
            }
        }, cancellationToken);

    private async Task SendAsync(object payload, CancellationToken cancellationToken)
    {
        var accessToken = await _credential.UnderlyingCredential.GetAccessTokenForRequestAsync(cancellationToken: cancellationToken);

        using var request = new HttpRequestMessage(HttpMethod.Post, $"https://fcm.googleapis.com/v1/projects/{_projectId}/messages:send");
        request.Content = JsonContent.Create(payload);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await _httpClient.SendAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"Firebase send error: {response.StatusCode}, {body}");
        }
    }
}
