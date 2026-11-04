using System.Text.Json.Serialization;

namespace Restaurant_Aqa_Framework.Api.Dtos.GroupReservations;

public sealed record GetGroupReservationResponse(
    [property: JsonPropertyName("groupReservationId")] string GroupReservationId,
    [property: JsonPropertyName("locationId")] string LocationId,
    [property: JsonPropertyName("restaurantName")] string RestaurantName,
    [property: JsonPropertyName("locationAddress")] string LocationAddress,
    [property: JsonPropertyName("startTime")] DateTime StartTime,
    [property: JsonPropertyName("endTime")] DateTime EndTime,
    [property: JsonPropertyName("maxGuestCount")] int MaxGuestCount,
    [property: JsonPropertyName("tableNumber")] int TableNumber,
    [property: JsonPropertyName("inviterName")] string InviterName);

