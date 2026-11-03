import { CurrencyPipe, DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, computed, inject, OnDestroy, OnInit, signal } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { EMPTY, forkJoin, merge, Observable, Subject, Subscription, timer } from 'rxjs';
import { catchError, exhaustMap, finalize, startWith, switchMap, tap } from 'rxjs/operators';
import { ParticipantCardComponent } from '@app/components/group-orders/participant-card/participant-card.component';
import { GroupReservationStatus } from '@app/models/group-orders/group-status.model';
import { Participant } from '@app/models/group-orders/participant.model';
import { GuestInvite } from '@app/models/reservations/guest-invite.model';
import { GroupOrdersService } from '@app/services/group-orders/group-orders.service';
import { ToastService } from '@app/services/toast/toast.service';
import { ModalComponent } from '@app/shared/custom-components/modal/modal.component';
import { consolidatePreOrder } from '@app/shared/helpers/consolidate-pre-order';

const SYNC_INTERVAL_MS = 15_000;

@Component({
  selector: 'app-group-order-page',
  imports: [ParticipantCardComponent, ModalComponent, CurrencyPipe, DatePipe],
  templateUrl: './group-order-page.component.html',
  styleUrl: './group-order-page.component.scss',
  changeDetection: ChangeDetectionStrategy.Eager,
})
export class GroupOrderPageComponent implements OnInit, OnDestroy {
  private readonly route = inject(ActivatedRoute);
  private readonly groupOrdersService = inject(GroupOrdersService);
  private readonly toastService = inject(ToastService);

  readonly invite = signal<GuestInvite | null>(null);
  readonly participants = signal<Participant[]>([]);
  readonly ownerSessionId = signal<string>('');
  readonly status = signal<GroupReservationStatus>('Open');

  readonly loading = signal<boolean>(false);
  readonly error = signal<'notFound' | 'general' | null>(null);
  readonly isSubmitting = signal<boolean>(false);
  readonly showSubmitModal = signal<boolean>(false);

  readonly guestToRemove = signal<Participant | null>(null);
  readonly isRemoving = signal<boolean>(false);

  readonly lastUpdatedAt = signal<Date | null>(null);
  readonly syncError = signal<boolean>(false);
  readonly isRefreshing = signal<boolean>(false);

  private readonly refresh$ = new Subject<void>();
  private pollSubscription?: Subscription;

  readonly consolidatedOrder = computed(() => consolidatePreOrder(this.participants()));

  readonly unsubmittedCount = computed(() => this.participants().filter((p) => !p.isSubmitted).length);

  readonly hasAnyDishes = computed(() => this.participants().some((p) => p.items.length > 0));

  get reservationId(): string {
    return this.route.snapshot.paramMap.get('reservationId') ?? '';
  }

  ngOnInit(): void {
    this.loadData();
  }

  ngOnDestroy(): void {
    this.stopPolling();
  }

  loadData(): void {
    const resId = this.reservationId;
    if (!resId) {
      return;
    }

    this.stopPolling();
    this.loading.set(true);
    this.error.set(null);

    this.groupOrdersService
      .getGroupInvite$(resId)
      .pipe(
        switchMap((invite) => {
          this.invite.set(invite);
          const groupId = invite.GroupReservationId;
          return forkJoin({
            sessions: this.groupOrdersService.getSessions$(groupId),
            status: this.groupOrdersService.getStatus$(groupId),
            ownerSessionId: this.groupOrdersService.getOwnerSessionId$(resId),
          });
        })
      )
      .subscribe({
        next: ({ sessions, status, ownerSessionId }) => {
          this.ownerSessionId.set(ownerSessionId);
          this.setParticipants(sessions.participants, ownerSessionId);
          this.status.set(status.status);
          this.lastUpdatedAt.set(new Date());
          this.syncError.set(false);
          this.loading.set(false);

          if (status.status === 'Open') {
            this.startPolling(this.invite()!.GroupReservationId);
          }
        },
        error: (err: HttpErrorResponse) => {
          this.loading.set(false);
          if (err.status === 404) {
            this.error.set('notFound');
          } else {
            this.error.set('general');
          }
        },
      });
  }

  private startPolling(groupId: string): void {
    this.stopPolling();

    const manual$ = this.refresh$;
    const auto$ = this.refresh$.pipe(
      startWith(null),
      switchMap(() => timer(SYNC_INTERVAL_MS, SYNC_INTERVAL_MS))
    );

    this.pollSubscription = merge(manual$, auto$)
      .pipe(
        exhaustMap(() => this.syncData$(groupId))
      )
      .subscribe();
  }

  private stopPolling(): void {
    if (this.pollSubscription) {
      this.pollSubscription.unsubscribe();
      this.pollSubscription = undefined;
    }
  }

  private syncData$(groupId: string): Observable<unknown> {
    this.isRefreshing.set(true);
    return forkJoin({
      sessions: this.groupOrdersService.getSessions$(groupId, { silent: true }),
      status: this.groupOrdersService.getStatus$(groupId, { silent: true }),
    }).pipe(
      tap({
        next: ({ sessions, status }) => {
          this.setParticipants(sessions.participants, this.ownerSessionId());
          this.status.set(status.status);
          this.lastUpdatedAt.set(new Date());
          this.syncError.set(false);
          if (status.status !== 'Open') {
            this.stopPolling();
          }
        },
        error: () => {
          this.syncError.set(true);
        },
      }),
      finalize(() => {
        this.isRefreshing.set(false);
      }),
      catchError(() => EMPTY)
    );
  }

  onRefresh(): void {
    if (this.status() === 'Open' && !this.isRefreshing()) {
      this.refresh$.next();
    }
  }

  openSubmitModal(): void {
    this.showSubmitModal.set(true);
  }

  closeSubmitModal(): void {
    if (!this.isSubmitting()) {
      this.showSubmitModal.set(false);
    }
  }

  confirmSubmit(): void {
    if (this.isSubmitting()) {
      return;
    }

    const invite = this.invite();
    if (!invite) {
      return;
    }

    this.isSubmitting.set(true);
    this.groupOrdersService.submit$(invite.GroupReservationId, this.reservationId).subscribe({
      next: () => {
        this.isSubmitting.set(false);
        this.showSubmitModal.set(false);
        this.status.set('Submitted');
        this.stopPolling();
        this.toastService.success('Pre-order sent to the restaurant');
      },
      error: () => {
        this.isSubmitting.set(false);
        this.showSubmitModal.set(false);
      },
    });
  }

  openRemoveGuestModal(guest: Participant): void {
    this.guestToRemove.set(guest);
  }

  closeRemoveModal(): void {
    if (!this.isRemoving()) {
      this.guestToRemove.set(null);
    }
  }

  confirmRemoveGuest(): void {
    if (this.isRemoving()) {
      return;
    }

    const guest = this.guestToRemove();
    const invite = this.invite();
    if (!guest || !invite) {
      return;
    }

    this.isRemoving.set(true);
    this.groupOrdersService.removeSession$(invite.GroupReservationId, guest.id).subscribe({
      next: (res) => {
        this.isRemoving.set(false);
        this.guestToRemove.set(null);
        this.setParticipants(res.participants);
        this.toastService.success(`Removed ${guest.name} from group order`);
      },
      error: () => {
        this.isRemoving.set(false);
        this.guestToRemove.set(null);
      },
    });
  }

  private setParticipants(participants: Participant[], ownerId?: string): void {
    const idToMatch = ownerId || this.ownerSessionId();
    const unique = participants.filter((p, index, self) => {
      if (p.type === 'Owner') {
        const ownerIndex = self.findIndex((x) =>
          x.type === 'Owner' && (idToMatch ? x.id === idToMatch : true)
        );
        return index === (ownerIndex >= 0 ? ownerIndex : self.findIndex((x) => x.type === 'Owner'));
      }
      return true;
    });
    this.participants.set(unique);
  }
}
