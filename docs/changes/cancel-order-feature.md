# Cancel Order — frontend spec

Backend change adding a "Cancel order" action. This doc is the contract for
building the UI against — endpoint, auth, request/response shapes, and the
one new status value to handle.

## Endpoint

```
POST /order/orders/{id}/cancel
```

(through the Gateway, same as every other Order route — no separate host/port)

- **Auth**: `Authorization: Bearer <jwt>` required, same as the rest of
  `/order/*`.
- **Body**: none.
- **Who can call it**: the order's owner, or a user with the `Admin` role.
  Anyone else gets a `404` (same "don't reveal it exists" behavior as
  `GET /order/orders/{id}` for orders you don't own).

## Precondition: order must be `Reserved`

Cancel is only allowed while the order is in `Reserved` status. A `Pending`
order hasn't finished being processed by Inventory yet, and `Rejected` /
`Canceled` are already terminal — none of those are cancelable.

**Important for the UI**: only show/enable a "Cancel" button when
`order.status === "Reserved"`. Don't rely on the API call to gate this —
show the button conditionally so users don't hit a 409 for an action that was
never going to work.

## This call is synchronous

Order creation is asynchronous — after `POST /orders` the order comes back
`Pending` and only later flips to `Reserved`/`Rejected` once Inventory
processes it (poll or re-fetch to see that). **Cancel is not like that.** The
order's status is flipped to `Canceled` in the same request/response — no
polling needed. A `200` response already contains the final state.

(The restock side-effect — telling Inventory to give the reserved quantities
back — happens asynchronously after the response, but that only affects
Inventory's stock numbers, not this order's status.)

## Responses

### `200 OK` — canceled

Body is the updated order, same shape as every other order response:

```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "customerId": "9c858901-8a57-4791-81fe-4c455b099bc9",
  "status": "Canceled",
  "rejectionReason": null,
  "createdAt": "2026-07-10T12:00:00Z",
  "updatedAt": "2026-07-13T09:15:00Z",
  "items": [
    { "id": "...", "productName": "Widget", "quantity": 2 }
  ]
}
```

### `404 Not Found`

The order doesn't exist, or exists but isn't visible to the caller (not the
owner and not an admin). Empty body.

### `409 Conflict` — wrong status

The order isn't currently `Reserved` (already `Canceled`, `Rejected`, or
still `Pending`). `ProblemDetails` body:

```json
{
  "title": "Order cannot be canceled while in 'Pending' status.",
  "status": 409
}
```

(Same `ProblemDetails` shape already used for the idempotency-conflict 409 on
`POST /orders` — render it the same way.)

### `401 Unauthorized`

Missing/invalid/expired bearer token — same as every other authenticated
route.

## New status value: `Canceled`

`order.status` can now be `"Pending"`, `"Reserved"`, `"Rejected"`, or
`"Canceled"`. Anywhere the UI renders or filters on status (order list,
order detail, status badges/colors), add a case for `Canceled`. Treat it as
terminal, same as `Rejected` — no further actions apply to it.
