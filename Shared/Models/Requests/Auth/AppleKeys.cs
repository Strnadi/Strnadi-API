using System.Text.Json.Serialization;

namespace Shared.Models.Requests.Auth;

public class AppleKey
{
    [JsonPropertyName("kid")]
    public string Kid { get; set; }

    [JsonPropertyName("n")]
    public string N { get; set; }

    [JsonPropertyName("e")]
    public string E { get; set; }
}

public class AppleKeysResponse
{
    [JsonPropertyName("keys")]
    public AppleKey[] Keys { get; set; }
}