using Microsoft.Playwright;

namespace Restaurant_Aqa_Framework.Api;

public sealed class GroupReservationsApi(IAPIRequestContext api) : ApiClientBase(api)
{
    public Task<IAPIResponse> GetInviteTokenAsync(string reservationId, string? bearerToken = null) =>
        GetAsync(Url($"/reservations/{reservationId}/invite-token"), new APIRequestContextOptions
        {
            Headers = Headers(bearerToken),
        });

    public Task<IAPIResponse> GetInviteDetailsAsync(string token) =>
        GetAsync(Url($"/guest/invites/{token}"));

    public Task<IAPIResponse> SubmitConsolidatedPreOrderAsync(string groupReservationId, string reservationId, string? bearerToken = null) =>
        Api.PostAsync(Url($"/group-reservations/{groupReservationId}/submit"), new APIRequestContextOptions
        {
            DataString = Serialize(new { reservationId }),
            Headers = Headers(bearerToken),
        });

    public Task<IAPIResponse> SubmitConsolidatedPreOrderRawAsync(string groupReservationId, string? rawBody = null, string? bearerToken = null) =>
        Api.PostAsync(Url($"/group-reservations/{groupReservationId}/submit"), new APIRequestContextOptions
        {
            DataString = rawBody,
            Headers = Headers(bearerToken),
        });
}
