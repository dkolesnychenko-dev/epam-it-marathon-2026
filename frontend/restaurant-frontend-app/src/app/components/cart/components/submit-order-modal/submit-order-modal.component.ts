import {
  ChangeDetectionStrategy,
  Component,
  OnInit,
  inject,
  input,
  output,
  signal,
} from '@angular/core';
import { Router } from '@angular/router';

import { Order } from '@app/models/orders/order.model';
import { ReservationService } from '@app/services/reservation/reservation.service';

@Component({
  selector: 'app-submit-order-modal',
  standalone: true,
  imports: [],
  templateUrl: './submit-order-modal.component.html',
  styleUrl: './submit-order-modal.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SubmitOrderModalComponent implements OnInit {
  private readonly router = inject(Router);
  private readonly reservationService = inject(ReservationService, { optional: true });

  readonly order = input.required<Order>();
  readonly guestCount = input<number | undefined>();

  readonly hasMultipleGuests = signal<boolean>(false);

  readonly canceled = output<void>();
  readonly edited = output<void>();
  readonly closed = output<void>();

  ngOnInit(): void {
    if (this.guestCount() !== undefined) {
      this.hasMultipleGuests.set(this.guestCount()! > 1);
      return;
    }

    const reservationId = this.order()?.reservationId;
    if (reservationId && this.reservationService) {
      this.reservationService.getReservationById(reservationId).subscribe({
        next: (res) => {
          this.hasMultipleGuests.set(res.guestCount > 1);
        },
        error: () => {
          this.hasMultipleGuests.set(false);
        },
      });
    }
  }

  cancelOrder(): void {
    this.canceled.emit();
  }

  editOrder(): void {
    this.edited.emit();
  }

  closeModal(): void {
    this.closed.emit();
  }

  goToGroupOrder(): void {
    this.closeModal();
    if (this.order().reservationId) {
      void this.router.navigate(['/reservations', this.order().reservationId, 'group-order']);
    }
  }
}
