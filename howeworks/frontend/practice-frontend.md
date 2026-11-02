# Practical Assignment — Frontend (~15 min)

**What we will build:** a group pre-order participant card and a page displaying all participants. This is the first building block of the homework: in Level 1, you will simply style and align this page with the design mockup.

**Data is already in the archive:** `src/app/shared/mocks/group-orders/group-order.preview.ts` → `GROUP_ORDER_PREVIEW.sessions.participants`. These are 4 participants: an Owner and 3 guests with carts.

All paths below are relative to `frontend/restaurant-frontend-app/src/app/`.

---

## Step 0 · Tests (1 min)

The tests are already written — they define what the card should be capable of. From the `frontend/restaurant-frontend-app` directory:

```bash
cp -R homework-tests/practice/. src/app/          # Windows: Copy-Item -Recurse -Force homework-tests\practice\* src\app\
npm run test:hw
```

The tests will fail to compile — the component doesn't exist yet. That is normal. After each step, run `npm run test:hw` and watch the groups "step 1", "step 3", and "step 4" turn green.

## Step 1 · Card Component (3 min)

Create `components/group-orders/participant-card/participant-card.component.ts`:

```ts
import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { Participant } from '@app/models/group-orders/participant.model';

@Component({
  selector: 'app-participant-card',
  templateUrl: './participant-card.component.html',
  changeDetection: ChangeDetectionStrategy.Eager,
})
export class ParticipantCardComponent {
  // what the card receives from outside — like a function parameter
  readonly participant = input.required<Participant>();

  // automatically computed whenever participant changes
  readonly title = computed(() => {
    const person = this.participant();
    return person.type === 'Owner' ? `Owner: ${person.name}` : `Guest: ${person.name}`;
  });
}
```

and the template `participant-card.component.html`:

```html
<article class="card">
  <h2 class="title">{{ title() }}</h2>
</article>
```

## Step 2 · Page and Route (3 min)

`pages/group-order-page/group-order-page.component.ts`:

```ts
import { ChangeDetectionStrategy, Component, signal } from '@angular/core';
import { GROUP_ORDER_PREVIEW } from '@app/shared/mocks/group-orders/group-order.preview';
import { ParticipantCardComponent } from '@app/components/group-orders/participant-card/participant-card.component';

@Component({
  selector: 'app-group-order-page',
  imports: [ParticipantCardComponent],
  templateUrl: './group-order-page.component.html',
  changeDetection: ChangeDetectionStrategy.Eager,
})
export class GroupOrderPageComponent {
  // static data for now; in Level 2 it will be replaced by the API response
  readonly participants = signal(GROUP_ORDER_PREVIEW.sessions.participants);
}
```

`group-order-page.component.html`:

```html
<main class="page">
  <h1>Group Pre-Order</h1>

  <ul class="participants">
    @for (person of participants(); track person.id) {
      <li><app-participant-card [participant]="person" /></li>
    }
  </ul>
</main>
```

In `app.routes.ts`, under `children` of the main layout (next to `join/:token`):

```ts
{
  path: 'reservations/:reservationId/group-order',
  canActivate: [AuthGuard],
  data: { authorized: true },
  loadComponent: () =>
    import('./pages/group-order-page/group-order-page.component').then((m) => m.GroupOrderPageComponent),
},
```

Open `http://localhost:4200/reservations/demo-reservation/group-order?demo` (`?demo` is a mock login, no account needed): four headings "Owner: Yevhenii", "Guest: Kateryna"…

## Step 3 · Dishes and Subtotal (4 min)

Add `imports: [CurrencyPipe]` (from `@angular/common`) to the card component and expand the template:

```html
<article class="card">
  <h2 class="title">{{ title() }}</h2>

  @if (participant().items.length) {
    <ul class="items">
      @for (item of participant().items; track item.dish.id) {
        <li class="item">
          @if (item.dish.imageUrl) {
            <img class="avatar" [src]="item.dish.imageUrl" alt="" width="40" height="40" loading="lazy" />
          } @else {
            <span class="avatar initial" aria-hidden="true">{{ item.dish.name.charAt(0) }}</span>
          }
          <p class="name">{{ item.dish.name }}</p>
          <p class="qty">× {{ item.quantity }}</p>
          <p class="price">{{ item.dish.price * item.quantity | currency }}</p>
        </li>
      }
    </ul>
  } @else {
    <p class="empty">No dishes yet</p>
  }

  <p class="subtotal"><span>Subtotal</span><span>{{ participant().total | currency }}</span></p>
</article>
```

`width="40" height="40"` is required: dish photos are large (up to several MBs and thousands of pixels), and styles are not yet added in the practice. These attributes immediately define dimensions and reserve space, preventing layout shifts during page loading (CLS). `loading="lazy"` ensures photos load only when they enter the viewport.

## Step 4 · Status as Text (2 min)

Below `<h2>` — the cart status. Specifically **as text**, not just color:

```html
@if (participant().isSubmitted) {
  <span class="chip submitted">Submitted</span>
} @else {
  <span class="chip pending">Still choosing</span>
}
```

## Step 5 · All Tests Green (2 min)

```bash
npm run test:hw
```

Expected output: **8 card tests are green** (plus the `shared/helpers` tests that were already in the archive).

Open `src/app/components/group-orders/participant-card/participant-card.component.spec.ts` and see how it is structured:
- `TestBed.createComponent(ParticipantCardComponent)` — instantiates the card independently of the page;
- `fixture.componentRef.setInput('participant', OLENA)` — how inputs are provided in tests;
- we verify what the user sees: header text, prices, status, photo dimensions.

## Complete If

- [ ] The page displays 4 cards with dishes, prices, and Subtotals;
- [ ] The status is clearly displayed as text;
- [ ] `npm run test:hw` — 8 card tests are green.

Styling is not required during the practical assignment — that is part of Level 1 of the homework.
