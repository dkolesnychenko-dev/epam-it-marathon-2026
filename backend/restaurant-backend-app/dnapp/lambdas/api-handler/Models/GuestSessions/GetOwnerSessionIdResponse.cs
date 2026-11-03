using System.Text.Json.Serialization;

namespace APIHandler.Models.GuestSessions;

public record GetOwnerSessionIdResponse(
    [property: JsonPropertyName("guestSessionId")] string GuestSessionId,
    [property: JsonPropertyName("groupReservationId")] string? GroupReservationId = null);
