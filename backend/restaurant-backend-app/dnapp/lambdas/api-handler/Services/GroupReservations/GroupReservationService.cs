using APIHandler.Constants;
using APIHandler.Constants.Orders;
using APIHandler.Entities;
using APIHandler.Exceptions;
using APIHandler.Models.GroupReservation;
using APIHandler.Models.GuestReservation;
using APIHandler.Repositories.GroupReservations;
using APIHandler.Repositories.GuestSessions;
using APIHandler.Repositories.Orders;
using APIHandler.Repositories.Reservations;
using APIHandler.Repositories.Tables;
using APIHandler.Services.Auth;
using APIHandler.Services.Locations;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace APIHandler.Services.GroupReservations;

public class GroupReservationService(
    IGroupReservationRepository groupReservationRepository,
    ITableRepository tableRepository,
    IReservationRepository reservationRepository,
    IGuestSessionRepository guestSessionRepository,
    IUserService userService,
    ILocationService locationService,
    IOrderRepository orderRepository) : IGroupReservationService
{
    private const string DefaultRestaurantName = "Green & Tasty";

    public async Task<GetGroupReservationResponse> GetGroupReservationByTokenAsync(string token)
    {
        GroupReservation groupReservation = await groupReservationRepository.GetGroupReservationByTokenAsync(token)
            ?? throw new GroupReservationNotFoundException();

        if (groupReservation.Status != GroupReservationStatus.Open)
            throw new GroupReservationExpiredException();

        Task<Reservation?> reservationTask = reservationRepository.GetReservationByIdAsync(groupReservation.ReservationId);
        Task<int> countTask = guestSessionRepository.CountByGroupReservationId(groupReservation.GroupReservationId);

        Reservation reservation = await reservationTask ?? throw new ValidationException("Associated reservation not found.");
        int joinedGuestsCount = await countTask;

        if (joinedGuestsCount >= reservation.GuestCount)
            throw new GuestLimitExceededException();

        Table table = await tableRepository.GetTableAsync(reservation.TableId)
            ?? throw new InvalidOperationException("Associated table not found.");

        Location? location = await locationService.GetLocationByIdAsync(table.LocationId);
        string restaurantName = DefaultRestaurantName;
        string address = location?.Address ?? "Unknown Address";

        string inviterName = "Unknown";
        if (!string.IsNullOrWhiteSpace(reservation.CustomerId))
        {
            inviterName = await userService.GetUserName(reservation.CustomerId) ?? "Unknown";
        }
        return new GetGroupReservationResponse(
            groupReservation.GroupReservationId,
            table.LocationId,
            restaurantName,
            address,
            reservation.StartTime,
            reservation.EndTime,
            reservation.GuestCount,
            table.TableNumber,
            inviterName);
    }

    public async Task<GroupReservation> GetGroupReservationAsync(string groupReservationId)
    {
        GroupReservation? groupReservation = await groupReservationRepository.GetGroupReservationByIdAsync(groupReservationId);
        return groupReservation ?? throw new InvalidOperationException($"Group reservation with ID {groupReservationId} not found.");
    }

    public async Task<GroupReservation> CreateGroupReservationAsync(string reservationId)
    {
        var groupReservation = new GroupReservation
        {
            GroupReservationId = Guid.NewGuid().ToString(),
            ReservationId = reservationId,
            ReservationToken = Guid.NewGuid().ToString("N"),
            Status = GroupReservationStatus.Open,
        };

        await groupReservationRepository.SaveGroupReservationAsync(groupReservation);
        return groupReservation;
    }

    public async Task CreateGroupReservationIfDoesNotExistAsync(string userId, string reservationId)
    {
        GroupReservation? group = await groupReservationRepository.GetByReservationIdAsync(reservationId);
        if (group is not null)
            return;

        await CreateGroupReservationAsync(reservationId);
    }

    public async Task UpdateGroupReservationStatusAsync(string groupReservationId, GroupReservationStatus status)
    {
        var groupReservation = await GetGroupReservationAsync(groupReservationId);

        groupReservation.Status = status;

        await groupReservationRepository.SaveGroupReservationAsync(groupReservation);
    }

    public async Task DeleteGroupReservationAsync(string groupReservationId)
    {
        var groupReservation = await GetGroupReservationAsync(groupReservationId);

        await groupReservationRepository.DeleteGroupReservationAsync(groupReservation);
    }

    public async Task CloseGroupReservationAsync(string reservationId)
    {
        GroupReservation? groupReservation = await groupReservationRepository.GetByReservationIdAsync(reservationId);
        if (groupReservation is null)
            return;

        groupReservation.Status = GroupReservationStatus.Closed;

        await groupReservationRepository.SaveGroupReservationAsync(groupReservation);
    }

    public async Task<GetGroupInviteTokenResponse> GetGroupReservationInviteTokenAsync(string reservationId)
    {
        var reservation = await reservationRepository.GetReservationByIdAsync(reservationId)
            ?? throw new ReservationNotFoundException($"Reservation {reservationId} was not found.");

        if (reservation.GuestCount < 2)
            throw new ValidationException("Retrieving invitation token for the reservation with only one guest is not permitted.");

        if (reservation.Status != ReservationStatus.Reserved)
            throw new GroupReservationExpiredException("The group reservation is expired.");

        GroupReservation groupReservation = (await groupReservationRepository.GetByReservationIdAsync(reservationId))!;

        if (groupReservation.Status == GroupReservationStatus.Submitted)
            throw new GroupReservationSubmittedException();

        if (groupReservation.Status == GroupReservationStatus.Closed)
            throw new GroupReservationExpiredException();

        return new GetGroupInviteTokenResponse(groupReservation.ReservationToken);
    }

    public async Task<List<ConsolidatedDishResponse>> SubmitConsolidatedPreOrderAsync(
        string userId,
        string groupReservationId,
        string reservationId)
    {
        var groupReservation = await groupReservationRepository.GetGroupReservationByIdAsync(groupReservationId)
            ?? throw new GroupReservationNotFoundException($"Group reservation with ID {groupReservationId} was not found.");

        if (groupReservation.ReservationId != reservationId)
        {
            throw new ValidationException("Group reservation does not match the specified reservation ID.");
        }

        if (groupReservation.Status == GroupReservationStatus.Submitted)
        {
            throw new GroupReservationSubmittedException();
        }

        if (groupReservation.Status == GroupReservationStatus.Closed)
        {
            throw new GroupReservationExpiredException();
        }

        var reservation = await reservationRepository.GetReservationByIdAsync(reservationId)
            ?? throw new ReservationNotFoundException($"Reservation with ID {reservationId} was not found.");

        if (!string.Equals(reservation.CustomerId, userId, StringComparison.OrdinalIgnoreCase))
        {
            throw new ActionForbiddenException("Only the reservation owner can submit the consolidated pre-order.");
        }

        if (reservation.Status != ReservationStatus.Reserved)
        {
            throw new ValidationException($"Pre-order can only be submitted for reservations in 'Reserved' status. Current status: '{reservation.Status}'.");
        }

        var order = await orderRepository.GetOrderByReservationIdAsync(reservationId)
            ?? throw new ValidationException($"Order for reservation '{reservationId}' was not found.");

        var sessions = await guestSessionRepository.GetAllByGroupReservationIdAsync(groupReservationId);

        var unsubmittedSessions = sessions.Where(s => !s.IsOwnerSession && !s.IsSubmitted).ToList();
        foreach (var unsubmitted in unsubmittedSessions)
        {
            await guestSessionRepository.DeleteAsync(unsubmitted);
        }

        var submittedSessions = sessions.Where(s => !s.IsOwnerSession && s.IsSubmitted).ToList();
        if (submittedSessions.Count == 0 && (order.Dishes == null || order.Dishes.Count == 0))
        {
            throw new ValidationException("There are no submitted guest sessions.");
        }

        var aggregatedDishes = submittedSessions
            .Where(s => s.Dishes != null)
            .SelectMany(s => s.Dishes)
            .GroupBy(d => d.DishId)
            .Select(g => new ConsolidatedDishResponse(g.Key, g.Sum(d => d.Quantity)))
            .Where(d => d.Quantity > 0)
            .ToList();

        if (aggregatedDishes.Count == 0 && (order.Dishes == null || order.Dishes.Count == 0))
        {
            throw new ValidationException("Submitted guest sessions contain no dishes.");
        }

        order.Dishes ??= [];
        foreach (var dish in aggregatedDishes)
        {
            var existingItem = order.Dishes.FirstOrDefault(d => d.DishId == dish.DishId);
            if (existingItem != null)
            {
                existingItem.Quantity += dish.Quantity;
            }
            else
            {
                order.Dishes.Add(new OrderItem
                {
                    DishId = dish.DishId,
                    Quantity = dish.Quantity,
                    State = OrderItemState.Pending
                });
            }
        }

        order.Status = OrderStatus.SubmittedByCustomer;
        await orderRepository.SaveOrderAsync(order);

        groupReservation.Status = GroupReservationStatus.Submitted;
        await groupReservationRepository.SaveGroupReservationAsync(groupReservation);

        return aggregatedDishes;
    }
}
