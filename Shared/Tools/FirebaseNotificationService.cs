using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Google.Apis.Auth.OAuth2;
using Microsoft.Extensions.Configuration;
using Shared.Logging;
using LogLevel = Shared.Logging.LogLevel;

namespace Shared.Tools;

public class FirebaseNotificationService
{
    private readonly GoogleCredential _credential;
    private readonly string _projectId;
    
    public FirebaseNotificationService(IConfiguration configuration)
    {
        string rawJson = configuration["Firebase:ServiceAccountJson"] 
                         ?? throw new NullReferenceException("Firebase Service Account JSON is not configured.");

        _credential = GoogleCredential.FromJson(rawJson)
            .CreateScoped("https://www.googleapis.com/auth/firebase.messaging");
        
        var parsed = JsonDocument.Parse(rawJson);
        _projectId = parsed.RootElement.GetProperty("project_id").GetString()!;
    }

    public async Task SendVisibleNotificationAsync(string fcmToken, string title, string body)
    {
        await SendNotificationBaseAsync(new FcmMessageRoot
        {
            Message = new FcmMessage
            {
                Token = fcmToken,
                Notification = new FcmNotification
                {
                    Title = title,
                    Body = body,
                },
                Android = new FcmAndroid { Priority = "HIGH" },
                Apns = new FcmApns
                {
                    Headers = new Dictionary<string, string> { { "apns-priority", "5" } },
                    Payload = new FcmApnsPayload
                    {
                        Aps = new FcmAps { ContentAvailable = 1 }
                    }
                }
            }
        });
    }
    
    public async Task SendInvisibleNotificationAsync(string fcmToken, Dictionary<string, string> data)
    {
        await SendNotificationBaseAsync(new
        {
            message = new
            {
                token = fcmToken,
                data,
                android = new { priority = "HIGH" },
                apns = new
                {
                    headers = new Dictionary<string, string> { { "apns-priority", "5" } },
                    payload = new FcmApnsPayload
                    {
                        Aps = new FcmAps { ContentAvailable = 1 }
                    }
                }
            }
        });
    }
    
    private async Task SendNotificationBaseAsync(object payload)
    {
        var accessToken = await _credential.UnderlyingCredential.GetAccessTokenForRequestAsync();

        using var http = new HttpClient();
        http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);
        
        var json = JsonSerializer.Serialize(payload);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var url = $"https://fcm.googleapis.com/v1/projects/{_projectId}/messages:send";

        var response = await http.PostAsync(url, content);
        var responseBody = await response.Content.ReadAsStringAsync();    
        
        if (!response.IsSuccessStatusCode)
        {
            throw new Exception($"Firebase send error: {response.StatusCode}, {responseBody}");
        }
    }
}