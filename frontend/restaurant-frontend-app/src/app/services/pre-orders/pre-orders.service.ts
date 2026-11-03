import { inject, Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { API_BASE_URL } from '@app/shared/config/api-base-url.token';
import { Order } from '@app/models/orders/order.model';
import { DishListResponse } from '@app/models/dishes/dish-list-response.model';
import { catchError, map, of, switchMap, tap, throwError } from 'rxjs';
import { CreateOrderRequest } from '@app/models/cart/create-order-request.model';

@Injectable({
  providedIn: 'root',
})
export class PreOrdersService {
  private readonly baseUrl = inject(API_BASE_URL);
  private readonly http = inject(HttpClient);

  private readonly ordersCache = new Map<string, Order>();

  getPreOrders() {
    return this.http.get<Order[]>(`${this.baseUrl}/orders`).pipe(
      tap((orders) => {
        for (const order of orders) {
          if (order?.reservationId) {
            this.ordersCache.set(order.reservationId, order);
          }
        }
      })
    );
  }

  getByReservationId(reservationId: string) {
    return this.http.get<Order>(`${this.baseUrl}/orders/${reservationId}`).pipe(
      tap((order) => {
        if (order?.reservationId) {
          this.ordersCache.set(order.reservationId, order);
        }
      })
    );
  }

  addDishToPreOrderByReservationId(
    reservationId: string,
    dish: DishListResponse,
    quantity: number
  ) {
    const cachedOrder = this.ordersCache.get(reservationId);
    const orderSource$ = cachedOrder
      ? of([cachedOrder])
      : this.getPreOrders().pipe(catchError(() => of([])));

    return orderSource$.pipe(
      map((orders) => {
        const order =
          orders.find((o) => o.reservationId === reservationId) ??
          this.ordersCache.get(reservationId);
        if (!order) {
          return null;
        }

        const existingItem = order.orderItems.find((item) => item.dish.id === dish.id);
        if (existingItem) {
          existingItem.quantity += quantity;
        } else {
          order.orderItems.push({ dish, quantity, state: 0 });
        }
        return order;
      }),
      switchMap((order) => {
        if (!order) {
          const orderCreateRequest: CreateOrderRequest = {
            reservationId,
            orderItems: [{ dish, quantity, state: 0 }],
          };
          return this.createPreOrder(orderCreateRequest).pipe(
            catchError((err) => {
              if (err?.status === 400) {
                return this.getByReservationId(reservationId).pipe(
                  switchMap((existingOrder) => {
                    const existingItem = existingOrder.orderItems.find(
                      (item) => item.dish.id === dish.id
                    );
                    if (existingItem) {
                      existingItem.quantity += quantity;
                    } else {
                      existingOrder.orderItems.push({ dish, quantity, state: 0 });
                    }
                    return this.editPreOrderByReservationId(reservationId, existingOrder);
                  })
                );
              }
              return throwError(() => err);
            })
          );
        }

        if (order.status === 1 || order.status === 3) {
          return this.unsubmitPreOrderByReservationId(reservationId).pipe(
            catchError(() => of(null)),
            switchMap(() => {
              order.status = 0;
              return this.editPreOrderByReservationId(reservationId, order);
            })
          );
        }

        return this.editPreOrderByReservationId(reservationId, order);
      })
    );
  }

  createPreOrder(order: CreateOrderRequest) {
    const createOrderRequest = {
      reservationId: order.reservationId,
      orderItems: order.orderItems.map((item) => ({
        dishId: item.dish.id,
        quantity: item.quantity,
      })),
    };

    return this.http.post<Order>(`${this.baseUrl}/orders`, createOrderRequest).pipe(
      tap((createdOrder) => {
        if (createdOrder?.reservationId) {
          this.ordersCache.set(createdOrder.reservationId, createdOrder);
        }
      })
    );
  }

  editPreOrderByReservationId(reservationId: string, order: Order) {
    const orderItems = order.orderItems.map((item) => ({
      dishId: item.dish.id,
      quantity: item.quantity,
    }));

    if (orderItems.length === 0) {
      return this.cancelPreOrderByReservationId(reservationId);
    }

    return this.http.put(`${this.baseUrl}/orders/${reservationId}`, { orderItems }).pipe(
      tap(() => {
        this.ordersCache.set(reservationId, order);
      })
    );
  }

  cancelPreOrderByReservationId(reservationId: string) {
    return this.http.delete(`${this.baseUrl}/orders/${reservationId}`).pipe(
      tap(() => {
        this.ordersCache.delete(reservationId);
      })
    );
  }

  submitPreOrderByReservationId(reservationId: string) {
    return this.http.post(`${this.baseUrl}/orders/${reservationId}/submit`, {}).pipe(
      tap(() => {
        const cached = this.ordersCache.get(reservationId);
        if (cached) {
          cached.status = 1;
        }
      })
    );
  }

  unsubmitPreOrderByReservationId(reservationId: string) {
    return this.http.post(`${this.baseUrl}/orders/${reservationId}/unsubmit`, {}).pipe(
      tap(() => {
        const cached = this.ordersCache.get(reservationId);
        if (cached) {
          cached.status = 0;
        }
      })
    );
  }

  confirmDishInOrder(reservationId: string, dishId: string) {
    return this.http.patch(
      `${this.baseUrl}/reservations/${reservationId}/order/confirmations/${dishId}`,
      {}
    );
  }

  rejectDishInOrder(reservationId: string, dishId: string) {
    return this.http.patch(
      `${this.baseUrl}/reservations/${reservationId}/order/cancellations/${dishId}`,
      {}
    );
  }
}
