using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using APIHandler.Exceptions;
using APIHandler.Handlers.GroupReservations;
using APIHandler.Models.GroupReservation;
using APIHandler.Services.GroupReservations;
using Moq;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

namespace APIHandler.Tests.Handlers.GroupReservations;

public class SubmitConsolidatedPreOrderHandlerTests
{
    private readonly Mock<IGroupReservationService> _mockGroupReservationService = new();
    private readonly Mock<ILambdaContext> _mockContext = new();
    private readonly Mock<ILambdaLogger> _mockLogger = new();
    private readonly SubmitConsolidatedPreOrderHandler _handler;

    public SubmitConsolidatedPreOrderHandlerTests()
    {
        _mockContext.Setup(c => c.Logger).Returns(_mockLogger.Object);
        _handler = new SubmitConsolidatedPreOrderHandler(_mockGroupReservationService.Object);
    }

    [Fact]
    public void Constructor_NullGroupReservationService_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new SubmitConsolidatedPreOrderHandler(null!));
    }

    [Fact]
    public async Task HandleRequest_ValidRequest_Returns200WithConsolidatedDishes()
    {
        // Arrange
        const string groupReservationId = "group-123";
        const string reservationId = "res-456";
        const string userId = "user-789";
        var expectedDishes = new List<ConsolidatedDishResponse>
        {
            new("dish-1", 3),
            new("dish-2", 2)
        };

        _mockGroupReservationService
            .Setup(s => s.SubmitConsolidatedPreOrderAsync(userId, groupReservationId, reservationId))
            .ReturnsAsync(expectedDishes);

        var request = CreateRequest(groupReservationId, userId, reservationId);

        // Act
        var response = await _handler.HandleRequest(request, _mockContext.Object);

        // Assert
        Assert.Equal(200, response.StatusCode);
        Assert.NotNull(response.Body);

        var returnedDishes = JsonSerializer.Deserialize<List<ConsolidatedDishResponse>>(
            response.Body,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.NotNull(returnedDishes);
        Assert.Equal(2, returnedDishes.Count);
        Assert.Equal("dish-1", returnedDishes[0].DishId);
        Assert.Equal(3, returnedDishes[0].Quantity);
        Assert.Equal("dish-2", returnedDishes[1].DishId);
        Assert.Equal(2, returnedDishes[1].Quantity);

        _mockGroupReservationService.Verify(
            s => s.SubmitConsolidatedPreOrderAsync(userId, groupReservationId, reservationId),
            Times.Once);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task HandleRequest_MissingGroupReservationId_Returns400(string? groupReservationId)
    {
        // Arrange
        var request = new APIGatewayProxyRequest
        {
            PathParameters = groupReservationId != null
                ? new Dictionary<string, string> { { "groupReservationId", groupReservationId } }
                : null,
            RequestContext = new APIGatewayProxyRequest.ProxyRequestContext
            {
                Authorizer = new APIGatewayCustomAuthorizerContext
                {
                    Claims = new Dictionary<string, string> { { "sub", "user-123" } }
                }
            },
            Body = JsonSerializer.Serialize(new { reservationId = "res-123" })
        };

        // Act
        var response = await _handler.HandleRequest(request, _mockContext.Object);

        // Assert
        Assert.Equal(400, response.StatusCode);
        using var doc = JsonDocument.Parse(response.Body);
        Assert.Equal("Group reservation ID is required.", doc.RootElement.GetProperty("error").GetString());
        _mockGroupReservationService.Verify(
            s => s.SubmitConsolidatedPreOrderAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task HandleRequest_MissingOrInvalidBody_Returns400(string? body)
    {
        // Arrange
        var request = new APIGatewayProxyRequest
        {
            PathParameters = new Dictionary<string, string> { { "groupReservationId", "group-123" } },
            RequestContext = new APIGatewayProxyRequest.ProxyRequestContext
            {
                Authorizer = new APIGatewayCustomAuthorizerContext
                {
                    Claims = new Dictionary<string, string> { { "sub", "user-123" } }
                }
            },
            Body = body!
        };

        // Act
        var response = await _handler.HandleRequest(request, _mockContext.Object);

        // Assert
        Assert.Equal(400, response.StatusCode);
        using var doc = JsonDocument.Parse(response.Body);
        Assert.Equal("Request body is required.", doc.RootElement.GetProperty("error").GetString());
        _mockGroupReservationService.Verify(
            s => s.SubmitConsolidatedPreOrderAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleRequest_InvalidJsonBody_Returns400()
    {
        // Arrange
        var request = new APIGatewayProxyRequest
        {
            PathParameters = new Dictionary<string, string> { { "groupReservationId", "group-123" } },
            RequestContext = new APIGatewayProxyRequest.ProxyRequestContext
            {
                Authorizer = new APIGatewayCustomAuthorizerContext
                {
                    Claims = new Dictionary<string, string> { { "sub", "user-123" } }
                }
            },
            Body = "{invalid-json-body"
        };

        // Act
        var response = await _handler.HandleRequest(request, _mockContext.Object);

        // Assert
        Assert.Equal(400, response.StatusCode);
        using var doc = JsonDocument.Parse(response.Body);
        Assert.Equal("Invalid JSON body.", doc.RootElement.GetProperty("error").GetString());
        _mockGroupReservationService.Verify(
            s => s.SubmitConsolidatedPreOrderAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"reservationId\": null}")]
    [InlineData("{\"reservationId\": \"\"}")]
    [InlineData("{\"reservationId\": \"   \"}")]
    public async Task HandleRequest_MissingReservationIdInBody_Returns400(string body)
    {
        // Arrange
        var request = new APIGatewayProxyRequest
        {
            PathParameters = new Dictionary<string, string> { { "groupReservationId", "group-123" } },
            RequestContext = new APIGatewayProxyRequest.ProxyRequestContext
            {
                Authorizer = new APIGatewayCustomAuthorizerContext
                {
                    Claims = new Dictionary<string, string> { { "sub", "user-123" } }
                }
            },
            Body = body
        };

        // Act
        var response = await _handler.HandleRequest(request, _mockContext.Object);

        // Assert
        Assert.Equal(400, response.StatusCode);
        using var doc = JsonDocument.Parse(response.Body);
        Assert.Equal("Reservation ID is required.", doc.RootElement.GetProperty("error").GetString());
        _mockGroupReservationService.Verify(
            s => s.SubmitConsolidatedPreOrderAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
    }

    [Theory]
    [InlineData(false, null)] // No Authorizer
    [InlineData(true, null)]  // Claims missing "sub"
    [InlineData(true, "")]    // "sub" empty string
    [InlineData(true, "   ")] // "sub" whitespace
    public async Task HandleRequest_Unauthenticated_Returns401(bool includeClaims, string? subClaim)
    {
        // Arrange
        var request = new APIGatewayProxyRequest
        {
            PathParameters = new Dictionary<string, string> { { "groupReservationId", "group-123" } },
            RequestContext = new APIGatewayProxyRequest.ProxyRequestContext(),
            Body = JsonSerializer.Serialize(new { reservationId = "res-123" })
        };

        if (includeClaims)
        {
            request.RequestContext.Authorizer = new APIGatewayCustomAuthorizerContext
            {
                Claims = subClaim != null
                    ? new Dictionary<string, string> { { "sub", subClaim } }
                    : new Dictionary<string, string>()
            };
        }

        // Act
        var response = await _handler.HandleRequest(request, _mockContext.Object);

        // Assert
        Assert.Equal(401, response.StatusCode);
        using var doc = JsonDocument.Parse(response.Body);
        Assert.Equal("Unauthorized.", doc.RootElement.GetProperty("error").GetString());
        _mockGroupReservationService.Verify(
            s => s.SubmitConsolidatedPreOrderAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleRequest_ReservationNotFound_Returns404()
    {
        // Arrange
        const string groupReservationId = "group-123";
        const string reservationId = "res-456";
        const string userId = "user-789";

        _mockGroupReservationService
            .Setup(s => s.SubmitConsolidatedPreOrderAsync(userId, groupReservationId, reservationId))
            .ThrowsAsync(new ReservationNotFoundException($"Reservation with ID {reservationId} was not found."));

        var request = CreateRequest(groupReservationId, userId, reservationId);

        // Act
        var response = await _handler.HandleRequest(request, _mockContext.Object);

        // Assert
        Assert.Equal(404, response.StatusCode);
        using var doc = JsonDocument.Parse(response.Body);
        Assert.Contains("Reservation not found.", doc.RootElement.GetProperty("error").GetString());
    }

    [Fact]
    public async Task HandleRequest_GroupReservationNotFound_Returns404()
    {
        // Arrange
        const string groupReservationId = "group-123";
        const string reservationId = "res-456";
        const string userId = "user-789";

        _mockGroupReservationService
            .Setup(s => s.SubmitConsolidatedPreOrderAsync(userId, groupReservationId, reservationId))
            .ThrowsAsync(new GroupReservationNotFoundException($"Group reservation with ID {groupReservationId} was not found."));

        var request = CreateRequest(groupReservationId, userId, reservationId);

        // Act
        var response = await _handler.HandleRequest(request, _mockContext.Object);

        // Assert
        Assert.Equal(404, response.StatusCode);
        using var doc = JsonDocument.Parse(response.Body);
        Assert.Contains("Group reservation with ID group-123 was not found.", doc.RootElement.GetProperty("error").GetString());
    }

    [Fact]
    public async Task HandleRequest_ActionForbidden_Returns403()
    {
        // Arrange
        const string groupReservationId = "group-123";
        const string reservationId = "res-456";
        const string userId = "user-789";

        _mockGroupReservationService
            .Setup(s => s.SubmitConsolidatedPreOrderAsync(userId, groupReservationId, reservationId))
            .ThrowsAsync(new ActionForbiddenException("Only the reservation owner can submit the consolidated pre-order."));

        var request = CreateRequest(groupReservationId, userId, reservationId);

        // Act
        var response = await _handler.HandleRequest(request, _mockContext.Object);

        // Assert
        Assert.Equal(403, response.StatusCode);
        using var doc = JsonDocument.Parse(response.Body);
        Assert.Equal("Only the reservation owner can submit the consolidated pre-order.", doc.RootElement.GetProperty("error").GetString());
    }

    [Fact]
    public async Task HandleRequest_ValidationException_Returns400()
    {
        // Arrange
        const string groupReservationId = "group-123";
        const string reservationId = "res-456";
        const string userId = "user-789";

        _mockGroupReservationService
            .Setup(s => s.SubmitConsolidatedPreOrderAsync(userId, groupReservationId, reservationId))
            .ThrowsAsync(new ValidationException("There are no submitted guest sessions."));

        var request = CreateRequest(groupReservationId, userId, reservationId);

        // Act
        var response = await _handler.HandleRequest(request, _mockContext.Object);

        // Assert
        Assert.Equal(400, response.StatusCode);
        using var doc = JsonDocument.Parse(response.Body);
        Assert.Equal("There are no submitted guest sessions.", doc.RootElement.GetProperty("error").GetString());
    }

    private static APIGatewayProxyRequest CreateRequest(string groupReservationId, string userId, string reservationId)
    {
        return new APIGatewayProxyRequest
        {
            PathParameters = new Dictionary<string, string>
            {
                { "groupReservationId", groupReservationId }
            },
            RequestContext = new APIGatewayProxyRequest.ProxyRequestContext
            {
                Authorizer = new APIGatewayCustomAuthorizerContext
                {
                    Claims = new Dictionary<string, string>
                    {
                        { "sub", userId }
                    }
                }
            },
            Body = JsonSerializer.Serialize(new { reservationId })
        };
    }
}
