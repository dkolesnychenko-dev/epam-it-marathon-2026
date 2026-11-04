using System.Text.Json.Serialization;

namespace Restaurant_Aqa_Framework.Api.Dtos.GuestSessions;

public sealed record GetOwnerSessionIdResponse(
    [property: JsonPropertyName("guestSessionId")] string GuestSessionId,
    [property: JsonPropertyName("groupReservationId")] string? GroupReservationId = null);

