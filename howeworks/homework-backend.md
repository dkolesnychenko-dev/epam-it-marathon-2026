# Homework: Backend — Group Pre-Order Management API 🚀

## Task Overview

The goal is to implement a set of API endpoints to manage group pre-orders. This includes allowing the group owner to view all participants' sessions, manage their own session, submit a consolidated order, and remove guest sessions from the group.

## Requirements

### Level 1: Guest Sessions List
**GET `/guest/groups/{groupReservationId}/sessions`**

**Description:**
The Owner should be able to see which participants have joined the group order, what dishes they have added, the total amount for each participant, and the total amount for all participants.

**Response Example:**
```json
{
  "participants": [
    {
      "id": "session1",
      "type": "Guest",
      "name": "Andrii",
      "items": [
        {
          "dish": {
            "id": "dish1",
            "name": "Caesar Salad",
            "price": 180
          },
          "quantity": 2
        }
      ],
      "total": 360,
      "isSubmitted": true
    },
    {
      "id": "session2",
      "type": "Guest",
      "name": "Olena",
      "items": [
        {
          "dish": {
            "id": "dish2",
            "name": "Tom Yum",
            "price": 220
          },
          "quantity": 1
        }
      ],
      "total": 220,
      "isSubmitted": false
    }
  ],
  "total": 580
}
```

---

### Level 2: Owner Session & Consolidated Submit

#### 1. Get Owner Session ID
**GET `/reservations/{reservationId}/guest-session-id`**

**Response Example:**
```json
{
  "guestSessionId": "owner-session-123"
}
```

#### 2. Submit Consolidated Pre-order
**POST `/group-reservations/{groupReservationId}/submit`**

**Request Example:**
```json
{
  "reservationId": "reservation-123"
}
```

**Description:**
After reviewing all participants' orders, the Owner must confirm the consolidated pre-order to be sent to the restaurant.

**Submission logic:**
- Take only `submitted` guest sessions.
- Aggregate all dishes into a single list.
- Merge identical dishes by `dishId` (sum quantities).
- Add items to the order.
- Change order status to `SubmittedByCustomer`.
- Change group reservation status to `Submitted`.

**Response Example:**
```json
[
  {
    "dishId": "dish1",
    "quantity": 3
  },
  {
    "dishId": "dish2",
    "quantity": 2
  }
]
```

**Conditions:**
- Only the Owner can call these endpoints.
- Return 404 if the reservation or group reservation is not found.
- Return 400 if there are no `submitted` sessions.
- Return 400 if submitted sessions contain no dishes.
- Return 400 if the order is not found.

---

### Level 3: Guest Session Removal
**DELETE `/guest/groups/{groupReservationId}/sessions/{guestSessionId}`**

**Description:**
The Owner can remove a specific participant's session from the group order.

**Response Example:**
```json
{
  "participants": [
    {
      "id": "session2",
      "type": "Guest",
      "name": "Olena",
      "items": [
        {
          "dish": {
            "id": "dish2",
            "name": "Tom Yum",
            "price": 220
          },
          "quantity": 1
        }
      ],
      "total": 220,
      "isSubmitted": false
    }
  ],
  "total": 220
}
```

**Conditions:**
- Only the Owner can delete a guest session.
- Return 400 if the guest session does not belong to this group reservation.
- Return 404 if the guest session is not found.
- Return the updated group snapshot after deletion.
