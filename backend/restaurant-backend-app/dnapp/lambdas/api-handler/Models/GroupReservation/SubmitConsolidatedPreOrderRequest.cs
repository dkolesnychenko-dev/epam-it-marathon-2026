using System.Text.Json.Serialization;

namespace APIHandler.Models.GroupReservation;

public record SubmitConsolidatedPreOrderRequest(
    [property: JsonPropertyName("reservationId")] string? ReservationId);
