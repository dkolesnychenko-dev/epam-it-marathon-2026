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
    public async Task PerformAsAsync(Actor actor)
    {
        var apiContext = actor.UsingTheApi();
        var locationsApi = new LocationsApi(apiContext);
        var tablesApi = new TablesApi(apiContext);
        var reservationsApi = new ReservationsApi(apiContext);
        var groupApi = new GroupReservationsApi(apiContext);
        var authApi = new AuthApi(apiContext);
        var cleanup = actor.Recall<CleanupScope>();

        // 1. Ensure location and table exist
        if (!actor.TryRecall<LocationResponse>(out var location))
        {
            await actor.AttemptsToAsync(new SetUpLocation());
            location = actor.Recall<LocationResponse>();
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
}
