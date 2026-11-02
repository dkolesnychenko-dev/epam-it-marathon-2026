import { CurrencyPipe, DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, computed, inject, OnInit, signal } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { forkJoin } from 'rxjs';
import { switchMap } from 'rxjs/operators';
import { ParticipantCardComponent } from '@app/components/group-orders/participant-card/participant-card.component';
import { GroupReservationStatus } from '@app/models/group-orders/group-status.model';
import { Participant } from '@app/models/group-orders/participant.model';
import { GuestInvite } from '@app/models/reservations/guest-invite.model';
import { GroupOrdersService } from '@app/services/group-orders/group-orders.service';
import { ToastService } from '@app/services/toast/toast.service';
import { ModalComponent } from '@app/shared/custom-components/modal/modal.component';
import { consolidatePreOrder } from '@app/shared/helpers/consolidate-pre-order';

@Component({
  selector: 'app-group-order-page',
  imports: [ParticipantCardComponent, ModalComponent, CurrencyPipe, DatePipe],
  templateUrl: './group-order-page.component.html',
  styleUrl: './group-order-page.component.scss',
  changeDetection: ChangeDetectionStrategy.Eager,
})
export class GroupOrderPageComponent implements OnInit {
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

  readonly consolidatedOrder = computed(() => consolidatePreOrder(this.participants()));

  readonly unsubmittedCount = computed(() => this.participants().filter((p) => !p.isSubmitted).length);

  readonly hasAnyDishes = computed(() => this.participants().some((p) => p.items.length > 0));

  get reservationId(): string {
    return this.route.snapshot.paramMap.get('reservationId') ?? '';
  }

  ngOnInit(): void {
    this.loadData();
  }

  loadData(): void {
    const resId = this.reservationId;
    if (!resId) {
      return;
    }

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
          this.participants.set(sessions.participants);
          this.status.set(status.status);
          this.ownerSessionId.set(ownerSessionId);
          this.loading.set(false);
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
        this.toastService.success('Pre-order sent to the restaurant');
      },
      error: () => {
        this.isSubmitting.set(false);
        this.showSubmitModal.set(false);
      },
    });
  }
}
