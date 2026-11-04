using System.Text.Json.Serialization;

namespace Restaurant_Aqa_Framework.Api.Dtos.GuestSessions;

public sealed record UpdateGuestSessionDishRequest(
    [property: JsonPropertyName("dishId")] string DishId,
    [property: JsonPropertyName("quantity")] int Quantity);

