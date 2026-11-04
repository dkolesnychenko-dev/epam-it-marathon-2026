using Restaurant_Aqa_Framework.Api.Dtos.Auth;
using Restaurant_Aqa_Framework.Api.Dtos.Dishes;
using Restaurant_Aqa_Framework.Api.Dtos.Reservations;

namespace Restaurant_Aqa_Framework.Contexts;

public sealed class GuestContext
{
    public required string GuestName { get; init; }
    public required string SessionId { get; init; }
    public required string SessionToken { get; init; }
    public bool IsSubmitted { get; set; }
    public List<(DishResponse Dish, int Quantity)> Dishes { get; } = [];
}

public sealed class GroupReservationContext
{
    public required ReservationResponse Reservation { get; init; }
    public required string GroupReservationId { get; init; }
    public required string InviteToken { get; init; }
    public required string OwnerBearerToken { get; init; }
    public required RegisterRequest OwnerUser { get; init; }
    public List<GuestContext> Guests { get; } = [];
}

