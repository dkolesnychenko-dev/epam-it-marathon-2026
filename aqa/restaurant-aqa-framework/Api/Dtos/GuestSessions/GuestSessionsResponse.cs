using System.Text.Json.Serialization;

namespace Restaurant_Aqa_Framework.Api.Dtos.GuestSessions;

public sealed record GuestSessionsResponse(
    [property: JsonPropertyName("participants")] IReadOnlyList<ParticipantDto> Participants,
    [property: JsonPropertyName("total")] decimal Total);

