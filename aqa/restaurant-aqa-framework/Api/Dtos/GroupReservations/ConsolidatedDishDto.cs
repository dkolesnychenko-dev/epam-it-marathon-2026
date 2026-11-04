using System.Text.Json.Serialization;

namespace Restaurant_Aqa_Framework.Api.Dtos.GroupReservations;

public sealed record ConsolidatedDishDto(
    [property: JsonPropertyName("dishId")] string DishId,
    [property: JsonPropertyName("quantity")] int Quantity);

