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

public class GetGuestSessionsHandlerTests
{
    private readonly Mock<IGuestSessionService> _mockGuestSessionService = new();
    private readonly Mock<ILambdaContext> _mockContext = new();
    private readonly Mock<ILambdaLogger> _mockLogger = new();
    private readonly GetGuestSessionsHandler _handler;

    public GetGuestSessionsHandlerTests()
    {
        _mockContext.Setup(c => c.Logger).Returns(_mockLogger.Object);
        _handler = new GetGuestSessionsHandler(_mockGuestSessionService.Object);
    }

    [Fact]
    public void Constructor_NullGuestSessionService_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new GetGuestSessionsHandler(null!));
    }

    [Fact]
    public async Task HandleRequest_ValidRequest_Returns200WithPayload()
    {
        // Arrange
        var groupReservationId = "group-123";
        var userId = "user-456";
        var expectedResponse = new GetAllSessionsResponse(
            new List<Participant>
            {
                new("session-1", ParticipantType.Owner, "John", [], 0m, true)
            },
            0m);

        _mockGuestSessionService
            .Setup(s => s.GetGuestSessionsAsync(userId, groupReservationId))
            .ReturnsAsync(expectedResponse);

        var request = CreateRequest(groupReservationId, userId);

        // Act
        var response = await _handler.HandleRequest(request, _mockContext.Object);

        // Assert
        Assert.Equal(200, response.StatusCode);
        Assert.NotNull(response.Body);
        Assert.Contains("session-1", response.Body);
        _mockGuestSessionService.Verify(s => s.GetGuestSessionsAsync(userId, groupReservationId), Times.Once);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
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
            }
        };

        // Act
        var response = await _handler.HandleRequest(request, _mockContext.Object);

        // Assert
        Assert.Equal(400, response.StatusCode);
        using var doc = JsonDocument.Parse(response.Body);
        Assert.Equal("Missing 'groupReservationId' in path parameters.", doc.RootElement.GetProperty("error").GetString());
        _mockGuestSessionService.Verify(s => s.GetGuestSessionsAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
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
            PathParameters = new Dictionary<string, string> { { "groupReservationId", "group-123" } }
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
        _mockGuestSessionService.Verify(s => s.GetGuestSessionsAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task HandleRequest_ActionForbiddenException_Returns403()
    {
        // Arrange
        var groupReservationId = "group-123";
        var userId = "user-456";
        var request = CreateRequest(groupReservationId, userId);

        _mockGuestSessionService
            .Setup(s => s.GetGuestSessionsAsync(userId, groupReservationId))
            .ThrowsAsync(new ActionForbiddenException("Only the reservation owner can perform this action."));

        // Act
        var response = await _handler.HandleRequest(request, _mockContext.Object);

        // Assert
        Assert.Equal(403, response.StatusCode);
        Assert.Contains("Only the reservation owner can perform this action.", response.Body);
    }

    [Fact]
    public async Task HandleRequest_GroupReservationNotFoundException_Returns404()
    {
        // Arrange
        var groupReservationId = "group-123";
        var userId = "user-456";
        var request = CreateRequest(groupReservationId, userId);

        _mockGuestSessionService
            .Setup(s => s.GetGuestSessionsAsync(userId, groupReservationId))
            .ThrowsAsync(new GroupReservationNotFoundException());

        // Act
        var response = await _handler.HandleRequest(request, _mockContext.Object);

        // Assert
        Assert.Equal(404, response.StatusCode);
        Assert.Contains("Group reservation was not found", response.Body);
    }

    [Fact]
    public async Task HandleRequest_ReservationNotFoundException_Returns404()
    {
        // Arrange
        var groupReservationId = "group-123";
        var userId = "user-456";
        var request = CreateRequest(groupReservationId, userId);

        _mockGuestSessionService
            .Setup(s => s.GetGuestSessionsAsync(userId, groupReservationId))
            .ThrowsAsync(new ReservationNotFoundException("Parent reservation not found."));

        // Act
        var response = await _handler.HandleRequest(request, _mockContext.Object);

        // Assert
        Assert.Equal(404, response.StatusCode);
        Assert.Contains("Reservation not found.", response.Body);
    }

    [Fact]
    public async Task HandleRequest_ValidationException_Returns400()
    {
        // Arrange
        var groupReservationId = "group-123";
        var userId = "user-456";
        var request = CreateRequest(groupReservationId, userId);

        _mockGuestSessionService
            .Setup(s => s.GetGuestSessionsAsync(userId, groupReservationId))
            .ThrowsAsync(new ValidationException("Validation failed."));

        // Act
        var response = await _handler.HandleRequest(request, _mockContext.Object);

        // Assert
        Assert.Equal(400, response.StatusCode);
        Assert.Contains("Validation failed.", response.Body);
    }

    private static APIGatewayProxyRequest CreateRequest(string groupReservationId, string userId)
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
            }
        };
    }
}
