# Homework Design — Owner Flow (Figma Export)

Original: [Figma — EPAM IT Marathon 6.0](https://www.figma.com/design/P9rkBNLJZ3WabXSQX9b5DO/EPAM-IT-Marathon-6.0?node-id=62645-20592) → page *FOR DEVELOPERS* → section **Owner Flow**. These PNGs represent the same screens so you don't have to look for them in Figma.

| File | Screen | Level |
|---|---|---|
| `01-owner-review.png` | Owner sees all participants' carts, Grand Total, Submit Pre-Order button | 1 (layout), 2 |
| `02-sent-to-restaurant.png` | After submission: "Sent to Restaurant" badge, button is disabled | 2 |
| `03-remove-guest.png` | Remove Guest dialog; reservation card with "Group Pre-Order" button in the background | 3 (bonus), 2 (button) |

## What from the Mockup is **Not** Part of the Homework

- The "-/+" and trash buttons next to items, and "Edit my Pre-Order": the backend cannot edit other participants' carts. Display the quantity as plain text (`× 2` or `Qty: 2`).
- The notification "Maria has filled her pre-order…" — replaced with the yellow banner from `homework-frontend.md`: "N participants haven't submitted their cart yet…".
- The screen opens as a separate page (`/reservations/:id/group-order`), not as a modal over the reservation list. If you want a modal, that's fine, but it is not required.

## What from the Mockup is **Required**

- Header: "Group Pre-Order", status badge, address, date, time, and number of guests with icons.
- Card for each participant: "My Pre-Order" / "Guest: Alex", item row (photo or first letter on a green circle, title, quantity, price), Subtotal.
- Grand Total in green and a wide green "Submit Pre-Order" button; disabled state is gray.
- Guest cart status as text (Submitted / Still choosing): it is not in the mockup, but without it the Owner won't know whose items will be included in the order.
