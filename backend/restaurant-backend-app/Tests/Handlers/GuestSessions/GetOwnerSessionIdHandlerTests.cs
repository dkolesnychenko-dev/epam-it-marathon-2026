using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using APIHandler.Exceptions;
using APIHandler.Handlers.GuestSessions;
using APIHandler.Models.GuestSessions;
using APIHandler.Services.GuestSessions;
using Moq;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

namespace APIHandler.Tests.Handlers.GuestSessions;

public class GetOwnerSessionIdHandlerTests
{
    private readonly Mock<IGuestSessionService> _mockGuestSessionService = new();
    private readonly Mock<ILambdaContext> _mockContext = new();
    private readonly Mock<ILambdaLogger> _mockLogger = new();
    private readonly GetOwnerSessionIdHandler _handler;

    public GetOwnerSessionIdHandlerTests()
    {
        _mockContext.Setup(c => c.Logger).Returns(_mockLogger.Object);
        _handler = new GetOwnerSessionIdHandler(_mockGuestSessionService.Object);
    }

    [Fact]
    public void Constructor_NullGuestSessionService_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new GetOwnerSessionIdHandler(null!));
    }

    [Fact]
    public async Task HandleRequest_ValidRequest_Returns200WithGuestSessionId()
    {
        // Arrange
        const string reservationId = "res-123";
        const string userId = "user-456";
        const string expectedSessionId = "session-123";
        var expectedResponse = new GetOwnerSessionIdResponse(expectedSessionId);

        _mockGuestSessionService
            .Setup(s => s.GetOwnerSessionIdAsync(userId, reservationId))
            .ReturnsAsync(expectedResponse);

        var request = CreateRequest(reservationId, userId);

        // Act
        var response = await _handler.HandleRequest(request, _mockContext.Object);

        // Assert
        Assert.Equal(200, response.StatusCode);
        Assert.NotNull(response.Body);
        using var doc = JsonDocument.Parse(response.Body);
        Assert.Equal(expectedSessionId, doc.RootElement.GetProperty("guestSessionId").GetString());
        _mockGuestSessionService.Verify(s => s.GetOwnerSessionIdAsync(userId, reservationId), Times.Once);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task HandleRequest_MissingReservationId_Returns400(string? reservationId)
    {
        // Arrange
        var request = new APIGatewayProxyRequest
        {
            PathParameters = reservationId != null
                ? new Dictionary<string, string> { { "reservationId", reservationId } }
                : null,
            RequestContext = new APIGatewayProxyRequest.ProxyRequestContext
            {
                Authorizer = new APIGatewayCustomAuthorizerContext
                {
                    Claims = new Dictionary<string, string> { { "sub", "user-123" } }
                }
            }
        };

        // Act
        var response = await _handler.HandleRequest(request, _mockContext.Object);

        // Assert
        Assert.Equal(400, response.StatusCode);
        using var doc = JsonDocument.Parse(response.Body);
        Assert.Equal("Missing 'reservationId' in path parameters.", doc.RootElement.GetProperty("error").GetString());
        _mockGuestSessionService.Verify(s => s.GetOwnerSessionIdAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Theory]
    [InlineData(false, null)] // No RequestContext/Authorizer
    [InlineData(true, null)]  // Claims missing "sub"
    [InlineData(true, "")]    // "sub" empty string
    [InlineData(true, "   ")] // "sub" whitespace
    public async Task HandleRequest_Unauthenticated_Returns401(bool includeClaims, string? subClaim)
    {
        // Arrange
        var request = new APIGatewayProxyRequest
        {
            PathParameters = new Dictionary<string, string> { { "reservationId", "res-123" } }
        };

        if (includeClaims)
        {
            request.RequestContext = new APIGatewayProxyRequest.ProxyRequestContext
            {
                Authorizer = new APIGatewayCustomAuthorizerContext
                {
                    Claims = subClaim != null
                        ? new Dictionary<string, string> { { "sub", subClaim } }
                        : new Dictionary<string, string>()
                }
            };
        }

        // Act
        var response = await _handler.HandleRequest(request, _mockContext.Object);

        // Assert
        Assert.Equal(401, response.StatusCode);
        Assert.Contains("Unauthorized.", response.Body);
        _mockGuestSessionService.Verify(s => s.GetOwnerSessionIdAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task HandleRequest_ReservationNotFound_Returns404()
    {
        // Arrange
        const string reservationId = "res-123";
        const string userId = "user-456";
        var request = CreateRequest(reservationId, userId);

        _mockGuestSessionService
            .Setup(s => s.GetOwnerSessionIdAsync(userId, reservationId))
            .ThrowsAsync(new ReservationNotFoundException($"Reservation with id {reservationId} not found."));

        // Act
        var response = await _handler.HandleRequest(request, _mockContext.Object);

        // Assert
        Assert.Equal(404, response.StatusCode);
        Assert.Contains("Reservation not found.", response.Body);
    }

    [Fact]
    public async Task HandleRequest_GroupReservationNotFound_Returns404()
    {
        // Arrange
        const string reservationId = "res-123";
        const string userId = "user-456";
        var request = CreateRequest(reservationId, userId);

        _mockGuestSessionService
            .Setup(s => s.GetOwnerSessionIdAsync(userId, reservationId))
            .ThrowsAsync(new GroupReservationNotFoundException($"Group reservation for reservation {reservationId} not found."));

        // Act
        var response = await _handler.HandleRequest(request, _mockContext.Object);

        // Assert
        Assert.Equal(404, response.StatusCode);
        Assert.Contains("Group reservation for reservation res-123 not found.", response.Body);
    }

    [Fact]
    public async Task HandleRequest_ActionForbidden_Returns403()
    {
        // Arrange
        const string reservationId = "res-123";
        const string userId = "user-456";
        var request = CreateRequest(reservationId, userId);

        _mockGuestSessionService
            .Setup(s => s.GetOwnerSessionIdAsync(userId, reservationId))
            .ThrowsAsync(new ActionForbiddenException("Only the reservation owner can perform this action."));

        // Act
        var response = await _handler.HandleRequest(request, _mockContext.Object);

        // Assert
        Assert.Equal(403, response.StatusCode);
        Assert.Contains("Only the reservation owner can perform this action.", response.Body);
    }

    [Fact]
    public async Task HandleRequest_ValidationException_Returns400()
    {
        // Arrange
        const string reservationId = "res-123";
        const string userId = "user-456";
        var request = CreateRequest(reservationId, userId);

        _mockGuestSessionService
            .Setup(s => s.GetOwnerSessionIdAsync(userId, reservationId))
            .ThrowsAsync(new ValidationException("Validation failed."));

        // Act
        var response = await _handler.HandleRequest(request, _mockContext.Object);

        // Assert
        Assert.Equal(400, response.StatusCode);
        Assert.Contains("Validation failed.", response.Body);
    }

    private static APIGatewayProxyRequest CreateRequest(string reservationId, string userId)
    {
        return new APIGatewayProxyRequest
        {
            PathParameters = new Dictionary<string, string>
            {
                { "reservationId", reservationId }
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
            }
        };
    }
}
