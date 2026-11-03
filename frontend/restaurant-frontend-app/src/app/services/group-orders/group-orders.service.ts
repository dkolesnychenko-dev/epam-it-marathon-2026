import { inject, Injectable } from '@angular/core';
import { HttpClient, HttpContext, HttpErrorResponse } from '@angular/common/http';
import { forkJoin, Observable } from 'rxjs';
import { catchError, map, switchMap } from 'rxjs/operators';
import { API_BASE_URL } from '@app/shared/config/api-base-url.token';
import { SKIP_ERROR_TOAST } from '@app/auth/skip-error-toast.context';
import { GuestInvite } from '@app/models/reservations/guest-invite.model';
import { GroupSessionsResponse } from '@app/models/group-orders/group-sessions-response.model';
import { GroupStatusResponse } from '@app/models/group-orders/group-status.model';
import { OwnerSessionResponse } from '@app/models/group-orders/owner-session.model';
import { SubmittedDish } from '@app/models/group-orders/submit-group-pre-order.model';

@Injectable({ providedIn: 'root' })
export class GroupOrdersService {
  private readonly baseUrl = inject(API_BASE_URL);
  private readonly http = inject(HttpClient);

  getGroupInvite$(reservationId: string): Observable<GuestInvite> {
    const context = new HttpContext().set(SKIP_ERROR_TOAST, true);
    return this.http
      .get<{ token: string }>(`${this.baseUrl}/reservations/${reservationId}/invite-token`, { context })
      .pipe(
        switchMap(({ token }) => this.http.get<GuestInvite>(`${this.baseUrl}/guest/invites/${token}`, { context })),
        catchError((err: HttpErrorResponse) => {
          if (err.status === 400 || err.status === 404) {
            return forkJoin({
              owner: this.getOwnerSession$(reservationId),
              res: this.http.get<any>(`${this.baseUrl}/reservations/${reservationId}`),
            }).pipe(
              map(({ owner, res }): GuestInvite => ({
                GroupReservationId: owner.groupReservationId ?? '',
                LocationId: res.locationId ?? '',
                RestaurantName: res.restaurantName ?? '',
                LocationAddress: res.locationAddress ?? '',
                StartTime: res.startTime ? new Date(res.startTime).toISOString() : '',
                EndTime: res.endTime ? new Date(res.endTime).toISOString() : '',
                MaxGuestCount: res.guestCount ?? 0,
                TableNumber: res.tableNumber ?? (res.tableName ? parseInt(res.tableName.replace(/\D/g, ''), 10) || 1 : 1),
                InviterName: res.createdByName ?? 'Host',
              }))
            );
          }
          throw err;
        })
      );
  }

  getSessions$(groupReservationId: string, options?: { silent?: boolean }): Observable<GroupSessionsResponse> {
    const context = new HttpContext();
    if (options?.silent) {
      context.set(SKIP_ERROR_TOAST, true);
    }
    return this.http.get<GroupSessionsResponse>(`${this.baseUrl}/guest/groups/${groupReservationId}/sessions`, {
      context,
    });
  }

  getOwnerSession$(reservationId: string): Observable<OwnerSessionResponse> {
    return this.http.get<OwnerSessionResponse>(`${this.baseUrl}/reservations/${reservationId}/guest-session-id`);
  }

  getOwnerSessionId$(reservationId: string): Observable<string> {
    return this.getOwnerSession$(reservationId).pipe(map((res) => res.guestSessionId));
  }

  getStatus$(groupReservationId: string, options?: { silent?: boolean }): Observable<GroupStatusResponse> {
    const context = new HttpContext();
    if (options?.silent) {
      context.set(SKIP_ERROR_TOAST, true);
    }
    return this.http.get<GroupStatusResponse>(`${this.baseUrl}/guest/groups/${groupReservationId}/status`, {
      context,
    });
  }

  submit$(groupReservationId: string, reservationId: string): Observable<SubmittedDish[]> {
    return this.http.post<SubmittedDish[]>(`${this.baseUrl}/group-reservations/${groupReservationId}/submit`, {
      reservationId,
    });
  }

  removeSession$(groupReservationId: string, guestSessionId: string): Observable<GroupSessionsResponse> {
    return this.http.delete<GroupSessionsResponse>(
      `${this.baseUrl}/guest/groups/${groupReservationId}/sessions/${guestSessionId}`
    );
  }
}
