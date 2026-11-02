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

public class DeleteGuestSessionHandlerTests
{
    private readonly Mock<IGuestSessionService> _mockGuestSessionService = new();
    private readonly Mock<ILambdaContext> _mockContext = new();
    private readonly Mock<ILambdaLogger> _mockLogger = new();
    private readonly DeleteGuestSessionHandler _handler;

    public DeleteGuestSessionHandlerTests()
    {
        _mockContext.Setup(c => c.Logger).Returns(_mockLogger.Object);
        _handler = new DeleteGuestSessionHandler(_mockGuestSessionService.Object);
    }

    [Fact]
    public void Constructor_NullGuestSessionService_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new DeleteGuestSessionHandler(null!));
    }

    [Fact]
    public async Task HandleRequest_ValidRequest_Returns200WithUpdatedSnapshot()
    {
        // Arrange
        const string groupReservationId = "group-123";
        const string guestSessionId = "session-456";
        const string userId = "user-789";
        var expectedResponse = new GetAllSessionsResponse(
            new List<Participant>
            {
                new("session-owner", ParticipantType.Owner, "Alice", [], 0m, false)
            },
            0m);

        _mockGuestSessionService
            .Setup(s => s.DeleteGuestSessionAsync(userId, groupReservationId, guestSessionId))
            .ReturnsAsync(expectedResponse);

        var request = CreateRequest(groupReservationId, guestSessionId, userId);

        // Act
        var response = await _handler.HandleRequest(request, _mockContext.Object);

        // Assert
        Assert.Equal(200, response.StatusCode);
        Assert.NotNull(response.Body);
        Assert.Contains("session-owner", response.Body);
        _mockGuestSessionService.Verify(s => s.DeleteGuestSessionAsync(userId, groupReservationId, guestSessionId), Times.Once);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task HandleRequest_MissingGroupReservationId_Returns400(string? groupReservationId)
    {
        // Arrange
        var pathParams = new Dictionary<string, string> { { "guestSessionId", "session-456" } };
        if (groupReservationId != null)
        {
            pathParams["groupReservationId"] = groupReservationId;
        }

        var request = new APIGatewayProxyRequest
        {
            PathParameters = groupReservationId == null ? null : pathParams,
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
        _mockGuestSessionService.Verify(s => s.DeleteGuestSessionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task HandleRequest_MissingGuestSessionId_Returns400(string? guestSessionId)
    {
        // Arrange
        var pathParams = new Dictionary<string, string> { { "groupReservationId", "group-123" } };
        if (guestSessionId != null)
        {
            pathParams["guestSessionId"] = guestSessionId;
        }

        var request = new APIGatewayProxyRequest
        {
            PathParameters = pathParams,
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
        Assert.Equal("Missing 'guestSessionId' in path parameters.", doc.RootElement.GetProperty("error").GetString());
        _mockGuestSessionService.Verify(s => s.DeleteGuestSessionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
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
            PathParameters = new Dictionary<string, string>
            {
                { "groupReservationId", "group-123" },
                { "guestSessionId", "session-456" }
            }
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
        _mockGuestSessionService.Verify(s => s.DeleteGuestSessionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task HandleRequest_ActionForbiddenException_Returns403()
    {
        // Arrange
        const string groupReservationId = "group-123";
        const string guestSessionId = "session-456";
        const string userId = "user-789";
        var request = CreateRequest(groupReservationId, guestSessionId, userId);

        _mockGuestSessionService
            .Setup(s => s.DeleteGuestSessionAsync(userId, groupReservationId, guestSessionId))
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
        const string groupReservationId = "group-123";
        const string guestSessionId = "session-456";
        const string userId = "user-789";
        var request = CreateRequest(groupReservationId, guestSessionId, userId);

        _mockGuestSessionService
            .Setup(s => s.DeleteGuestSessionAsync(userId, groupReservationId, guestSessionId))
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
        const string groupReservationId = "group-123";
        const string guestSessionId = "session-456";
        const string userId = "user-789";
        var request = CreateRequest(groupReservationId, guestSessionId, userId);

        _mockGuestSessionService
            .Setup(s => s.DeleteGuestSessionAsync(userId, groupReservationId, guestSessionId))
            .ThrowsAsync(new ReservationNotFoundException("Parent reservation not found."));

        // Act
        var response = await _handler.HandleRequest(request, _mockContext.Object);

        // Assert
        Assert.Equal(404, response.StatusCode);
        Assert.Contains("Reservation not found.", response.Body);
    }

    [Fact]
    public async Task HandleRequest_GuestSessionNotFoundException_Returns404()
    {
        // Arrange
        const string groupReservationId = "group-123";
        const string guestSessionId = "session-456";
        const string userId = "user-789";
        var request = CreateRequest(groupReservationId, guestSessionId, userId);

        _mockGuestSessionService
            .Setup(s => s.DeleteGuestSessionAsync(userId, groupReservationId, guestSessionId))
            .ThrowsAsync(new GuestSessionNotFoundException("Guest session not found."));

        // Act
        var response = await _handler.HandleRequest(request, _mockContext.Object);

        // Assert
        Assert.Equal(404, response.StatusCode);
        Assert.Contains("Guest session not found.", response.Body);
    }

    [Fact]
    public async Task HandleRequest_ValidationException_Returns400()
    {
        // Arrange
        const string groupReservationId = "group-123";
        const string guestSessionId = "session-456";
        const string userId = "user-789";
        var request = CreateRequest(groupReservationId, guestSessionId, userId);

        _mockGuestSessionService
            .Setup(s => s.DeleteGuestSessionAsync(userId, groupReservationId, guestSessionId))
            .ThrowsAsync(new ValidationException("Validation failed."));

        // Act
        var response = await _handler.HandleRequest(request, _mockContext.Object);

        // Assert
        Assert.Equal(400, response.StatusCode);
        Assert.Contains("Validation failed.", response.Body);
    }

    private static APIGatewayProxyRequest CreateRequest(string groupReservationId, string guestSessionId, string userId)
    {
        return new APIGatewayProxyRequest
        {
            PathParameters = new Dictionary<string, string>
            {
                { "groupReservationId", groupReservationId },
                { "guestSessionId", guestSessionId }
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
