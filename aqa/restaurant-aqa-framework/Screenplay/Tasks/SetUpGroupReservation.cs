using System.Text;
using System.Text.Json;
using Restaurant_Aqa_Framework.Api;
using Restaurant_Aqa_Framework.Api.Dtos.Auth;
using Restaurant_Aqa_Framework.Api.Dtos.GroupReservations;
using Restaurant_Aqa_Framework.Api.Dtos.Locations;
using Restaurant_Aqa_Framework.Api.Dtos.Reservations;
using Restaurant_Aqa_Framework.Api.Dtos.Tables;
using Restaurant_Aqa_Framework.Api.Seeding;
using Restaurant_Aqa_Framework.Configuration;
using Restaurant_Aqa_Framework.Contexts;
using Restaurant_Aqa_Framework.Screenplay.Abilities;
using Restaurant_Aqa_Framework.Screenplay.Questions;

namespace Restaurant_Aqa_Framework.Screenplay.Tasks;

public sealed class SetUpGroupReservation(int guestCount = 4) : ITask
{
    private sealed record WaiterLocationResponse(string? LocationId);

    public async Task PerformAsAsync(Actor actor)
    {
        var apiContext = actor.UsingTheApi();
        var locationsApi = new LocationsApi(apiContext);
        var tablesApi = new TablesApi(apiContext);
        var reservationsApi = new ReservationsApi(apiContext);
        var groupApi = new GroupReservationsApi(apiContext);
        var authApi = new AuthApi(apiContext);
        var cleanup = actor.Recall<CleanupScope>();

        // 1. Ensure location and table exist (must have a waiter assigned to it)
        if (!actor.TryRecall<LocationResponse>(out var location))
        {
            location = await ResolveLocationWithWaiterAsync(actor, apiContext, locationsApi, authApi, cleanup);
            actor.Remember(location);
        }

        if (!actor.TryRecall<TableResponse>(out var table))
        {
            await actor.AttemptsToAsync(new AddTable(seats: Math.Max(guestCount, 4)));
            table = actor.Recall<TableResponse>();
        }

        // 2. Register & sign in Owner
        var ownerUser = SampleDataFactory.RegisterRequest();
        var regResponse = await authApi.RegisterAsync(ownerUser);
        if (!regResponse.Ok)
            throw new InvalidOperationException($"Owner registration failed ({regResponse.Status}): {await regResponse.TextAsync()}");

        var signInResponse = await authApi.SignInAsync(new SignInRequest(ownerUser.Email, ownerUser.Password));
        if (!signInResponse.Ok)
            throw new InvalidOperationException($"Owner sign in failed ({signInResponse.Status}): {await signInResponse.TextAsync()}");

        var tokens = await signInResponse.ReadAsAsync<SignInResponse>();
        var ownerIdToken = tokens.IdToken;

        // 3. Create group reservation (guestCount >= 2 triggers group reservation creation)
        var startTime = DateTime.UtcNow.AddDays(2).Date.AddHours(18);
        var endTime = startTime.AddHours(2);
        var createReservationReq = new CreateReservationRequest(table.Id, startTime, endTime, guestCount);

        var resResponse = await reservationsApi.CreateReservationAsync(createReservationReq, ownerIdToken);
        if (!resResponse.Ok)
            throw new InvalidOperationException($"Reservation creation failed ({resResponse.Status}): {await resResponse.TextAsync()}");

        var reservation = await resResponse.ReadAsAsync<ReservationResponse>();
        cleanup.TrackDelete($"reservation:{reservation.Id}", () => reservationsApi.CancelReservationAsync(reservation.Id, ownerIdToken));

        // 4. Retrieve invite token and group reservation ID
        var inviteTokenRes = await groupApi.GetInviteTokenAsync(reservation.Id, ownerIdToken);
        if (!inviteTokenRes.Ok)
            throw new InvalidOperationException($"Get invite token failed ({inviteTokenRes.Status}): {await inviteTokenRes.TextAsync()}");

        var tokenDto = await inviteTokenRes.ReadAsAsync<GetGroupInviteTokenResponse>();

        var inviteDetailsRes = await groupApi.GetInviteDetailsAsync(tokenDto.Token);
        if (!inviteDetailsRes.Ok)
            throw new InvalidOperationException($"Get invite details failed ({inviteDetailsRes.Status}): {await inviteDetailsRes.TextAsync()}");

        var groupDetails = await inviteDetailsRes.ReadAsAsync<GetGroupReservationResponse>();

        var groupContext = new GroupReservationContext
        {
            Reservation = reservation,
            GroupReservationId = groupDetails.GroupReservationId,
            InviteToken = tokenDto.Token,
            OwnerBearerToken = ownerIdToken,
            OwnerUser = ownerUser
        };

        actor.Remember(groupContext);
        actor.Remember(reservation);

        // If UI actor, sign in to the browser session so the Owner is authenticated in the UI
        if (actor.Can<IBrowseTheWeb>())
        {
            await actor.AttemptsToAsync(new SignInThroughTheLoginForm(ownerUser.Email, ownerUser.Password));
        }
    }

    private static async Task<LocationResponse> ResolveLocationWithWaiterAsync(
        Actor actor,
        Microsoft.Playwright.IAPIRequestContext apiContext,
        LocationsApi locationsApi,
        AuthApi authApi,
        CleanupScope cleanup)
    {
        // Strategy A: Find the location assigned to the standing configured Waiter
        if (Settings.Current.WaiterUsername is { Length: > 0 } && Settings.Current.WaiterPassword is { Length: > 0 })
        {
            try
            {
                var waiterSession = await actor.AsksForAsync(new TheWaiterSession());
                var waiterUserId = ExtractSubFromJwt(waiterSession.IdToken);
                if (!string.IsNullOrEmpty(waiterUserId))
                {
                    var waiterLocRes = await apiContext.GetAsync($"{Settings.Current.ApiBaseUrl.TrimEnd('/')}/waiters/{waiterUserId}/location");
                    if (waiterLocRes.Ok)
                    {
                        var locDto = await waiterLocRes.ReadAsAsync<WaiterLocationResponse>();
                        if (!string.IsNullOrEmpty(locDto?.LocationId))
                        {
                            var locRes = await locationsApi.GetLocationAsync(locDto.LocationId);
                            if (locRes.Ok)
                            {
                                return await locRes.ReadAsAsync<LocationResponse>();
                            }
                        }
                    }
                }
            }
            catch
            {
                // Proceed to next fallback strategy
            }
        }

        // Strategy B: Reuse an existing seeded permanent location from the database
        try
        {
            var existingLocationsRes = await locationsApi.GetLocationsAsync();
            if (existingLocationsRes.Ok)
            {
                var allLocs = await existingLocationsRes.ReadAsAsync<List<LocationResponse>>();
                var seeded = allLocs?.FirstOrDefault(l => !l.Address.StartsWith(SampleDataFactory.Prefix, StringComparison.Ordinal));
                if (seeded is not null)
                {
                    return seeded;
                }
            }
        }
        catch
        {
            // Proceed to self-provisioning fallback
        }

        // Strategy C: Self-provision a new location AND assign a newly created waiter to it
        await actor.AttemptsToAsync(new SetUpLocation());
        var location = actor.Recall<LocationResponse>();

        var waiterUser = SampleDataFactory.RegisterRequest();
        var regRes = await authApi.RegisterAsync(waiterUser);
        if (regRes.Ok)
        {
            var addWaiterRes = await apiContext.PostAsync(
                $"{Settings.Current.ApiBaseUrl.TrimEnd('/')}/waiters",
                new Microsoft.Playwright.APIRequestContextOptions
                {
                    DataString = JsonSerializer.Serialize(new { email = waiterUser.Email, locationId = location.Id }),
                    Headers = new Dictionary<string, string> { ["Content-Type"] = "application/json" }
                });

            if (addWaiterRes.Ok)
            {
                cleanup.TrackDelete($"waiter:{waiterUser.Email}", () => apiContext.DeleteAsync(
                    $"{Settings.Current.ApiBaseUrl.TrimEnd('/')}/waiters",
                    new Microsoft.Playwright.APIRequestContextOptions
                    {
                        DataString = JsonSerializer.Serialize(new { email = waiterUser.Email }),
                        Headers = new Dictionary<string, string> { ["Content-Type"] = "application/json" }
                    }));
            }
        }

        return location;
    }

    private static string? ExtractSubFromJwt(string jwt)
    {
        try
        {
            var parts = jwt.Split('.');
            if (parts.Length < 2) return null;
            var base64 = parts[1].PadRight(parts[1].Length + (4 - parts[1].Length % 4) % 4, '=').Replace('-', '+').Replace('_', '/');
            var json = Encoding.UTF8.GetString(Convert.FromBase64String(base64));
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("sub", out var subProp))
                return subProp.GetString();
        }
        catch { }
        return null;
    }
}
