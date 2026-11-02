using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using APIHandler.Exceptions;
using APIHandler.Helpers;
using APIHandler.Models.GroupReservation;
using APIHandler.Services.GroupReservations;
using System;
using System.Text.Json;
using System.Threading.Tasks;

namespace APIHandler.Handlers.GroupReservations;

public class SubmitConsolidatedPreOrderHandler(IGroupReservationService groupReservationService) : IApiRequestHandler
{
    private readonly IGroupReservationService _groupReservationService = groupReservationService ?? throw new ArgumentNullException(nameof(groupReservationService));

    public async Task<APIGatewayProxyResponse> HandleRequest(APIGatewayProxyRequest request, ILambdaContext context)
    {
        if (!request.TryGetPathValue("groupReservationId", out var groupReservationId) || string.IsNullOrWhiteSpace(groupReservationId))
        {
            return ResponseBuilder.CreateErrorResponse(400, "Group reservation ID is required.", context.Logger);
        }

        var userId = UserContextHelper.GetAuthenticatedUserId(request);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return ResponseBuilder.CreateErrorResponse(401, "Unauthorized.", context.Logger);
        }

        if (string.IsNullOrWhiteSpace(request.Body))
        {
            return ResponseBuilder.CreateErrorResponse(400, "Request body is required.", context.Logger);
        }

        SubmitConsolidatedPreOrderRequest? requestBody;
        try
        {
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            requestBody = JsonSerializer.Deserialize<SubmitConsolidatedPreOrderRequest>(request.Body, options);
        }
        catch (JsonException)
        {
            return ResponseBuilder.CreateErrorResponse(400, "Invalid JSON body.", context.Logger);
        }

        if (requestBody == null || string.IsNullOrWhiteSpace(requestBody.ReservationId))
        {
            return ResponseBuilder.CreateErrorResponse(400, "Reservation ID is required.", context.Logger);
        }

        try
        {
            var result = await _groupReservationService.SubmitConsolidatedPreOrderAsync(
                userId,
                groupReservationId,
                requestBody.ReservationId);

            return ResponseBuilder.CreateSuccessResponse(result);
        }
        catch (ReservationNotFoundException ex)
        {
            return ResponseBuilder.CreateErrorResponse(404, ex.Message, context.Logger, ex);
        }
        catch (GroupReservationNotFoundException ex)
        {
            return ResponseBuilder.CreateErrorResponse(404, ex.Message, context.Logger, ex);
        }
        catch (ActionForbiddenException ex)
        {
            return ResponseBuilder.CreateErrorResponse(403, ex.Message, context.Logger, ex);
        }
        catch (ValidationException ex)
        {
            return ResponseBuilder.CreateErrorResponse(400, ex.Message, context.Logger, ex);
        }
    }
}
