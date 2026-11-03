import {
  ChangeDetectionStrategy,
  Component,
  inject,
  input,
  output,
} from '@angular/core';
import { Router } from '@angular/router';

import { Order } from '@app/models/orders/order.model';

@Component({
  selector: 'app-submit-order-modal',
  standalone: true,
  imports: [],
  templateUrl: './submit-order-modal.component.html',
  styleUrl: './submit-order-modal.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SubmitOrderModalComponent {
  private readonly router = inject(Router);

  readonly order = input.required<Order>();

  readonly canceled = output<void>();
  readonly edited = output<void>();
  readonly closed = output<void>();

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
