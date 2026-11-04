using System.Text.Json.Serialization;

namespace Restaurant_Aqa_Framework.Api.Dtos.GuestSessions;

public sealed record GuestDishDto(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("price")] decimal Price);

