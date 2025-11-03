using System.Text.Json.Serialization;

namespace Shared.Tools;

public struct FcmMessageRoot
{
    [JsonPropertyName("message")]
    public FcmMessage Message { get; set; }
}

public struct FcmNotification
{
    [JsonPropertyName("title")]
    public string Title { get; set; }
    
    [JsonPropertyName("body")]
    public string Body { get; set; }
}

public struct FcmMessage
{
    [JsonPropertyName("token")]
    public string Token { get; set; }

    [JsonPropertyName("data")]
    public Dictionary<string, string>? Data { get; set; }
    
    [JsonPropertyName("notification")]
    public FcmNotification? Notification { get; set; }

    [JsonPropertyName("android")]
    public FcmAndroid Android { get; set; }

    [JsonPropertyName("apns")]
    public FcmApns Apns { get; set; }
}

public struct FcmAndroid
{
    [JsonPropertyName("priority")]
    public string Priority { get; set; }
}

public struct FcmApns
{
    [JsonPropertyName("headers")]
    public Dictionary<string, string>? Headers { get; set; }

    [JsonPropertyName("payload")]
    public FcmApnsPayload Payload { get; set; }
}

public struct FcmApnsPayload
{
    [JsonPropertyName("aps")]
    public FcmAps Aps { get; set; }
}

public struct FcmAps
{
    [JsonPropertyName("content-available")]
    public int ContentAvailable { get; set; }
}