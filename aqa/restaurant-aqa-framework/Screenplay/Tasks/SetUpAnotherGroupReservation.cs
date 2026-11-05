using Restaurant_Aqa_Framework.Api;
using Restaurant_Aqa_Framework.Api.Dtos.Auth;
using Restaurant_Aqa_Framework.Api.Dtos.GroupReservations;
using Restaurant_Aqa_Framework.Api.Dtos.GuestSessions;
using Restaurant_Aqa_Framework.Api.Dtos.Reservations;
using Restaurant_Aqa_Framework.Api.Dtos.Tables;
using Restaurant_Aqa_Framework.Api.Seeding;
using Restaurant_Aqa_Framework.Contexts;

namespace Restaurant_Aqa_Framework.Screenplay.Tasks;

public sealed class SetUpAnotherGroupReservation(string? guestName = null) : ITask
{
    public const string SecondGroupKey = "SecondGroupContext";

    public async Task PerformAsAsync(Actor actor)
    {
        var api = actor.UsingTheApi();
        var authApi = new AuthApi(api);
        var reservationsApi = new ReservationsApi(api);
        var groupApi = new GroupReservationsApi(api);
        var guestSessionsApi = new GuestSessionsApi(api);
        var cleanup = actor.Recall<CleanupScope>();

        var hadFirstContext = actor.TryRecall<GroupReservationContext>(out var firstContext);

        // 1. Add another table
        await actor.AttemptsToAsync(new AddTable(seats: 4));
        var secondTable = actor.Recall<TableResponse>();

        // 2. Register & sign in second Owner
        var secondOwner = SampleDataFactory.RegisterRequest();
        var regRes = await authApi.RegisterAsync(secondOwner);
        if (!regRes.Ok)
            throw new InvalidOperationException($"Registering second owner failed: {await regRes.TextAsync()}");

        var signInRes = await authApi.SignInAsync(new SignInRequest(secondOwner.Email, secondOwner.Password));
        if (!signInRes.Ok)
            throw new InvalidOperationException($"Signing in second owner failed: {await signInRes.TextAsync()}");

        var tokens = await signInRes.ReadAsAsync<SignInResponse>();
        var secondOwnerToken = tokens.IdToken;

        // 3. Create second group reservation
        var startTime = DateTime.UtcNow.AddDays(2).Date.AddHours(20);
        var endTime = startTime.AddHours(2);
        var createReservationReq = new CreateReservationRequest(secondTable.Id, startTime, endTime, 4);

        var resResponse = await reservationsApi.CreateReservationAsync(createReservationReq, secondOwnerToken);
        if (!resResponse.Ok)
            throw new InvalidOperationException($"Second reservation creation failed: {await resResponse.TextAsync()}");

        var secondReservation = await resResponse.ReadAsAsync<ReservationResponse>();
        cleanup.TrackDelete($"reservation:{secondReservation.Id}", () => reservationsApi.CancelReservationAsync(secondReservation.Id, secondOwnerToken));

        // 4. Retrieve invite token and group reservation ID
        var inviteTokenRes = await groupApi.GetInviteTokenAsync(secondReservation.Id, secondOwnerToken);
        if (!inviteTokenRes.Ok)
            throw new InvalidOperationException($"Getting second invite token failed: {await inviteTokenRes.TextAsync()}");

        var tokenDto = await inviteTokenRes.ReadAsAsync<GetGroupInviteTokenResponse>();

        var inviteDetailsRes = await groupApi.GetInviteDetailsAsync(tokenDto.Token);
        if (!inviteDetailsRes.Ok)
            throw new InvalidOperationException($"Getting second invite details failed: {await inviteDetailsRes.TextAsync()}");

        var groupDetails = await inviteDetailsRes.ReadAsAsync<GetGroupReservationResponse>();

        var secondContext = new GroupReservationContext
        {
            Reservation = secondReservation,
            GroupReservationId = groupDetails.GroupReservationId,
            InviteToken = tokenDto.Token,
            OwnerBearerToken = secondOwnerToken,
            OwnerUser = secondOwner
        };

        // 5. Add guest to second group if specified
        if (!string.IsNullOrEmpty(guestName))
        {
            var createSessionRes = await guestSessionsApi.CreateGuestSessionAsync(
                new CreateGuestSessionRequest(secondContext.GroupReservationId, guestName));

            if (!createSessionRes.Ok)
                throw new InvalidOperationException($"Creating second guest session failed: {await createSessionRes.TextAsync()}");

            var session = await createSessionRes.ReadAsAsync<CreateGuestSessionResponse>();
            secondContext.Guests.Add(new GuestContext
            {
                GuestName = guestName,
                SessionId = session.SessionId,
                SessionToken = session.SessionToken,
                IsSubmitted = false
            });
        }

        actor.Remember(SecondGroupKey, secondContext);
        if (hadFirstContext)
        {
            actor.Remember(firstContext);
        }
    }
}
