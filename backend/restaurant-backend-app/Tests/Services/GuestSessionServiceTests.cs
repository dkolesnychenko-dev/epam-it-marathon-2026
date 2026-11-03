using Amazon.CognitoIdentityProvider.Model;
using APIHandler.Constants.Dishes;
using APIHandler.Constants.Orders;
using APIHandler.Entities;
using APIHandler.Exceptions;
using APIHandler.Models.GuestSessions;
using APIHandler.Repositories.Auth;
using APIHandler.Repositories.Dishes;
using APIHandler.Repositories.GroupReservations;
using APIHandler.Repositories.GuestSessions;
using APIHandler.Repositories.Orders;
using APIHandler.Repositories.Reservations;
using APIHandler.Repositories.Tables;
using APIHandler.Services.GuestSessions;
using AutoFixture.Xunit2;
using Moq;
using NPOI.POIFS.Storage;
using System.Security.Cryptography;
using System.Text;

namespace APIHandler.Tests.Services;

public class GuestSessionServiceTests
{
    private readonly Mock<IGuestSessionRepository> _mockGuestSessionRepository = new();
    private readonly Mock<IGroupReservationRepository> _mockGroupReservationRepository = new();
    private readonly Mock<IDishRepository> _mockDishRepository = new();
    private readonly Mock<IReservationRepository> _mockReservationRepository = new();
    private readonly Mock<IUserRepository> _mockUserRepository = new();
    private readonly Mock<ITableRepository> _mockTableRepository = new();
    private readonly Mock<IOrderRepository> _mockOrderRepository = new();
    private readonly GuestSessionService _guestSessionService;

    public GuestSessionServiceTests()
    {
        _guestSessionService = new GuestSessionService(
            _mockGroupReservationRepository.Object,
            _mockGuestSessionRepository.Object,
            _mockDishRepository.Object,
            _mockReservationRepository.Object,
            _mockUserRepository.Object,
            _mockTableRepository.Object,
            _mockOrderRepository.Object);
    }
    [Theory]
    [InlineAutoData(0)]
    [InlineAutoData(3)]
    public async Task CreateGuestSessionAsync_CorrectData_ReturnsSessionToken(
        int guestCount,
        CreateGuestSessionRequest request,
        GroupReservation groupReservation,
        Reservation parentReservation)
    {
        groupReservation.GroupReservationId = request.GroupReservationId;
        groupReservation.Status = GroupReservationStatus.Open;
        groupReservation.ReservationId = parentReservation.Id;

        parentReservation.GuestCount = 5;

        _mockGroupReservationRepository
            .Setup(r => r.GetGroupReservationByIdAsync(groupReservation.GroupReservationId))
            .ReturnsAsync(groupReservation);

        _mockReservationRepository
            .Setup(r => r.GetReservationByIdAsync(parentReservation.Id))
            .ReturnsAsync(parentReservation);

        _mockGuestSessionRepository
            .Setup(r => r.GetAllByGroupReservationIdAsync(groupReservation.GroupReservationId))
            .ReturnsAsync([]);

        GuestSession? session = null;
        _mockGuestSessionRepository
            .Setup(r => r.SaveAsync(It.IsAny<GuestSession>()))
            .Callback<GuestSession>(s => session = s);

        _mockGuestSessionRepository
            .Setup(r => r.CountByGroupReservationId(groupReservation.GroupReservationId))
            .ReturnsAsync(guestCount);

        var response = await _guestSessionService.CreateGuestSessionAsync(request);

        Assert.NotEmpty(response.SessionId);
        Assert.NotEmpty(response.SessionToken);
        Assert.NotNull(session);
        Assert.Equal(groupReservation.GroupReservationId, session.GroupReservationId);
        Assert.Equal(request.GuestName, session.GuestName);
        Assert.NotNull(session.SessionTokenHash);
        Assert.NotEmpty(session.SessionTokenHash);
    }

    [Theory]
    [AutoData]
    public async Task CreateGuestSessionAsync_InvalidGroupReservationId_ThrowsGroupReservationNotFoundException(CreateGuestSessionRequest request)
    {
        _mockGroupReservationRepository
            .Setup(r => r.GetGroupReservationByIdAsync(request.GroupReservationId))
            .ReturnsAsync((GroupReservation?)null);

        await Assert.ThrowsAsync<GroupReservationNotFoundException>(() => _guestSessionService.CreateGuestSessionAsync(request));
    }

    [Theory]
    [AutoData]
    public async Task CreateGuestSessionAsync_GroupReservationExpired_ThrowsGroupReservationExpiredException(CreateGuestSessionRequest request, GroupReservation groupReservation)
    {
        groupReservation.GroupReservationId = request.GroupReservationId;
        groupReservation.Status = GroupReservationStatus.Open;

        _mockGroupReservationRepository
            .Setup(r => r.GetGroupReservationByIdAsync(request.GroupReservationId))
            .ReturnsAsync(groupReservation);

        await Assert.ThrowsAsync<GroupReservationExpiredException>(() => _guestSessionService.CreateGuestSessionAsync(request));
    }

    [Theory]
    [AutoData]
    public async Task AddDishAsync_ShouldAddNewDish_WhenDishDoesNotExistInSession(GuestSession guestSession, GroupReservation groupReservation, Reservation parentReservation, Table table, Dish dish)
    {
        guestSession.Dishes = [];
        guestSession.GroupReservationId = groupReservation.GroupReservationId;
        groupReservation.Status = GroupReservationStatus.Open;

        groupReservation.ReservationId = parentReservation.Id;
        parentReservation.TableId = table.Id;
        dish.State = DishState.Available;
        int quantityToAdd = 2;

        _mockGuestSessionRepository.Setup(r => r.GetByGuestSessionId(guestSession.Id)).ReturnsAsync(guestSession);
        _mockGroupReservationRepository.Setup(r => r.GetGroupReservationByIdAsync(groupReservation.GroupReservationId)).ReturnsAsync(groupReservation);
        _mockReservationRepository.Setup(r => r.GetReservationByIdAsync(parentReservation.Id)).ReturnsAsync(parentReservation);
        _mockTableRepository.Setup(r => r.GetTableAsync(table.Id)).ReturnsAsync(table);
        _mockDishRepository.Setup(r => r.GetDishesByLocationIdAsync(table.LocationId)).ReturnsAsync([dish]);

        await _guestSessionService.AddDishAsync(guestSession.Id, dish.Id, quantityToAdd);

        Assert.Single(guestSession.Dishes);
        Assert.Equal(dish.Id, guestSession.Dishes[0].DishId);
        Assert.Equal(quantityToAdd, guestSession.Dishes[0].Quantity);
        Assert.Equal(OrderItemState.Pending, guestSession.Dishes[0].State);

        _mockGuestSessionRepository.Verify(r => r.SaveAsync(guestSession), Times.Once);
    }

    [Theory]
    [AutoData]
    public async Task AddDishAsync_ShouldIncreaseQuantity_WhenDishAlreadyExists(GuestSession guestSession, GroupReservation groupReservation, Reservation parentReservation, Table table, Dish dish)
    {
        int initialQuantity = 3;
        int quantityToAdd = 2;
        guestSession.Dishes = [new OrderItem { DishId = dish.Id, Quantity = initialQuantity, State = OrderItemState.Pending }];
        guestSession.GroupReservationId = groupReservation.GroupReservationId;
        groupReservation.Status = GroupReservationStatus.Open;

        groupReservation.ReservationId = parentReservation.Id;
        parentReservation.TableId = table.Id;
        dish.State = DishState.Available;

        _mockGuestSessionRepository.Setup(r => r.GetByGuestSessionId(guestSession.Id)).ReturnsAsync(guestSession);
        _mockGroupReservationRepository.Setup(r => r.GetGroupReservationByIdAsync(groupReservation.GroupReservationId)).ReturnsAsync(groupReservation);
        _mockReservationRepository.Setup(r => r.GetReservationByIdAsync(parentReservation.Id)).ReturnsAsync(parentReservation);
        _mockTableRepository.Setup(r => r.GetTableAsync(table.Id)).ReturnsAsync(table);
        _mockDishRepository.Setup(r => r.GetDishesByLocationIdAsync(table.LocationId)).ReturnsAsync([dish]);

        await _guestSessionService.AddDishAsync(guestSession.Id, dish.Id, quantityToAdd);

        Assert.Single(guestSession.Dishes);
        Assert.Equal(initialQuantity + quantityToAdd, guestSession.Dishes[0].Quantity);

        _mockGuestSessionRepository.Verify(r => r.SaveAsync(guestSession), Times.Once);
    }

    [Theory]
    [InlineAutoData(0)]
    [InlineAutoData(-1)]
    public async Task AddDishAsync_ShouldThrowValidationException_WhenQuantityIsZeroOrLess(int quantity, string guestSessionId, Dish dish)
    {
        dish.State = DishState.Available;

        _mockDishRepository
            .Setup(r => r.GetDishByIdAsync(dish.Id))
            .ReturnsAsync(dish);

        await Assert.ThrowsAsync<ValidationException>(() =>
            _guestSessionService.AddDishAsync(guestSessionId, dish.Id, quantity));
    }

    [Theory]
    [AutoData]
    public async Task AddDishAsync_ShouldThrowGuestSessionNotFoundException_WhenSessionNotFound(string guestSessionId, Dish dish)
    {
        dish.State = DishState.Available;

        _mockGuestSessionRepository
            .Setup(r => r.GetByGuestSessionId(guestSessionId))
            .ReturnsAsync((GuestSession?)null);

        _mockDishRepository
            .Setup(r => r.GetDishByIdAsync(dish.Id))
            .ReturnsAsync(dish);

        var exception = await Assert.ThrowsAsync<GuestSessionNotFoundException>(() =>
            _guestSessionService.AddDishAsync(guestSessionId, dish.Id, 2));
    }

    [Theory]
    [AutoData]
    public async Task AddDishAsync_ShouldThrowDishNotFound_WhenDishNotFound(GuestSession guestSession, GroupReservation groupReservation, Reservation parentReservation, Table table, string dishId)
    {
        guestSession.GroupReservationId = groupReservation.GroupReservationId;
        groupReservation.Status = GroupReservationStatus.Open;
        groupReservation.ReservationId = parentReservation.Id;
        parentReservation.TableId = table.Id;

        _mockGuestSessionRepository.Setup(r => r.GetByGuestSessionId(guestSession.Id)).ReturnsAsync(guestSession);
        _mockGroupReservationRepository.Setup(r => r.GetGroupReservationByIdAsync(groupReservation.GroupReservationId)).ReturnsAsync(groupReservation);
        _mockReservationRepository.Setup(r => r.GetReservationByIdAsync(parentReservation.Id)).ReturnsAsync(parentReservation);
        _mockTableRepository.Setup(r => r.GetTableAsync(table.Id)).ReturnsAsync(table);

        _mockDishRepository.Setup(r => r.GetDishesByLocationIdAsync(table.LocationId)).ReturnsAsync([]);

        var exception = await Assert.ThrowsAsync<ValidationException>(() =>
            _guestSessionService.AddDishAsync(guestSession.Id, dishId, quantity: 2));
    }

    [Theory]
    [AutoData]
    public async Task AddDishAsync_ShouldThrowDishNotAvailable_WhenDishIsNotAvailable(GuestSession guestSession, GroupReservation groupReservation, Reservation parentReservation, Table table, Dish dish)
    {
        dish.State = DishState.OutOfStock;
        guestSession.GroupReservationId = groupReservation.GroupReservationId;
        groupReservation.Status = GroupReservationStatus.Open;
        groupReservation.ReservationId = parentReservation.Id;
        parentReservation.TableId = table.Id;

        _mockGuestSessionRepository.Setup(r => r.GetByGuestSessionId(guestSession.Id)).ReturnsAsync(guestSession);
        _mockGroupReservationRepository.Setup(r => r.GetGroupReservationByIdAsync(groupReservation.GroupReservationId)).ReturnsAsync(groupReservation);
        _mockReservationRepository.Setup(r => r.GetReservationByIdAsync(parentReservation.Id)).ReturnsAsync(parentReservation);
        _mockTableRepository.Setup(r => r.GetTableAsync(table.Id)).ReturnsAsync(table);
        _mockDishRepository.Setup(r => r.GetDishesByLocationIdAsync(table.LocationId)).ReturnsAsync([dish]);

        var exception = await Assert.ThrowsAsync<DishIsNotAvailableException>(() =>
            _guestSessionService.AddDishAsync(guestSession.Id, dish.Id, quantity: 2));
    }

    [Theory]
    [InlineAutoData(GroupReservationStatus.Submitted, typeof(GroupReservationSubmittedException))]
    [InlineAutoData(GroupReservationStatus.Closed, typeof(GroupReservationExpiredException))]
    public async Task AddDishAsync_ShouldThrowGroupReservationExpired_WhenGroupStatusIsNotOpen(GroupReservationStatus status, Type exceptionType, GuestSession guestSession, GroupReservation groupReservation, Dish dish)
    {
        dish.State = DishState.Available;
        guestSession.GroupReservationId = groupReservation.GroupReservationId;
        groupReservation.Status = status;

        _mockGuestSessionRepository
            .Setup(r => r.GetByGuestSessionId(guestSession.Id))
            .ReturnsAsync(guestSession);

        _mockDishRepository
            .Setup(r => r.GetDishByIdAsync(dish.Id))
            .ReturnsAsync(dish);

        _mockGroupReservationRepository
            .Setup(r => r.GetGroupReservationByIdAsync(groupReservation.GroupReservationId))
            .ReturnsAsync(groupReservation);

        var exception = await Assert.ThrowsAsync(exceptionType, () =>
            _guestSessionService.AddDishAsync(guestSession.Id, dish.Id, quantity: 2));
    }

    [Theory]
    [AutoData]
    public async Task RemoveDishAsync_ShouldDecreaseQuantity_WhenQuantityIsGreaterThanAmountToRemove(GuestSession guestSession, GroupReservation groupReservation, string dishId)
    {
        int initialQuantity = 5;
        int quantityToRemove = 2;
        guestSession.Dishes =
        [
            new OrderItem { DishId = dishId, Quantity = initialQuantity, State = OrderItemState.Pending }
        ];
        guestSession.GroupReservationId = groupReservation.GroupReservationId;
        groupReservation.Status = GroupReservationStatus.Open;

        _mockGuestSessionRepository
            .Setup(r => r.GetByGuestSessionId(guestSession.Id))
            .ReturnsAsync(guestSession);


        _mockGroupReservationRepository
            .Setup(r => r.GetGroupReservationByIdAsync(groupReservation.GroupReservationId))
            .ReturnsAsync(groupReservation);

        await _guestSessionService.RemoveDishAsync(guestSession.Id, dishId, quantityToRemove);

        Assert.Single(guestSession.Dishes);
        Assert.Equal(initialQuantity - quantityToRemove, guestSession.Dishes[0].Quantity);

        _mockGuestSessionRepository.Verify(r => r.SaveAsync(guestSession), Times.Once);
    }

    [Theory]
    [AutoData]
    public async Task RemoveDishAsync_ShouldRemoveDishFromList_WhenQuantityToRemoveIsEqualOrGreater(GuestSession guestSession, GroupReservation groupReservation, string dishId)
    {
        int initialQuantity = 2;
        int quantityToRemove = 3;
        guestSession.Dishes =
        [
            new OrderItem { DishId = dishId, Quantity = initialQuantity, State = OrderItemState.Pending }
        ];
        guestSession.GroupReservationId = groupReservation.GroupReservationId;
        groupReservation.Status = GroupReservationStatus.Open;

        _mockGuestSessionRepository
            .Setup(r => r.GetByGuestSessionId(guestSession.Id))
            .ReturnsAsync(guestSession);

        _mockGroupReservationRepository
            .Setup(r => r.GetGroupReservationByIdAsync(groupReservation.GroupReservationId))
            .ReturnsAsync(groupReservation);

        await _guestSessionService.RemoveDishAsync(guestSession.Id, dishId, quantityToRemove);

        Assert.Empty(guestSession.Dishes);
        _mockGuestSessionRepository.Verify(r => r.SaveAsync(guestSession), Times.Once);
    }

    [Theory]
    [AutoData]
    public async Task RemoveDishAsync_ShouldThrowDishNotFoundException_WhenDishDoesNotExistInSession(GuestSession guestSession, GroupReservation groupReservation, string dishId)
    {
        guestSession.Dishes = [];
        guestSession.GroupReservationId = groupReservation.GroupReservationId;
        groupReservation.Status = GroupReservationStatus.Open;

        _mockGuestSessionRepository
            .Setup(r => r.GetByGuestSessionId(guestSession.Id))
            .ReturnsAsync(guestSession);

        _mockGroupReservationRepository
           .Setup(r => r.GetGroupReservationByIdAsync(groupReservation.GroupReservationId))
           .ReturnsAsync(groupReservation);

        var exception = await Assert.ThrowsAsync<DishNotFoundException>(() =>
            _guestSessionService.RemoveDishAsync(guestSession.Id, dishId, 2));

        Assert.Contains("not found in guest session", exception.Message);
    }

    [Theory]
    [AutoData]
    public async Task RemoveDishAsync_ShouldThrowValidationException_WhenQuantityIsZeroOrLess(string guestSessionId, string dishId)
    {
        await Assert.ThrowsAsync<ValidationException>(() =>
            _guestSessionService.RemoveDishAsync(guestSessionId, dishId, 0));

        await Assert.ThrowsAsync<ValidationException>(() =>
            _guestSessionService.RemoveDishAsync(guestSessionId, dishId, -5));
    }

    [Theory]
    [AutoData]
    public async Task RemoveDishAsync_ShouldThrowGuestSessionNotFoundException_WhenSessionNotFound(string guestSessionId, string dishId)
    {
        _mockGuestSessionRepository
            .Setup(r => r.GetByGuestSessionId(guestSessionId))
            .ReturnsAsync((GuestSession?)null);

        var exception = await Assert.ThrowsAsync<GuestSessionNotFoundException>(() =>
            _guestSessionService.RemoveDishAsync(guestSessionId, dishId, 2));
    }
    [Theory]
    [InlineAutoData(1, 0)]
    [InlineAutoData(1, 1)]
    [InlineAutoData(2, 1)]
    [InlineAutoData(2, 2)]
    public async Task CreateGuestSessionAsync_WhenCapacityReached_ThrowsGuestLimitExceededException(
        int reservationCapacity,
        int guestCount,
        CreateGuestSessionRequest request,
        GroupReservation groupReservation,
        Reservation parentReservation)
    {
        groupReservation.Status = GroupReservationStatus.Open;
        groupReservation.GroupReservationId = request.GroupReservationId;
        groupReservation.ReservationId = parentReservation.Id;

        parentReservation.GuestCount = reservationCapacity;

        _mockGroupReservationRepository
            .Setup(r => r.GetGroupReservationByIdAsync(groupReservation.GroupReservationId))
            .ReturnsAsync(groupReservation);

        _mockReservationRepository
            .Setup(r => r.GetReservationByIdAsync(parentReservation.Id))
            .ReturnsAsync(parentReservation);

        _mockGuestSessionRepository
            .Setup(r => r.CountByGroupReservationId(groupReservation.GroupReservationId))
            .ReturnsAsync(guestCount);

        var exception = await Assert.ThrowsAsync<GuestLimitExceededException>(() => _guestSessionService.CreateGuestSessionAsync(request));

        _mockGuestSessionRepository.Verify(r => r.SaveAsync(It.IsAny<GuestSession>()), Times.Never);
    }

    [Theory]
    [AutoData]
    public async Task CreateGuestSessionAsync_MissingParentReservation_ThrowsGroupReservationExpiredException(
        CreateGuestSessionRequest request,
        GroupReservation groupReservation)
    {
        groupReservation.GroupReservationId = request.GroupReservationId;
        groupReservation.Status = GroupReservationStatus.Open;

        _mockGroupReservationRepository
            .Setup(r => r.GetGroupReservationByIdAsync(groupReservation.GroupReservationId))
            .ReturnsAsync(groupReservation);

        _mockReservationRepository
            .Setup(r => r.GetReservationByIdAsync(groupReservation.ReservationId))
            .ReturnsAsync((Reservation?)null);

        var exception = await Assert.ThrowsAsync<GroupReservationExpiredException>(() => _guestSessionService.CreateGuestSessionAsync(request));
    }

    [Theory]
    [AutoData]
    public async Task IsAuthorizedAsync_ValidTokenAndId_ReturnsTrue(GuestSession guestSession, byte[] tokenBytes)
    {
        string tokenString = Convert.ToBase64String(tokenBytes);
        var hashString = Convert.ToBase64String(SHA256.HashData(tokenBytes));
        guestSession.SessionTokenHash = hashString;

        _mockGuestSessionRepository
            .Setup(r => r.GetByTokenHash(hashString))
            .ReturnsAsync(guestSession);

        bool result = await _guestSessionService.IsAuthorizedAsync(tokenString, guestSession.Id);

        Assert.True(result);
    }

    [Theory]
    [AutoData]
    public async Task IsAuthorizedAsync_InvalidToken_ReturnsFalse(byte[] tokenBytes, string guestSessionId)
    {
        string tokenString = Convert.ToBase64String(tokenBytes);
        var hashString = Convert.ToBase64String(SHA256.HashData(tokenBytes));

        _mockGuestSessionRepository
            .Setup(r => r.GetByTokenHash(hashString))
            .ReturnsAsync((GuestSession?)null);

        bool result = await _guestSessionService.IsAuthorizedAsync(tokenString, guestSessionId);

        Assert.False(result);
    }

    [Theory]
    [AutoData]
    public async Task IsAuthorizedAsync_InvalidGuestSessionId_ReturnsFalse(GuestSession guestSession, byte[] tokenBytes, string guestSessionId)
    {
        string tokenString = Convert.ToBase64String(tokenBytes);
        var hashString = Convert.ToBase64String(SHA256.HashData(tokenBytes));
        guestSession.SessionTokenHash = hashString;

        _mockGuestSessionRepository
            .Setup(r => r.GetByTokenHash(hashString))
            .ReturnsAsync(guestSession);

        bool result = await _guestSessionService.IsAuthorizedAsync(tokenString, guestSessionId);

        Assert.False(result);
    }
    [Theory]
    [AutoData]
    public async Task SubmitCartAsync_Valid_SubmitsSuccessfuilly(GuestSession guestSession, GroupReservation groupReservation, Dish[] dishes)
    {
        foreach (var dish in dishes)
            dish.State = DishState.Available;

        guestSession.IsSubmitted = false;
        guestSession.GroupReservationId = groupReservation.GroupReservationId;

        _mockGuestSessionRepository
            .Setup(r => r.GetByGuestSessionId(guestSession.Id))
            .ReturnsAsync(guestSession);

        _mockGroupReservationRepository
           .Setup(r => r.GetGroupReservationByIdAsync(guestSession.GroupReservationId))
           .ReturnsAsync(groupReservation);

        _mockDishRepository
            .Setup(d => d.GetDishesByIdsAsync(It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync(dishes);

        GuestSession? session = null;
        _mockGuestSessionRepository
            .Setup(r => r.SaveAsync(It.IsAny<GuestSession>()))
            .Callback<GuestSession>(s => session = s);

        await _guestSessionService.SubmitCartAsync(guestSession.Id);

        Assert.NotNull(session);
        Assert.True(session.IsSubmitted);
    }

    [Theory]
    [AutoData]
    public async Task SubmitCartAsync_SessionNotFound_ThrowsGuestSessionNotFound(string guestSessionId)
    {
        _mockGuestSessionRepository
            .Setup(r => r.GetByGuestSessionId(guestSessionId))
            .ReturnsAsync((GuestSession?)null);

        await Assert.ThrowsAsync<GuestSessionNotFoundException>(() => _guestSessionService.SubmitCartAsync(guestSessionId));
    }

    [Theory]
    [InlineAutoData(GroupReservationStatus.Submitted, typeof(GroupReservationSubmittedException))]
    [InlineAutoData(GroupReservationStatus.Closed, typeof(GroupReservationExpiredException))]
    public async Task SubmitCartAsync_GroupReservationNotOpen_ThrowsException(GroupReservationStatus status, Type exceptionType, GuestSession guestSession, GroupReservation groupReservation)
    {
        guestSession.GroupReservationId = groupReservation.GroupReservationId;
        groupReservation.Status = status;

        _mockGuestSessionRepository
            .Setup(r => r.GetByGuestSessionId(guestSession.Id))
            .ReturnsAsync(guestSession);

        _mockGroupReservationRepository
            .Setup(r => r.GetGroupReservationByIdAsync(guestSession.GroupReservationId))
            .ReturnsAsync(groupReservation);

        await Assert.ThrowsAsync(exceptionType, () => _guestSessionService.SubmitCartAsync(guestSession.Id));
    }

    [Theory]
    [AutoData]
    public async Task SubmitCartAsync_GuestSessionHasNoDishes_ThrowsCartIsEmptyException(GuestSession guestSession, GroupReservation groupReservation)
    {
        guestSession.Dishes = [];
        guestSession.GroupReservationId = groupReservation.GroupReservationId;
        groupReservation.Status = GroupReservationStatus.Open;

        _mockGuestSessionRepository
            .Setup(r => r.GetByGuestSessionId(guestSession.Id))
            .ReturnsAsync(guestSession);

        _mockGroupReservationRepository
            .Setup(r => r.GetGroupReservationByIdAsync(guestSession.GroupReservationId))
            .ReturnsAsync(groupReservation);

        await Assert.ThrowsAsync<CartIsEmptyException>(() => _guestSessionService.SubmitCartAsync(guestSession.Id));
    }

    [Theory]
    [AutoData]
    public async Task SubmitCartAsync_GuestSessionSubmitted_ThrowsAlreadySubmited(GuestSession guestSession, GroupReservation groupReservation)
    {
        guestSession.IsSubmitted = true;
        guestSession.GroupReservationId = groupReservation.GroupReservationId;
        groupReservation.Status = GroupReservationStatus.Open;

        _mockGuestSessionRepository
            .Setup(r => r.GetByGuestSessionId(guestSession.Id))
            .ReturnsAsync(guestSession);

        _mockGroupReservationRepository
            .Setup(r => r.GetGroupReservationByIdAsync(guestSession.GroupReservationId))
            .ReturnsAsync(groupReservation);

        await Assert.ThrowsAsync<GuestSessionSubmittedException>(() => _guestSessionService.SubmitCartAsync(guestSession.Id));
    }

    [Theory]
    [InlineAutoData(DishState.OutOfStock)]
    [InlineAutoData(DishState.OnStop)]
    public async Task SubmitCartAsync_DishNotAvailable_ThrowsDishNotAvailable(DishState state, Dish dish, GuestSession guestSession, GroupReservation groupReservation)
    {
        dish.State = state;
        guestSession.IsSubmitted = false;
        guestSession.GroupReservationId = groupReservation.GroupReservationId;
        guestSession.Dishes = 
        [
            new OrderItem()
            {
                DishId = dish.Id,
                Quantity = 1,
            }
        ];

        _mockDishRepository
            .Setup(d => d.GetDishesByIdsAsync(It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync([dish]);

        _mockGuestSessionRepository
            .Setup(r => r.GetByGuestSessionId(guestSession.Id))
            .ReturnsAsync(guestSession);

        _mockGroupReservationRepository
           .Setup(r => r.GetGroupReservationByIdAsync(guestSession.GroupReservationId))
           .ReturnsAsync(groupReservation);

        await Assert.ThrowsAsync<DishIsNotAvailableException>(() => _guestSessionService.SubmitCartAsync(guestSession.Id));
    }
    [Theory]
    [MemberAutoData(nameof(PepareDishesForGuestSession))]
    public async Task GetGuestSessionInfoAsync_Guest_ReturnsAsync(
        IEnumerable<string> dishIds,
        IEnumerable<Dish> dishes,
        List<OrderItem> orderItems,
        decimal expectedTotal,
        GuestSession session)
    {
        session.Dishes = orderItems;
        session.IsOwnerSession = false;

        _mockDishRepository
            .Setup(r => r.GetDishesByIdsAsync(dishIds))
            .ReturnsAsync(dishes);

        _mockGuestSessionRepository
            .Setup(r => r.GetByGuestSessionId(session.Id))
            .ReturnsAsync(session);

        GetGuestSessionResponse result = await _guestSessionService.GetGuestSessionInfoAsync(session.Id);

        Assert.Equal(session.GuestName, result.Name);
        Assert.Equal(dishes.Count(), result.Items.Count());
        Assert.Equal(expectedTotal, result.Total);
    }

    [Theory]
    [MemberAutoData(nameof(PepareDishesForGuestSession))]
    public async Task GetGuestSessionInfoAsync_GroupOwner_ReturnsAsync(
        IEnumerable<string> dishIds,
        IEnumerable<Dish> dishes,
        List<OrderItem> orderItems,
        decimal expectedTotal,
        GuestSession session,
        GroupReservation groupReservation,
        Reservation reservation,
        string customerId,
        string ownerName,
        UserType userType)
    {
        session.Dishes = orderItems;
        session.IsOwnerSession = true;
        session.GroupReservationId = groupReservation.GroupReservationId;
        groupReservation.ReservationId = reservation.Id;
        reservation.CustomerId = customerId;
        userType.Attributes.Add(new AttributeType()
        {
            Name = "given_name",
            Value = ownerName,
        });

        _mockDishRepository
            .Setup(r => r.GetDishesByIdsAsync(dishIds))
            .ReturnsAsync(dishes);

        _mockGuestSessionRepository
            .Setup(r => r.GetByGuestSessionId(session.Id))
            .ReturnsAsync(session);

        _mockGroupReservationRepository
            .Setup(r => r.GetGroupReservationByIdAsync(groupReservation.GroupReservationId))
            .ReturnsAsync(groupReservation);

        _mockReservationRepository
            .Setup(r => r.GetReservationByIdAsync(reservation.Id))
            .ReturnsAsync(reservation);

        _mockUserRepository
            .Setup(r => r.GetUserByIdAsync(customerId))
            .ReturnsAsync(userType);

        GetGuestSessionResponse result = await _guestSessionService.GetGuestSessionInfoAsync(session.Id);

        Assert.Equal(ownerName, result.Name);
        Assert.Equal(dishes.Count(), result.Items.Count());
        Assert.Equal(expectedTotal, result.Total);
    }

    public static TheoryData<IEnumerable<string>, IEnumerable<Dish>, List<OrderItem>, decimal> PepareDishesForGuestSession()
    {
        var dishMap = new Dictionary<string, Dish>()
        {
            ["1"] = new()
            {
                Id = "1",
                Price = 1
            },
            ["2"] = new()
            {
                Id = "2",
                Price = 2
            },
            ["3"] = new()
            {
                Id = "3",
                Price = 3
            }
        };

        var orderItems = new List<OrderItem>
        {
            new OrderItem()
                    {
                        DishId = "1",
                        Quantity = 2
                    },
                    new OrderItem()
                    {
                        DishId = "2",
                        Quantity = 3
                    },
                    new OrderItem()
                    {
                        DishId = "3",
                        Quantity = 1
                    }
        };

        var expectedTotal = orderItems.Sum(o => dishMap[o.DishId].Price * o.Quantity);

        return new()
        {
            { dishMap.Keys, dishMap.Values, orderItems, expectedTotal }
        };
    }

        [Fact]
    public async Task GetGuestSessionsAsync_ValidOwnerAndSessionsExist_ReturnsParticipantsAndTotal()
    {
        // Arrange
        const string userId = "owner-user-123";
        const string groupReservationId = "group-res-456";
        const string reservationId = "res-789";

        var groupReservation = new GroupReservation
        {
            GroupReservationId = groupReservationId,
            ReservationId = reservationId,
            ReservationToken = "token-123",
            Status = GroupReservationStatus.Open
        };

        var reservation = new Reservation
        {
            Id = reservationId,
            TableId = "table-1",
            WaiterId = "waiter-1",
            CustomerId = userId
        };

        var dish1 = new Dish { Id = "dish-1", Name = "Pasta", Price = 12.50m };
        var dish2 = new Dish { Id = "dish-2", Name = "Salad", Price = 8.00m };
        var dish3 = new Dish { Id = "dish-3", Name = "Gelato", Price = 5.00m };
        var dishes = new List<Dish> { dish1, dish2, dish3 };

        var ownerSession = new GuestSession
        {
            Id = "session-owner",
            GroupReservationId = groupReservationId,
            IsOwnerSession = true,
            GuestName = "Owner",
            OwnerUserId = userId,
            Dishes = new List<OrderItem>
            {
                new() { DishId = "dish-1", Quantity = 2 },
                new() { DishId = "dish-2", Quantity = 1 }
            },
            IsSubmitted = true
        };

        var guestSession = new GuestSession
        {
            Id = "session-guest",
            GroupReservationId = groupReservationId,
            IsOwnerSession = false,
            GuestName = "Charlie",
            Dishes = new List<OrderItem>
            {
                new() { DishId = "dish-2", Quantity = 2 },
                new() { DishId = "dish-3", Quantity = 3 }
            },
            IsSubmitted = false
        };

        var ownerUser = new UserType
        {
            Username = userId,
            Attributes = new List<AttributeType>
            {
                new() { Name = "given_name", Value = "Alice" }
            }
        };

        _mockGroupReservationRepository
            .Setup(r => r.GetGroupReservationByIdAsync(groupReservationId))
            .ReturnsAsync(groupReservation);

        _mockReservationRepository
            .Setup(r => r.GetReservationByIdAsync(reservationId))
            .ReturnsAsync(reservation);

        _mockGuestSessionRepository
            .Setup(r => r.GetAllByGroupReservationIdAsync(groupReservationId))
            .ReturnsAsync(new List<GuestSession> { ownerSession, guestSession });

        _mockDishRepository
            .Setup(r => r.GetDishesByIdsAsync(It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync(dishes);

        _mockUserRepository
            .Setup(r => r.GetUserByIdAsync(userId))
            .ReturnsAsync(ownerUser);

        // Act
        var result = await _guestSessionService.GetGuestSessionsAsync(userId, groupReservationId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Participants.Count());

        var participantsList = result.Participants.ToList();

        // Verify Owner Participant
        var ownerParticipant = participantsList.First(p => p.Id == "session-owner");
        Assert.Equal(ParticipantType.Owner, ownerParticipant.Type);
        Assert.Equal("Alice", ownerParticipant.Name);
        Assert.True(ownerParticipant.IsSubmitted);
        Assert.Equal(2, ownerParticipant.Items.Count());
        Assert.Equal(33.00m, ownerParticipant.Total); // (2 * 12.50) + (1 * 8.00) = 33.00

        // Verify Guest Participant
        var guestParticipant = participantsList.First(p => p.Id == "session-guest");
        Assert.Equal(ParticipantType.Guest, guestParticipant.Type);
        Assert.Equal("Charlie", guestParticipant.Name);
        Assert.False(guestParticipant.IsSubmitted);
        Assert.Equal(2, guestParticipant.Items.Count());
        Assert.Equal(31.00m, guestParticipant.Total); // (2 * 8.00) + (3 * 5.00) = 31.00

        // Verify Overall Total
        Assert.Equal(64.00m, result.Total); // 33.00 + 31.00 = 64.00
    }

    [Fact]
    public async Task GetGuestSessionsAsync_EmptyDishes_ReturnsEmptyItemsAndZeroTotal()
    {
        // Arrange
        const string userId = "owner-user-123";
        const string groupReservationId = "group-res-456";
        const string reservationId = "res-789";

        var groupReservation = new GroupReservation
        {
            GroupReservationId = groupReservationId,
            ReservationId = reservationId,
            ReservationToken = "token-123",
            Status = GroupReservationStatus.Open
        };

        var reservation = new Reservation
        {
            Id = reservationId,
            TableId = "table-1",
            WaiterId = "waiter-1",
            CustomerId = userId
        };

        var ownerSession = new GuestSession
        {
            Id = "session-owner",
            GroupReservationId = groupReservationId,
            IsOwnerSession = true,
            GuestName = "Owner",
            OwnerUserId = userId,
            Dishes = new List<OrderItem>(),
            IsSubmitted = true
        };

        var guestSession = new GuestSession
        {
            Id = "session-guest",
            GroupReservationId = groupReservationId,
            IsOwnerSession = false,
            GuestName = "Charlie",
            Dishes = new List<OrderItem>(),
            IsSubmitted = false
        };

        var ownerUser = new UserType
        {
            Username = userId,
            Attributes = new List<AttributeType>
            {
                new() { Name = "given_name", Value = "Alice" }
            }
        };

        _mockGroupReservationRepository
            .Setup(r => r.GetGroupReservationByIdAsync(groupReservationId))
            .ReturnsAsync(groupReservation);

        _mockReservationRepository
            .Setup(r => r.GetReservationByIdAsync(reservationId))
            .ReturnsAsync(reservation);

        _mockGuestSessionRepository
            .Setup(r => r.GetAllByGroupReservationIdAsync(groupReservationId))
            .ReturnsAsync(new List<GuestSession> { ownerSession, guestSession });

        _mockDishRepository
            .Setup(r => r.GetDishesByIdsAsync(It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync(new List<Dish>());

        _mockUserRepository
            .Setup(r => r.GetUserByIdAsync(userId))
            .ReturnsAsync(ownerUser);

        // Act
        var result = await _guestSessionService.GetGuestSessionsAsync(userId, groupReservationId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Participants.Count());

        foreach (var participant in result.Participants)
        {
            Assert.Empty(participant.Items);
            Assert.Equal(0m, participant.Total);
        }

        Assert.Equal(0m, result.Total);
    }

    [Fact]
    public async Task GetGuestSessionsAsync_GroupReservationNotFound_ThrowsGroupReservationNotFoundException()
    {
        // Arrange
        const string userId = "owner-user-123";
        const string groupReservationId = "group-nonexistent";

        _mockGroupReservationRepository
            .Setup(r => r.GetGroupReservationByIdAsync(groupReservationId))
            .ReturnsAsync((GroupReservation?)null);

        // Act & Assert
        await Assert.ThrowsAsync<GroupReservationNotFoundException>(() =>
            _guestSessionService.GetGuestSessionsAsync(userId, groupReservationId));
    }

    [Fact]
    public async Task GetGuestSessionsAsync_ParentReservationNotFound_ThrowsReservationNotFoundException()
    {
        // Arrange
        const string userId = "owner-user-123";
        const string groupReservationId = "group-res-456";
        const string reservationId = "res-nonexistent";

        var groupReservation = new GroupReservation
        {
            GroupReservationId = groupReservationId,
            ReservationId = reservationId,
            ReservationToken = "token-123",
            Status = GroupReservationStatus.Open
        };

        _mockGroupReservationRepository
            .Setup(r => r.GetGroupReservationByIdAsync(groupReservationId))
            .ReturnsAsync(groupReservation);

        _mockReservationRepository
            .Setup(r => r.GetReservationByIdAsync(reservationId))
            .ReturnsAsync((Reservation?)null);

        // Act & Assert
        await Assert.ThrowsAsync<ReservationNotFoundException>(() =>
            _guestSessionService.GetGuestSessionsAsync(userId, groupReservationId));
    }

    [Fact]
    public async Task GetGuestSessionsAsync_CallerNotOwner_ThrowsActionForbiddenException()
    {
        // Arrange
        const string actualOwnerUserId = "actual-owner-123";
        const string callerUserId = "unauthorized-user-999";
        const string groupReservationId = "group-res-456";
        const string reservationId = "res-789";

        var groupReservation = new GroupReservation
        {
            GroupReservationId = groupReservationId,
            ReservationId = reservationId,
            ReservationToken = "token-123",
            Status = GroupReservationStatus.Open
        };

        var reservation = new Reservation
        {
            Id = reservationId,
            TableId = "table-1",
            WaiterId = "waiter-1",
            CustomerId = actualOwnerUserId
        };

        _mockGroupReservationRepository
            .Setup(r => r.GetGroupReservationByIdAsync(groupReservationId))
            .ReturnsAsync(groupReservation);

        _mockReservationRepository
            .Setup(r => r.GetReservationByIdAsync(reservationId))
            .ReturnsAsync(reservation);

        // Act & Assert
        await Assert.ThrowsAsync<ActionForbiddenException>(() =>
            _guestSessionService.GetGuestSessionsAsync(callerUserId, groupReservationId));
    }

    [Fact]
    public async Task GetOwnerSessionIdAsync_ExistingOwnerSession_ReturnsExistingSessionId()
    {
        // Arrange
        const string userId = "user-123";
        const string reservationId = "res-456";
        const string groupReservationId = "group-789";
        const string existingOwnerSessionId = "session-owner-1";

        var reservation = new Reservation
        {
            Id = reservationId,
            TableId = "table-1",
            WaiterId = "waiter-1",
            CustomerId = userId
        };

        var groupReservation = new GroupReservation
        {
            GroupReservationId = groupReservationId,
            ReservationId = reservationId,
            ReservationToken = "token-abc",
            Status = GroupReservationStatus.Open
        };

        var existingOwnerSession = new GuestSession
        {
            Id = existingOwnerSessionId,
            GroupReservationId = groupReservationId,
            GuestName = "Owner",
            OwnerUserId = userId,
            IsOwnerSession = true,
            Dishes = []
        };

        var guestSession = new GuestSession
        {
            Id = "session-guest-1",
            GroupReservationId = groupReservationId,
            GuestName = "Guest 1",
            IsOwnerSession = false,
            Dishes = []
        };

        _mockReservationRepository
            .Setup(r => r.GetReservationByIdAsync(reservationId))
            .ReturnsAsync(reservation);

        _mockGroupReservationRepository
            .Setup(r => r.GetByReservationIdAsync(reservationId))
            .ReturnsAsync(groupReservation);

        _mockGuestSessionRepository
            .Setup(r => r.GetAllByGroupReservationIdAsync(groupReservationId))
            .ReturnsAsync([existingOwnerSession, guestSession]);

        // Act
        var result = await _guestSessionService.GetOwnerSessionIdAsync(userId, reservationId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(existingOwnerSessionId, result.GuestSessionId);
        _mockGuestSessionRepository.Verify(r => r.SaveAsync(It.IsAny<GuestSession>()), Times.Never);
    }

    [Fact]
    public async Task GetOwnerSessionIdAsync_NoOwnerSessionYet_CreatesSavesAndReturnsNewSessionId()
    {
        // Arrange
        const string userId = "user-123";
        const string reservationId = "res-456";
        const string groupReservationId = "group-789";

        var reservation = new Reservation
        {
            Id = reservationId,
            TableId = "table-1",
            WaiterId = "waiter-1",
            CustomerId = userId
        };

        var groupReservation = new GroupReservation
        {
            GroupReservationId = groupReservationId,
            ReservationId = reservationId,
            ReservationToken = "token-abc",
            Status = GroupReservationStatus.Open
        };

        var guestSession = new GuestSession
        {
            Id = "session-guest-1",
            GroupReservationId = groupReservationId,
            GuestName = "Guest 1",
            IsOwnerSession = false,
            Dishes = []
        };

        _mockReservationRepository
            .Setup(r => r.GetReservationByIdAsync(reservationId))
            .ReturnsAsync(reservation);

        _mockGroupReservationRepository
            .Setup(r => r.GetByReservationIdAsync(reservationId))
            .ReturnsAsync(groupReservation);

        _mockGuestSessionRepository
            .Setup(r => r.GetAllByGroupReservationIdAsync(groupReservationId))
            .ReturnsAsync([guestSession]);

        GuestSession? savedSession = null;
        _mockGuestSessionRepository
            .Setup(r => r.SaveAsync(It.IsAny<GuestSession>()))
            .Callback<GuestSession>(s => savedSession = s)
            .Returns(Task.CompletedTask);

        // Act
        var result = await _guestSessionService.GetOwnerSessionIdAsync(userId, reservationId);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(savedSession);
        Assert.Equal(savedSession.Id, result.GuestSessionId);
        Assert.True(savedSession.IsOwnerSession);
        Assert.Equal(userId, savedSession.OwnerUserId);
        Assert.Equal(groupReservationId, savedSession.GroupReservationId);
        Assert.Equal("Owner", savedSession.GuestName);
        Assert.False(savedSession.IsSubmitted);
        Assert.Empty(savedSession.Dishes);

        _mockGuestSessionRepository.Verify(r => r.SaveAsync(It.Is<GuestSession>(s =>
            s.IsOwnerSession == true &&
            s.OwnerUserId == userId &&
            s.GroupReservationId == groupReservationId)), Times.Once);
    }

    [Fact]
    public async Task GetOwnerSessionIdAsync_ReservationNotFound_ThrowsReservationNotFoundException()
    {
        // Arrange
        const string userId = "user-123";
        const string reservationId = "res-nonexistent";

        _mockReservationRepository
            .Setup(r => r.GetReservationByIdAsync(reservationId))
            .ReturnsAsync((Reservation?)null);

        // Act & Assert
        await Assert.ThrowsAsync<ReservationNotFoundException>(() =>
            _guestSessionService.GetOwnerSessionIdAsync(userId, reservationId));

        _mockGuestSessionRepository.Verify(r => r.SaveAsync(It.IsAny<GuestSession>()), Times.Never);
    }

    [Fact]
    public async Task GetOwnerSessionIdAsync_CallerNotOwner_ThrowsActionForbiddenException()
    {
        // Arrange
        const string actualOwnerId = "actual-owner";
        const string callerUserId = "different-user";
        const string reservationId = "res-456";

        var reservation = new Reservation
        {
            Id = reservationId,
            TableId = "table-1",
            WaiterId = "waiter-1",
            CustomerId = actualOwnerId
        };

        _mockReservationRepository
            .Setup(r => r.GetReservationByIdAsync(reservationId))
            .ReturnsAsync(reservation);

        // Act & Assert
        await Assert.ThrowsAsync<ActionForbiddenException>(() =>
            _guestSessionService.GetOwnerSessionIdAsync(callerUserId, reservationId));

        _mockGroupReservationRepository.Verify(r => r.GetByReservationIdAsync(It.IsAny<string>()), Times.Never);
        _mockGuestSessionRepository.Verify(r => r.SaveAsync(It.IsAny<GuestSession>()), Times.Never);
    }

    [Fact]
    public async Task GetOwnerSessionIdAsync_GroupReservationNotFound_ThrowsGroupReservationNotFoundException()
    {
        // Arrange
        const string userId = "user-123";
        const string reservationId = "res-456";

        var reservation = new Reservation
        {
            Id = reservationId,
            TableId = "table-1",
            WaiterId = "waiter-1",
            CustomerId = userId
        };

        _mockReservationRepository
            .Setup(r => r.GetReservationByIdAsync(reservationId))
            .ReturnsAsync(reservation);

        _mockGroupReservationRepository
            .Setup(r => r.GetByReservationIdAsync(reservationId))
            .ReturnsAsync((GroupReservation?)null);

        // Act & Assert
        await Assert.ThrowsAsync<GroupReservationNotFoundException>(() =>
            _guestSessionService.GetOwnerSessionIdAsync(userId, reservationId));

        _mockGuestSessionRepository.Verify(r => r.GetAllByGroupReservationIdAsync(It.IsAny<string>()), Times.Never);
        _mockGuestSessionRepository.Verify(r => r.SaveAsync(It.IsAny<GuestSession>()), Times.Never);
    }
    [Fact]
    public async Task DeleteGuestSessionAsync_ValidGuestSession_DeletesAndReturnsUpdatedSnapshot()
    {
        // Arrange
        const string userId = "owner-user-123";
        const string groupReservationId = "group-123";
        const string guestSessionId = "session-guest-456";
        const string reservationId = "res-789";

        var groupReservation = new GroupReservation
        {
            GroupReservationId = groupReservationId,
            ReservationId = reservationId,
            ReservationToken = "token-123",
            Status = GroupReservationStatus.Open
        };

        var reservation = new Reservation
        {
            Id = reservationId,
            TableId = "table-1",
            WaiterId = "waiter-1",
            CustomerId = userId
        };

        var targetGuestSession = new GuestSession
        {
            Id = guestSessionId,
            GroupReservationId = groupReservationId,
            GuestName = "Bob",
            IsOwnerSession = false,
            Dishes = new List<OrderItem>
            {
                new() { DishId = "dish-1", Quantity = 2 }
            },
            IsSubmitted = false
        };

        var remainingOwnerSession = new GuestSession
        {
            Id = "session-owner",
            GroupReservationId = groupReservationId,
            GuestName = "Alice",
            OwnerUserId = userId,
            IsOwnerSession = true,
            Dishes = new List<OrderItem>
            {
                new() { DishId = "dish-2", Quantity = 1 }
            },
            IsSubmitted = true
        };

        var dish2 = new Dish { Id = "dish-2", Name = "Tom Yum", Price = 220m };

        _mockGroupReservationRepository
            .Setup(r => r.GetGroupReservationByIdAsync(groupReservationId))
            .ReturnsAsync(groupReservation);

        _mockReservationRepository
            .Setup(r => r.GetReservationByIdAsync(reservationId))
            .ReturnsAsync(reservation);

        _mockGuestSessionRepository
            .Setup(r => r.GetByGuestSessionId(guestSessionId))
            .ReturnsAsync(targetGuestSession);

        _mockGuestSessionRepository
            .Setup(r => r.DeleteAsync(targetGuestSession))
            .Returns(Task.CompletedTask);

        _mockGuestSessionRepository
            .Setup(r => r.GetAllByGroupReservationIdAsync(groupReservationId))
            .ReturnsAsync(new List<GuestSession> { remainingOwnerSession });

        _mockDishRepository
            .Setup(r => r.GetDishesByIdsAsync(It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync(new List<Dish> { dish2 });

        // Act
        var result = await _guestSessionService.DeleteGuestSessionAsync(userId, groupReservationId, guestSessionId);

        // Assert
        Assert.NotNull(result);
        _mockGuestSessionRepository.Verify(r => r.DeleteAsync(targetGuestSession), Times.Once);
        Assert.Single(result.Participants);

        var participant = result.Participants.First();
        Assert.Equal("session-owner", participant.Id);
        Assert.Equal(ParticipantType.Owner, participant.Type);
        Assert.Equal("Alice", participant.Name);
        Assert.True(participant.IsSubmitted);
        Assert.Single(participant.Items);
        Assert.Equal(220m, participant.Total);
        Assert.Equal(220m, result.Total);
    }

    [Fact]
    public async Task DeleteGuestSessionAsync_CallerNotOwner_ThrowsActionForbiddenException()
    {
        // Arrange
        const string actualOwnerUserId = "actual-owner-123";
        const string callerUserId = "different-user-456";
        const string groupReservationId = "group-123";
        const string guestSessionId = "session-guest-456";
        const string reservationId = "res-789";

        var groupReservation = new GroupReservation
        {
            GroupReservationId = groupReservationId,
            ReservationId = reservationId,
            ReservationToken = "token-123",
            Status = GroupReservationStatus.Open
        };

        var reservation = new Reservation
        {
            Id = reservationId,
            TableId = "table-1",
            WaiterId = "waiter-1",
            CustomerId = actualOwnerUserId
        };

        _mockGroupReservationRepository
            .Setup(r => r.GetGroupReservationByIdAsync(groupReservationId))
            .ReturnsAsync(groupReservation);

        _mockReservationRepository
            .Setup(r => r.GetReservationByIdAsync(reservationId))
            .ReturnsAsync(reservation);

        // Act & Assert
        await Assert.ThrowsAsync<ActionForbiddenException>(() =>
            _guestSessionService.DeleteGuestSessionAsync(callerUserId, groupReservationId, guestSessionId));

        _mockGuestSessionRepository.Verify(r => r.GetByGuestSessionId(It.IsAny<string>()), Times.Never);
        _mockGuestSessionRepository.Verify(r => r.DeleteAsync(It.IsAny<GuestSession>()), Times.Never);
    }

    [Fact]
    public async Task DeleteGuestSessionAsync_GroupReservationNotFound_ThrowsGroupReservationNotFoundException()
    {
        // Arrange
        const string userId = "owner-user-123";
        const string groupReservationId = "group-nonexistent";
        const string guestSessionId = "session-guest-456";

        _mockGroupReservationRepository
            .Setup(r => r.GetGroupReservationByIdAsync(groupReservationId))
            .ReturnsAsync((GroupReservation?)null);

        // Act & Assert
        await Assert.ThrowsAsync<GroupReservationNotFoundException>(() =>
            _guestSessionService.DeleteGuestSessionAsync(userId, groupReservationId, guestSessionId));

        _mockGuestSessionRepository.Verify(r => r.DeleteAsync(It.IsAny<GuestSession>()), Times.Never);
    }

    [Fact]
    public async Task DeleteGuestSessionAsync_ReservationNotFound_ThrowsReservationNotFoundException()
    {
        // Arrange
        const string userId = "owner-user-123";
        const string groupReservationId = "group-123";
        const string guestSessionId = "session-guest-456";
        const string reservationId = "res-nonexistent";

        var groupReservation = new GroupReservation
        {
            GroupReservationId = groupReservationId,
            ReservationId = reservationId,
            ReservationToken = "token-123",
            Status = GroupReservationStatus.Open
        };

        _mockGroupReservationRepository
            .Setup(r => r.GetGroupReservationByIdAsync(groupReservationId))
            .ReturnsAsync(groupReservation);

        _mockReservationRepository
            .Setup(r => r.GetReservationByIdAsync(reservationId))
            .ReturnsAsync((Reservation?)null);

        // Act & Assert
        await Assert.ThrowsAsync<ReservationNotFoundException>(() =>
            _guestSessionService.DeleteGuestSessionAsync(userId, groupReservationId, guestSessionId));

        _mockGuestSessionRepository.Verify(r => r.DeleteAsync(It.IsAny<GuestSession>()), Times.Never);
    }

    [Fact]
    public async Task DeleteGuestSessionAsync_GuestSessionNotFound_ThrowsGuestSessionNotFoundException()
    {
        // Arrange
        const string userId = "owner-user-123";
        const string groupReservationId = "group-123";
        const string guestSessionId = "session-nonexistent";
        const string reservationId = "res-789";

        var groupReservation = new GroupReservation
        {
            GroupReservationId = groupReservationId,
            ReservationId = reservationId,
            ReservationToken = "token-123",
            Status = GroupReservationStatus.Open
        };

        var reservation = new Reservation
        {
            Id = reservationId,
            TableId = "table-1",
            WaiterId = "waiter-1",
            CustomerId = userId
        };

        _mockGroupReservationRepository
            .Setup(r => r.GetGroupReservationByIdAsync(groupReservationId))
            .ReturnsAsync(groupReservation);

        _mockReservationRepository
            .Setup(r => r.GetReservationByIdAsync(reservationId))
            .ReturnsAsync(reservation);

        _mockGuestSessionRepository
            .Setup(r => r.GetByGuestSessionId(guestSessionId))
            .ReturnsAsync((GuestSession?)null);

        // Act & Assert
        await Assert.ThrowsAsync<GuestSessionNotFoundException>(() =>
            _guestSessionService.DeleteGuestSessionAsync(userId, groupReservationId, guestSessionId));

        _mockGuestSessionRepository.Verify(r => r.DeleteAsync(It.IsAny<GuestSession>()), Times.Never);
    }

    [Fact]
    public async Task DeleteGuestSessionAsync_SessionBelongsToDifferentGroup_ThrowsValidationException()
    {
        // Arrange
        const string userId = "owner-user-123";
        const string groupReservationId = "group-123";
        const string guestSessionId = "session-guest-456";
        const string reservationId = "res-789";

        var groupReservation = new GroupReservation
        {
            GroupReservationId = groupReservationId,
            ReservationId = reservationId,
            ReservationToken = "token-123",
            Status = GroupReservationStatus.Open
        };

        var reservation = new Reservation
        {
            Id = reservationId,
            TableId = "table-1",
            WaiterId = "waiter-1",
            CustomerId = userId
        };

        var sessionFromOtherGroup = new GuestSession
        {
            Id = guestSessionId,
            GroupReservationId = "different-group-id",
            GuestName = "Bob",
            Dishes = [],
            IsOwnerSession = false
        };

        _mockGroupReservationRepository
            .Setup(r => r.GetGroupReservationByIdAsync(groupReservationId))
            .ReturnsAsync(groupReservation);

        _mockReservationRepository
            .Setup(r => r.GetReservationByIdAsync(reservationId))
            .ReturnsAsync(reservation);

        _mockGuestSessionRepository
            .Setup(r => r.GetByGuestSessionId(guestSessionId))
            .ReturnsAsync(sessionFromOtherGroup);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            _guestSessionService.DeleteGuestSessionAsync(userId, groupReservationId, guestSessionId));

        Assert.Equal("Guest session does not belong to this group reservation.", ex.Message);
        _mockGuestSessionRepository.Verify(r => r.DeleteAsync(It.IsAny<GuestSession>()), Times.Never);
    }

    [Fact]
    public async Task DeleteGuestSessionAsync_TargetSessionIsOwner_ThrowsValidationException()
    {
        // Arrange
        const string userId = "owner-user-123";
        const string groupReservationId = "group-123";
        const string guestSessionId = "session-owner-456";
        const string reservationId = "res-789";

        var groupReservation = new GroupReservation
        {
            GroupReservationId = groupReservationId,
            ReservationId = reservationId,
            ReservationToken = "token-123",
            Status = GroupReservationStatus.Open
        };

        var reservation = new Reservation
        {
            Id = reservationId,
            TableId = "table-1",
            WaiterId = "waiter-1",
            CustomerId = userId
        };

        var ownerSession = new GuestSession
        {
            Id = guestSessionId,
            GroupReservationId = groupReservationId,
            GuestName = "Owner",
            OwnerUserId = userId,
            Dishes = [],
            IsOwnerSession = true
        };

        _mockGroupReservationRepository
            .Setup(r => r.GetGroupReservationByIdAsync(groupReservationId))
            .ReturnsAsync(groupReservation);

        _mockReservationRepository
            .Setup(r => r.GetReservationByIdAsync(reservationId))
            .ReturnsAsync(reservation);

        _mockGuestSessionRepository
            .Setup(r => r.GetByGuestSessionId(guestSessionId))
            .ReturnsAsync(ownerSession);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            _guestSessionService.DeleteGuestSessionAsync(userId, groupReservationId, guestSessionId));

        Assert.Equal("Cannot remove the owner session from the group order.", ex.Message);
        _mockGuestSessionRepository.Verify(r => r.DeleteAsync(It.IsAny<GuestSession>()), Times.Never);
    }
}
