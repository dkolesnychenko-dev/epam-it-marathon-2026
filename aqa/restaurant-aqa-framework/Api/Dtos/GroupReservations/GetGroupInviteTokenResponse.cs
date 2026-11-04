using System.Text.Json.Serialization;

namespace Restaurant_Aqa_Framework.Api.Dtos.GroupReservations;

public sealed record GetGroupInviteTokenResponse([property: JsonPropertyName("token")] string Token);

