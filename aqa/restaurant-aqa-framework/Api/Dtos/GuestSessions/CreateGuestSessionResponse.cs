using System.Text.Json.Serialization;

namespace Restaurant_Aqa_Framework.Api.Dtos.GuestSessions;

public sealed record CreateGuestSessionResponse(
    [property: JsonPropertyName("sessionId")] string SessionId,
    [property: JsonPropertyName("sessionToken")] string SessionToken);

