using System.Text.Json.Serialization;

namespace Restaurant_Aqa_Framework.Api.Dtos.GuestSessions;

public sealed record GuestOrderItemDto(
    [property: JsonPropertyName("dish")] GuestDishDto Dish,
    [property: JsonPropertyName("quantity")] int Quantity);

