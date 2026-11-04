using System.Text.Json.Serialization;

namespace Restaurant_Aqa_Framework.Api.Dtos.GuestSessions;

public sealed record GroupReservationStatusDto(
    [property: JsonPropertyName("currentParticipants")] int CurrentParticipants,
    [property: JsonPropertyName("maxCapacity")] int MaxCapacity,
    [property: JsonPropertyName("status")] string Status);

