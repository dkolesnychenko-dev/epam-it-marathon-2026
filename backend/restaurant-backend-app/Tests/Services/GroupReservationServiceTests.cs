using APIHandler.Constants;
using APIHandler.Constants.Orders;
using APIHandler.Entities;
using APIHandler.Exceptions;
using APIHandler.Models.GroupReservation;
using APIHandler.Repositories.GroupReservations;
using APIHandler.Repositories.GuestSessions;
using APIHandler.Repositories.Orders;
using APIHandler.Repositories.Reservations;
using APIHandler.Repositories.Tables;
using APIHandler.Services.Auth;
using APIHandler.Services.GroupReservations;
using APIHandler.Services.Locations;
using AutoFixture.Xunit2;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace APIHandler.Tests.Services;

public class GroupReservationServiceTests
{
    private readonly Mock<IGroupReservationRepository> _groupReservationRepositoryMock = new();
    private readonly Mock<ITableRepository> _tableRepositoryMock = new();
    private readonly Mock<IReservationRepository> _reservationRepositoryMock = new();
    private readonly Mock<IGuestSessionRepository> _guestSessionRepositoryMock = new();
    private readonly Mock<IUserService> _userServiceMock = new();
    private readonly Mock<ILocationService> _locationServiceMock = new();
    private readonly Mock<IOrderRepository> _orderRepositoryMock = new();

    private readonly GroupReservationService _service;

    public GroupReservationServiceTests()
    {
        _service = new GroupReservationService(
            _groupReservationRepositoryMock.Object,
            _tableRepositoryMock.Object,
            _reservationRepositoryMock.Object,
            _guestSessionRepositoryMock.Object,
            _userServiceMock.Object,
            _locationServiceMock.Object,
            _orderRepositoryMock.Object);
    }

    [Theory]
    [AutoData]
    public async Task GetGroupReservationByTokenAsync_ValidInput_ReturnsGroupReservationInfo(
        GroupReservation groupReservation,
        Table table,
        Reservation reservation,
        Location location,
        string expectedUserName)
    {
        groupReservation.ReservationId = reservation.Id;
        groupReservation.Status = GroupReservationStatus.Open;
        reservation.TableId = table.Id;
        table.LocationId = location.Id;

        _groupReservationRepositoryMock
            .Setup(r => r.GetGroupReservationByTokenAsync(groupReservation.ReservationToken))
            .ReturnsAsync(groupReservation);

        _reservationRepositoryMock
            .Setup(r => r.GetReservationByIdAsync(reservation.Id))
            .ReturnsAsync(reservation);

        _guestSessionRepositoryMock
            .Setup(r => r.CountByGroupReservationId(groupReservation.GroupReservationId))
            .ReturnsAsync(0);

        _tableRepositoryMock
            .Setup(r => r.GetTableAsync(table.Id))
            .ReturnsAsync(table);

        _locationServiceMock
            .Setup(s => s.GetLocationByIdAsync(table.LocationId))
            .ReturnsAsync(location);

        _userServiceMock
            .Setup(s => s.GetUserName(reservation.CustomerId!))
            .ReturnsAsync(expectedUserName);

        var result = await _service.GetGroupReservationByTokenAsync(groupReservation.ReservationToken);

        Assert.Equal(groupReservation.GroupReservationId, result.GroupReservationId);
        Assert.Equal(table.LocationId, result.LocationId);
        Assert.Equal(reservation.GuestCount, result.MaxGuestCount);
        Assert.Equal(reservation.StartTime, result.StartTime);
        Assert.Equal(reservation.EndTime, result.EndTime);
        Assert.Equal(table.TableNumber, result.TableNumber);
        Assert.Equal("Green & Tasty", result.RestaurantName);
        Assert.Equal(location.Address, result.LocationAddress);
        Assert.Equal(expectedUserName, result.InviterName);
    }

    [Theory]
    [AutoData]
    public async Task GetGroupReservationByTokenAsync_InvalidToken_ThrowsNotFound(string token)
    {
        _groupReservationRepositoryMock
            .Setup(r => r.GetGroupReservationByTokenAsync(token))
            .ReturnsAsync((GroupReservation?)null);

        await Assert.ThrowsAsync<GroupReservationNotFoundException>(() => _service.GetGroupReservationByTokenAsync(token));
    }

    [Theory]
    [InlineAutoData(GroupReservationStatus.Closed)]
    [InlineAutoData(GroupReservationStatus.Submitted)]
    public async Task GetGroupReservationByTokenAsync_GroupReservationIsNotOpen_ThrowsException(
        GroupReservationStatus status,
        GroupReservation groupReservation)
    {
        groupReservation.Status = status;

        _groupReservationRepositoryMock
            .Setup(r => r.GetGroupReservationByTokenAsync(groupReservation.ReservationToken))
            .ReturnsAsync(groupReservation);

        await Assert.ThrowsAsync<GroupReservationExpiredException>(() => _service.GetGroupReservationByTokenAsync(groupReservation.ReservationToken));
    }

    [Theory]
    [AutoData]
    public async Task GetGroupReservationByTokenAsync_WhenGuestCountIsMax_ThrowsGuestLimitExceeded(GroupReservation groupReservation, Reservation reservation)
    {
        groupReservation.Status = GroupReservationStatus.Open;
        groupReservation.ReservationId = reservation.Id;

        _groupReservationRepositoryMock
            .Setup(r => r.GetGroupReservationByTokenAsync(groupReservation.ReservationToken))
            .ReturnsAsync(groupReservation);

        _reservationRepositoryMock
            .Setup(r => r.GetReservationByIdAsync(reservation.Id))
            .ReturnsAsync(reservation);

        _guestSessionRepositoryMock
            .Setup(r => r.CountByGroupReservationId(groupReservation.GroupReservationId))
            .ReturnsAsync(reservation.GuestCount);

        await Assert.ThrowsAsync<GuestLimitExceededException>(() => _service.GetGroupReservationByTokenAsync(groupReservation.ReservationToken));
    }

    [Theory]
    [AutoData]
    public async Task CloseGroupReservationAsync_ValidInput_UpdatesGroupReservationStatus(GroupReservation groupReservation)
    {
        _groupReservationRepositoryMock
            .Setup(r => r.GetByReservationIdAsync(groupReservation.ReservationId))
            .ReturnsAsync(groupReservation);

        GroupReservation? savedGroupReservation = null;
        _groupReservationRepositoryMock
            .Setup(r => r.SaveGroupReservationAsync(It.IsAny<GroupReservation>()))
            .Callback<GroupReservation>(r => savedGroupReservation = r);

        await _service.CloseGroupReservationAsync(groupReservation.ReservationId);

        Assert.NotNull(savedGroupReservation);
        Assert.Equal(GroupReservationStatus.Closed, savedGroupReservation.Status);
    }

    [Theory]
    [AutoData]
    public async Task CloseGroupReservationAsync_GroupReservationNotFound_DoesNotThrowException(string reservationId)
    {
        _groupReservationRepositoryMock
            .Setup(r => r.GetByReservationIdAsync(reservationId))
            .ReturnsAsync((GroupReservation?)null);

        await _service.CloseGroupReservationAsync(reservationId);
    }

    [Theory]
    [AutoData]
    public async Task GetGroupReservationInviteTokenAsync_ValidInput_ReturnsToken(Reservation reservation, GroupReservation groupReservation)
    {
        reservation.Status = ReservationStatus.Reserved;
        reservation.GuestCount = 3;
        groupReservation.ReservationId = reservation.Id;
        groupReservation.Status = GroupReservationStatus.Open;

        _reservationRepositoryMock
            .Setup(r => r.GetReservationByIdAsync(reservation.Id))
            .ReturnsAsync(reservation);

        _groupReservationRepositoryMock
            .Setup(r => r.GetByReservationIdAsync(reservation.Id))
            .ReturnsAsync(groupReservation);

        GetGroupInviteTokenResponse result = await _service.GetGroupReservationInviteTokenAsync(reservation.Id);

        Assert.Equal(groupReservation.ReservationToken, result.Token);
    }

    [Theory]
    [AutoData]
    public async Task GetGroupReservationInviteTokenAsync_InvalidReservationId_ThrowsNotFound(string reservationId)
    {
        _reservationRepositoryMock
            .Setup(r => r.GetReservationByIdAsync(reservationId))
            .ReturnsAsync((Reservation?)null);

        await Assert.ThrowsAsync<ReservationNotFoundException>(() => _service.GetGroupReservationInviteTokenAsync(reservationId));
    }

    [Theory]
    [AutoData]
    public async Task GetGroupReservationInviteTokenAsync_OneGuest_ThrowsValidationException(Reservation reservation)
    {
        reservation.Status = ReservationStatus.Reserved;
        reservation.GuestCount = 1;

        _reservationRepositoryMock
            .Setup(r => r.GetReservationByIdAsync(reservation.Id))
            .ReturnsAsync(reservation);

        await Assert.ThrowsAsync<ValidationException>(() => _service.GetGroupReservationInviteTokenAsync(reservation.Id));
    }

    [Theory]
    [InlineAutoData(ReservationStatus.Completed)]
    [InlineAutoData(ReservationStatus.Cancelled)]
    [InlineAutoData(ReservationStatus.InProgress)]
    public async Task GetGroupReservationInviteTokenAsync_StatusIsNotReserved_ThrowsValidationException(ReservationStatus reservationStatus, Reservation reservation)
    {
        reservation.Status = reservationStatus;
        reservation.GuestCount = 1;

        _reservationRepositoryMock
            .Setup(r => r.GetReservationByIdAsync(reservation.Id))
            .ReturnsAsync(reservation);

        await Assert.ThrowsAsync<ValidationException>(() => _service.GetGroupReservationInviteTokenAsync(reservation.Id));
    }

    [Theory]
    [InlineAutoData(GroupReservationStatus.Closed, typeof(GroupReservationExpiredException))]
    [InlineAutoData(GroupReservationStatus.Submitted, typeof(GroupReservationSubmittedException))]
    public async Task GetGroupReservationInviteTokenAsync_GroupStatusIsNotOpen_ThrowsException(GroupReservationStatus status, Type type, Reservation reservation, GroupReservation groupReservation)
    {
        reservation.Status = ReservationStatus.Reserved;
        reservation.GuestCount = 3;
        groupReservation.ReservationId = reservation.Id;
        groupReservation.Status = status;

        _reservationRepositoryMock
            .Setup(r => r.GetReservationByIdAsync(reservation.Id))
            .ReturnsAsync(reservation);

        _groupReservationRepositoryMock
            .Setup(r => r.GetByReservationIdAsync(reservation.Id))
            .ReturnsAsync(groupReservation);

        await Assert.ThrowsAsync(type, () => _service.GetGroupReservationInviteTokenAsync(reservation.Id));
    }

    [Fact]
    public async Task SubmitConsolidatedPreOrderAsync_ValidSubmittedSessions_AggregatesQuantitiesAndUpdatesOrderAndGroupReservation()
    {
        // Arrange
        const string userId = "user-123";
        const string groupReservationId = "group-123";
        const string reservationId = "res-123";

        var groupReservation = new GroupReservation
        {
            GroupReservationId = groupReservationId,
            ReservationId = reservationId,
            ReservationToken = "token-abc",
            Status = GroupReservationStatus.Open
        };

        var reservation = new Reservation
        {
            Id = reservationId,
            CustomerId = userId,
            GuestCount = 3,
            TableId = "table-1", WaiterId = "waiter-1",
            StartTime = DateTime.UtcNow,
            EndTime = DateTime.UtcNow.AddHours(2),
            Status = ReservationStatus.Reserved
        };

        var order = new Order
        {
            OrderId = "order-1",
            ReservationId = reservationId,
            CustomerId = userId,
            ReservationAddress = "123 Main St",
            ReservationTime = "7:00 PM",
            Dishes = [],
            Status = OrderStatus.Created
        };

        var session1 = new GuestSession
        {
            Id = "session-1",
            GroupReservationId = groupReservationId,
            GuestName = "Guest 1",
            IsSubmitted = true,
            Dishes =
            [
                new OrderItem { DishId = "dish1", Quantity = 2, State = OrderItemState.Pending }
            ]
        };

        var session2 = new GuestSession
        {
            Id = "session-2",
            GroupReservationId = groupReservationId,
            GuestName = "Guest 2",
            IsSubmitted = true,
            Dishes =
            [
                new OrderItem { DishId = "dish1", Quantity = 1, State = OrderItemState.Pending },
                new OrderItem { DishId = "dish2", Quantity = 2, State = OrderItemState.Pending }
            ]
        };

        _groupReservationRepositoryMock
            .Setup(r => r.GetGroupReservationByIdAsync(groupReservationId))
            .ReturnsAsync(groupReservation);

        _reservationRepositoryMock
            .Setup(r => r.GetReservationByIdAsync(reservationId))
            .ReturnsAsync(reservation);

        _orderRepositoryMock
            .Setup(r => r.GetOrderByReservationIdAsync(reservationId))
            .ReturnsAsync(order);

        _guestSessionRepositoryMock
            .Setup(r => r.GetAllByGroupReservationIdAsync(groupReservationId))
            .ReturnsAsync([session1, session2]);

        // Act
        var result = await _service.SubmitConsolidatedPreOrderAsync(userId, groupReservationId, reservationId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Count);

        var dish1Result = result.FirstOrDefault(d => d.DishId == "dish1");
        Assert.NotNull(dish1Result);
        Assert.Equal(3, dish1Result.Quantity);

        var dish2Result = result.FirstOrDefault(d => d.DishId == "dish2");
        Assert.NotNull(dish2Result);
        Assert.Equal(2, dish2Result.Quantity);

        Assert.Equal(OrderStatus.SubmittedByCustomer, order.Status);
        Assert.Equal(2, order.Dishes.Count);
        Assert.Equal(3, order.Dishes.First(d => d.DishId == "dish1").Quantity);
        Assert.Equal(2, order.Dishes.First(d => d.DishId == "dish2").Quantity);

        Assert.Equal(GroupReservationStatus.Submitted, groupReservation.Status);

        _orderRepositoryMock.Verify(r => r.SaveOrderAsync(order), Times.Once);
        _groupReservationRepositoryMock.Verify(r => r.SaveGroupReservationAsync(groupReservation), Times.Once);
    }

    [Fact]
    public async Task SubmitConsolidatedPreOrderAsync_UnsubmittedSessions_AreIgnored()
    {
        // Arrange
        const string userId = "user-123";
        const string groupReservationId = "group-123";
        const string reservationId = "res-123";

        var groupReservation = new GroupReservation
        {
            GroupReservationId = groupReservationId,
            ReservationId = reservationId,
            ReservationToken = "token-abc",
            Status = GroupReservationStatus.Open
        };

        var reservation = new Reservation
        {
            Id = reservationId,
            CustomerId = userId,
            GuestCount = 3,
            TableId = "table-1", WaiterId = "waiter-1",
            StartTime = DateTime.UtcNow,
            EndTime = DateTime.UtcNow.AddHours(2),
            Status = ReservationStatus.Reserved
        };

        var order = new Order
        {
            OrderId = "order-1",
            ReservationId = reservationId,
            CustomerId = userId,
            ReservationAddress = "123 Main St",
            ReservationTime = "7:00 PM",
            Dishes = [],
            Status = OrderStatus.Created
        };

        var submittedSession = new GuestSession
        {
            Id = "session-1",
            GroupReservationId = groupReservationId,
            GuestName = "Guest 1",
            IsSubmitted = true,
            Dishes = [new OrderItem { DishId = "dish1", Quantity = 2, State = OrderItemState.Pending }]
        };

        var unsubmittedSession = new GuestSession
        {
            Id = "session-2",
            GroupReservationId = groupReservationId,
            GuestName = "Guest 2",
            IsSubmitted = false,
            Dishes = [new OrderItem { DishId = "dish2", Quantity = 5, State = OrderItemState.Pending }]
        };

        _groupReservationRepositoryMock
            .Setup(r => r.GetGroupReservationByIdAsync(groupReservationId))
            .ReturnsAsync(groupReservation);

        _reservationRepositoryMock
            .Setup(r => r.GetReservationByIdAsync(reservationId))
            .ReturnsAsync(reservation);

        _orderRepositoryMock
            .Setup(r => r.GetOrderByReservationIdAsync(reservationId))
            .ReturnsAsync(order);

        _guestSessionRepositoryMock
            .Setup(r => r.GetAllByGroupReservationIdAsync(groupReservationId))
            .ReturnsAsync([submittedSession, unsubmittedSession]);

        // Act
        var result = await _service.SubmitConsolidatedPreOrderAsync(userId, groupReservationId, reservationId);

        // Assert
        Assert.Single(result);
        Assert.Equal("dish1", result[0].DishId);
        Assert.Equal(2, result[0].Quantity);
        Assert.DoesNotContain(result, d => d.DishId == "dish2");

        Assert.Single(order.Dishes);
        Assert.Equal("dish1", order.Dishes[0].DishId);
        Assert.Equal(2, order.Dishes[0].Quantity);
    }

    [Fact]
    public async Task SubmitConsolidatedPreOrderAsync_ExistingDishesInOrder_MergesQuantitiesAndPreservesExistingItems()
    {
        // Arrange
        const string userId = "user-123";
        const string groupReservationId = "group-123";
        const string reservationId = "res-123";

        var groupReservation = new GroupReservation
        {
            GroupReservationId = groupReservationId,
            ReservationId = reservationId,
            ReservationToken = "token-abc",
            Status = GroupReservationStatus.Open
        };

        var reservation = new Reservation
        {
            Id = reservationId,
            CustomerId = userId,
            GuestCount = 3,
            TableId = "table-1", WaiterId = "waiter-1",
            StartTime = DateTime.UtcNow,
            EndTime = DateTime.UtcNow.AddHours(2),
            Status = ReservationStatus.Reserved
        };

        var order = new Order
        {
            OrderId = "order-1",
            ReservationId = reservationId,
            CustomerId = userId,
            ReservationAddress = "123 Main St",
            ReservationTime = "7:00 PM",
            Dishes =
            [
                new OrderItem { DishId = "dish1", Quantity = 2, State = OrderItemState.Pending },
                new OrderItem { DishId = "dish-existing", Quantity = 1, State = OrderItemState.Pending }
            ],
            Status = OrderStatus.Created
        };

        var session = new GuestSession
        {
            Id = "session-1",
            GroupReservationId = groupReservationId,
            GuestName = "Guest 1",
            IsSubmitted = true,
            Dishes =
            [
                new OrderItem { DishId = "dish1", Quantity = 3, State = OrderItemState.Pending },
                new OrderItem { DishId = "dish2", Quantity = 4, State = OrderItemState.Pending }
            ]
        };

        _groupReservationRepositoryMock
            .Setup(r => r.GetGroupReservationByIdAsync(groupReservationId))
            .ReturnsAsync(groupReservation);

        _reservationRepositoryMock
            .Setup(r => r.GetReservationByIdAsync(reservationId))
            .ReturnsAsync(reservation);

        _orderRepositoryMock
            .Setup(r => r.GetOrderByReservationIdAsync(reservationId))
            .ReturnsAsync(order);

        _guestSessionRepositoryMock
            .Setup(r => r.GetAllByGroupReservationIdAsync(groupReservationId))
            .ReturnsAsync([session]);

        // Act
        var result = await _service.SubmitConsolidatedPreOrderAsync(userId, groupReservationId, reservationId);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Equal(3, order.Dishes.Count);

        var mergedDish1 = order.Dishes.First(d => d.DishId == "dish1");
        Assert.Equal(5, mergedDish1.Quantity); // 2 existing + 3 consolidated

        var preservedDish = order.Dishes.First(d => d.DishId == "dish-existing");
        Assert.Equal(1, preservedDish.Quantity);

        var newDish2 = order.Dishes.First(d => d.DishId == "dish2");
        Assert.Equal(4, newDish2.Quantity);
        Assert.Equal(OrderItemState.Pending, newDish2.State);
    }

    [Fact]
    public async Task SubmitConsolidatedPreOrderAsync_OrderDishesInitiallyNull_InitializesAndAddsDishes()
    {
        // Arrange
        const string userId = "user-123";
        const string groupReservationId = "group-123";
        const string reservationId = "res-123";

        var groupReservation = new GroupReservation
        {
            GroupReservationId = groupReservationId,
            ReservationId = reservationId,
            ReservationToken = "token-abc",
            Status = GroupReservationStatus.Open
        };

        var reservation = new Reservation
        {
            Id = reservationId,
            CustomerId = userId,
            GuestCount = 3,
            TableId = "table-1", WaiterId = "waiter-1",
            StartTime = DateTime.UtcNow,
            EndTime = DateTime.UtcNow.AddHours(2),
            Status = ReservationStatus.Reserved
        };

        var order = new Order
        {
            OrderId = "order-1",
            ReservationId = reservationId,
            CustomerId = userId,
            ReservationAddress = "123 Main St",
            ReservationTime = "7:00 PM",
            Dishes = null!,
            Status = OrderStatus.Created
        };

        var session = new GuestSession
        {
            Id = "session-1",
            GroupReservationId = groupReservationId,
            GuestName = "Guest 1",
            IsSubmitted = true,
            Dishes = [new OrderItem { DishId = "dish1", Quantity = 2, State = OrderItemState.Pending }]
        };

        _groupReservationRepositoryMock
            .Setup(r => r.GetGroupReservationByIdAsync(groupReservationId))
            .ReturnsAsync(groupReservation);

        _reservationRepositoryMock
            .Setup(r => r.GetReservationByIdAsync(reservationId))
            .ReturnsAsync(reservation);

        _orderRepositoryMock
            .Setup(r => r.GetOrderByReservationIdAsync(reservationId))
            .ReturnsAsync(order);

        _guestSessionRepositoryMock
            .Setup(r => r.GetAllByGroupReservationIdAsync(groupReservationId))
            .ReturnsAsync([session]);

        // Act
        var result = await _service.SubmitConsolidatedPreOrderAsync(userId, groupReservationId, reservationId);

        // Assert
        Assert.Single(result);
        Assert.NotNull(order.Dishes);
        Assert.Single(order.Dishes);
        Assert.Equal(2, order.Dishes[0].Quantity);
        Assert.Equal(OrderItemState.Pending, order.Dishes[0].State);
    }

    [Fact]
    public async Task SubmitConsolidatedPreOrderAsync_GroupReservationNotFound_ThrowsGroupReservationNotFoundException()
    {
        // Arrange
        const string groupReservationId = "non-existent-group";

        _groupReservationRepositoryMock
            .Setup(r => r.GetGroupReservationByIdAsync(groupReservationId))
            .ReturnsAsync((GroupReservation?)null);

        // Act & Assert
        await Assert.ThrowsAsync<GroupReservationNotFoundException>(
            () => _service.SubmitConsolidatedPreOrderAsync("user-1", groupReservationId, "res-1"));
    }

    [Fact]
    public async Task SubmitConsolidatedPreOrderAsync_ReservationIdMismatch_ThrowsValidationException()
    {
        // Arrange
        const string groupReservationId = "group-1";
        const string actualReservationId = "res-actual";
        const string providedReservationId = "res-different";

        var groupReservation = new GroupReservation
        {
            GroupReservationId = groupReservationId,
            ReservationId = actualReservationId,
            ReservationToken = "token-1",
            Status = GroupReservationStatus.Open
        };

        _groupReservationRepositoryMock
            .Setup(r => r.GetGroupReservationByIdAsync(groupReservationId))
            .ReturnsAsync(groupReservation);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(
            () => _service.SubmitConsolidatedPreOrderAsync("user-1", groupReservationId, providedReservationId));
        Assert.Equal("Group reservation does not match the specified reservation ID.", ex.Message);
    }

    [Fact]
    public async Task SubmitConsolidatedPreOrderAsync_GroupReservationAlreadySubmitted_ThrowsGroupReservationSubmittedException()
    {
        // Arrange
        const string groupReservationId = "group-1";
        const string reservationId = "res-1";

        var groupReservation = new GroupReservation
        {
            GroupReservationId = groupReservationId,
            ReservationId = reservationId,
            ReservationToken = "token-1",
            Status = GroupReservationStatus.Submitted
        };

        _groupReservationRepositoryMock
            .Setup(r => r.GetGroupReservationByIdAsync(groupReservationId))
            .ReturnsAsync(groupReservation);

        // Act & Assert
        await Assert.ThrowsAsync<GroupReservationSubmittedException>(
            () => _service.SubmitConsolidatedPreOrderAsync("user-1", groupReservationId, reservationId));
    }

    [Fact]
    public async Task SubmitConsolidatedPreOrderAsync_GroupReservationClosed_ThrowsGroupReservationExpiredException()
    {
        // Arrange
        const string groupReservationId = "group-1";
        const string reservationId = "res-1";

        var groupReservation = new GroupReservation
        {
            GroupReservationId = groupReservationId,
            ReservationId = reservationId,
            ReservationToken = "token-1",
            Status = GroupReservationStatus.Closed
        };

        _groupReservationRepositoryMock
            .Setup(r => r.GetGroupReservationByIdAsync(groupReservationId))
            .ReturnsAsync(groupReservation);

        // Act & Assert
        await Assert.ThrowsAsync<GroupReservationExpiredException>(
            () => _service.SubmitConsolidatedPreOrderAsync("user-1", groupReservationId, reservationId));
    }

    [Fact]
    public async Task SubmitConsolidatedPreOrderAsync_ReservationNotFound_ThrowsReservationNotFoundException()
    {
        // Arrange
        const string groupReservationId = "group-1";
        const string reservationId = "res-1";

        var groupReservation = new GroupReservation
        {
            GroupReservationId = groupReservationId,
            ReservationId = reservationId,
            ReservationToken = "token-1",
            Status = GroupReservationStatus.Open
        };

        _groupReservationRepositoryMock
            .Setup(r => r.GetGroupReservationByIdAsync(groupReservationId))
            .ReturnsAsync(groupReservation);

        _reservationRepositoryMock
            .Setup(r => r.GetReservationByIdAsync(reservationId))
            .ReturnsAsync((Reservation?)null);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ReservationNotFoundException>(
            () => _service.SubmitConsolidatedPreOrderAsync("user-1", groupReservationId, reservationId));
        Assert.Equal("Reservation not found.", ex.Message);
    }

    [Fact]
    public async Task SubmitConsolidatedPreOrderAsync_CallerNotOwner_ThrowsActionForbiddenException()
    {
        // Arrange
        const string groupReservationId = "group-1";
        const string reservationId = "res-1";
        const string ownerId = "owner-user";
        const string callingUserId = "intruder-user";

        var groupReservation = new GroupReservation
        {
            GroupReservationId = groupReservationId,
            ReservationId = reservationId,
            ReservationToken = "token-1",
            Status = GroupReservationStatus.Open
        };

        var reservation = new Reservation
        {
            Id = reservationId,
            CustomerId = ownerId,
            GuestCount = 2,
            TableId = "table-1", WaiterId = "waiter-1",
            StartTime = DateTime.UtcNow,
            EndTime = DateTime.UtcNow.AddHours(2),
            Status = ReservationStatus.Reserved
        };

        _groupReservationRepositoryMock
            .Setup(r => r.GetGroupReservationByIdAsync(groupReservationId))
            .ReturnsAsync(groupReservation);

        _reservationRepositoryMock
            .Setup(r => r.GetReservationByIdAsync(reservationId))
            .ReturnsAsync(reservation);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ActionForbiddenException>(
            () => _service.SubmitConsolidatedPreOrderAsync(callingUserId, groupReservationId, reservationId));
        Assert.Equal("Only the reservation owner can submit the consolidated pre-order.", ex.Message);
    }

    [Fact]
    public async Task SubmitConsolidatedPreOrderAsync_OrderNotFound_ThrowsValidationException()
    {
        // Arrange
        const string groupReservationId = "group-1";
        const string reservationId = "res-1";
        const string userId = "user-1";

        var groupReservation = new GroupReservation
        {
            GroupReservationId = groupReservationId,
            ReservationId = reservationId,
            ReservationToken = "token-1",
            Status = GroupReservationStatus.Open
        };

        var reservation = new Reservation
        {
            Id = reservationId,
            CustomerId = userId,
            GuestCount = 2,
            TableId = "table-1", WaiterId = "waiter-1",
            StartTime = DateTime.UtcNow,
            EndTime = DateTime.UtcNow.AddHours(2),
            Status = ReservationStatus.Reserved
        };

        _groupReservationRepositoryMock
            .Setup(r => r.GetGroupReservationByIdAsync(groupReservationId))
            .ReturnsAsync(groupReservation);

        _reservationRepositoryMock
            .Setup(r => r.GetReservationByIdAsync(reservationId))
            .ReturnsAsync(reservation);

        _orderRepositoryMock
            .Setup(r => r.GetOrderByReservationIdAsync(reservationId))
            .ReturnsAsync((Order?)null);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(
            () => _service.SubmitConsolidatedPreOrderAsync(userId, groupReservationId, reservationId));
        Assert.Contains($"Order for reservation '{reservationId}' was not found.", ex.Message);
    }

    [Fact]
    public async Task SubmitConsolidatedPreOrderAsync_NoSubmittedSessions_ThrowsValidationException()
    {
        // Arrange
        const string groupReservationId = "group-1";
        const string reservationId = "res-1";
        const string userId = "user-1";

        var groupReservation = new GroupReservation
        {
            GroupReservationId = groupReservationId,
            ReservationId = reservationId,
            ReservationToken = "token-1",
            Status = GroupReservationStatus.Open
        };

        var reservation = new Reservation
        {
            Id = reservationId,
            CustomerId = userId,
            GuestCount = 2,
            TableId = "table-1", WaiterId = "waiter-1",
            StartTime = DateTime.UtcNow,
            EndTime = DateTime.UtcNow.AddHours(2),
            Status = ReservationStatus.Reserved
        };

        var order = new Order
        {
            OrderId = "order-1",
            ReservationId = reservationId,
            CustomerId = userId,
            ReservationAddress = "addr",
            ReservationTime = "time",
            Dishes = [],
            Status = OrderStatus.Created
        };

        var session = new GuestSession
        {
            Id = "session-1",
            GroupReservationId = groupReservationId,
            GuestName = "Guest 1",
            IsSubmitted = false,
            Dishes = [new OrderItem { DishId = "dish1", Quantity = 2, State = OrderItemState.Pending }]
        };

        _groupReservationRepositoryMock
            .Setup(r => r.GetGroupReservationByIdAsync(groupReservationId))
            .ReturnsAsync(groupReservation);

        _reservationRepositoryMock
            .Setup(r => r.GetReservationByIdAsync(reservationId))
            .ReturnsAsync(reservation);

        _orderRepositoryMock
            .Setup(r => r.GetOrderByReservationIdAsync(reservationId))
            .ReturnsAsync(order);

        _guestSessionRepositoryMock
            .Setup(r => r.GetAllByGroupReservationIdAsync(groupReservationId))
            .ReturnsAsync([session]);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(
            () => _service.SubmitConsolidatedPreOrderAsync(userId, groupReservationId, reservationId));
        Assert.Equal("There are no submitted guest sessions.", ex.Message);
    }

    [Theory]
    [InlineData(false)] // Empty dishes list
    [InlineData(true)]  // Dishes with zero quantity
    public async Task SubmitConsolidatedPreOrderAsync_SubmittedSessionsContainNoDishes_ThrowsValidationException(bool hasZeroQuantityDishes)
    {
        // Arrange
        const string groupReservationId = "group-1";
        const string reservationId = "res-1";
        const string userId = "user-1";

        var groupReservation = new GroupReservation
        {
            GroupReservationId = groupReservationId,
            ReservationId = reservationId,
            ReservationToken = "token-1",
            Status = GroupReservationStatus.Open
        };

        var reservation = new Reservation
        {
            Id = reservationId,
            CustomerId = userId,
            GuestCount = 2,
            TableId = "table-1", WaiterId = "waiter-1",
            StartTime = DateTime.UtcNow,
            EndTime = DateTime.UtcNow.AddHours(2),
            Status = ReservationStatus.Reserved
        };

        var order = new Order
        {
            OrderId = "order-1",
            ReservationId = reservationId,
            CustomerId = userId,
            ReservationAddress = "addr",
            ReservationTime = "time",
            Dishes = [],
            Status = OrderStatus.Created
        };

        var session = new GuestSession
        {
            Id = "session-1",
            GroupReservationId = groupReservationId,
            GuestName = "Guest 1",
            IsSubmitted = true,
            Dishes = hasZeroQuantityDishes
                ? [new OrderItem { DishId = "dish1", Quantity = 0, State = OrderItemState.Pending }]
                : []
        };

        _groupReservationRepositoryMock
            .Setup(r => r.GetGroupReservationByIdAsync(groupReservationId))
            .ReturnsAsync(groupReservation);

        _reservationRepositoryMock
            .Setup(r => r.GetReservationByIdAsync(reservationId))
            .ReturnsAsync(reservation);

        _orderRepositoryMock
            .Setup(r => r.GetOrderByReservationIdAsync(reservationId))
            .ReturnsAsync(order);

        _guestSessionRepositoryMock
            .Setup(r => r.GetAllByGroupReservationIdAsync(groupReservationId))
            .ReturnsAsync([session]);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(
            () => _service.SubmitConsolidatedPreOrderAsync(userId, groupReservationId, reservationId));
        Assert.Equal("Submitted guest sessions contain no dishes.", ex.Message);
    }

    [Theory]
    [InlineData(ReservationStatus.Cancelled)]
    [InlineData(ReservationStatus.InProgress)]
    [InlineData(ReservationStatus.Completed)]
    public async Task SubmitConsolidatedPreOrderAsync_ReservationStatusNotReserved_ThrowsValidationException(ReservationStatus status)
    {
        // Arrange
        const string groupReservationId = "group-1";
        const string reservationId = "res-1";
        const string userId = "user-1";

        var groupReservation = new GroupReservation
        {
            GroupReservationId = groupReservationId,
            ReservationId = reservationId,
            ReservationToken = "token-1",
            Status = GroupReservationStatus.Open
        };

        var reservation = new Reservation
        {
            Id = reservationId,
            CustomerId = userId,
            GuestCount = 2,
            TableId = "table-1",
            WaiterId = "waiter-1",
            StartTime = DateTime.UtcNow,
            EndTime = DateTime.UtcNow.AddHours(2),
            Status = status
        };

        _groupReservationRepositoryMock
            .Setup(r => r.GetGroupReservationByIdAsync(groupReservationId))
            .ReturnsAsync(groupReservation);

        _reservationRepositoryMock
            .Setup(r => r.GetReservationByIdAsync(reservationId))
            .ReturnsAsync(reservation);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(
            () => _service.SubmitConsolidatedPreOrderAsync(userId, groupReservationId, reservationId));
        Assert.Contains("Pre-order can only be submitted for reservations in 'Reserved' status", ex.Message);
    }
}