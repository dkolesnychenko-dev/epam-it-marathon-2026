# **EPAM IT-Marathon**

## **Workshop \#1 Homework**

**Module:** Requirement Engineering & User Stories

**Topic:** Group Pre-order Overview & Management (GreenTasty Project)

### **US\_New\_1: Group Pre-order Overview & Item Management by Owner**

| Epic | Order Management |
| :---- | :---- |
| **US Name** | Group Pre-order Overview and Item Management by Owner |
| **Role** | Customer (Owner) |
| **Description** | **As a** Reservation Owner, **I want to** view a consolidated group pre-order summary on a single screen and manage (add or remove) items within specific guests' subcarts, **so that** I can maintain full control over the final order structure, and verify the total bill before arriving at the restaurant. |

#### Pre-conditions:

* The Customer (Owner) is logged in.  
* The parent Reservation status is `"Reserved"`.  
* At least one Guest has added at least one item to the group pre-order session.

#### Acceptance Criteria (AC):

1. The system must display a group cart screen aggregated by participant, showing each Guest's display name, selected dishes, quantity, unit price, individual subtotal, and the overall group total sum.  
2. Each item within any Guest's subcart must feature a `"Delete"` icon, which immediately removes the selected dish and recalculates both the Guest's subtotal and the overall group total in real-time.  
3. Each Guest block must include an `"+ Add Item"` button that opens the restaurant menu and allows the Owner to add dishes directly to that specific Guest's personal subcart.

### **US\_New\_2: Guest Removal from Group Session by Owner** 

| Epic | Order Management |
| :---- | :---- |
| **US Name** | Guest Removal from Group Pre-order Session  |
| **Role** | Customer (Owner) |
| **Description** | **As a** Reservation Owner, **I want to** remove a guest and their associated subcart from the group pre-order session, **so that** I can keep the participant list and total bill accurate if a guest cancels their attendance.  |

#### Pre-conditions:

* The Customer (Owner) is logged in.  
* The Customer (Owner) is on the Group Pre-order Management screen.  
* At least one joined Guest is present in the session.  
* The parent Reservation status is `"Reserved"`.

#### Acceptance Criteria (AC):

1. Each Guest block must feature a `"Remove Guest"` action option.  
2. Clicking `"Remove Guest"` must trigger a confirmation modal: *"Are you sure you want to remove \[Guest Display Name\] and all their pre-ordered dishes from the group?"* with `"Cancel"` and `"Remove"` actions.  
3. Upon confirmation, the system must permanently remove the Guest and all items in their personal subcart, updating the total reservation sum and guest count in real-time.

### 

### **US\_New\_3A: One-Time Guest Edit Link Generation by Owner** 

| Epic | Order Management |
| :---- | :---- |
| **US Name** | One-Time Guest Edit Link Generation by Owner  |
| **Role** | Customer (Owner) |
| **Description** | **As a** Reservation Owner, **I want to** generate and share a single-use edit link for a specific guest upon their request, **so that** the guest can make a one-time adjustment to their personal pre-order.  |

#### Pre-conditions:

* The Customer (Owner) is logged in.  
* The Customer (Owner) is on the Group Pre-order Management screen.  
* The Guest has previously submitted a personal subcart in the group session.  
* The parent Reservation status is "Reserved".

#### Acceptance Criteria (AC):

1. Each Guest block on the Owner's management screen must feature an edit link management control (initially displayed as "Generate Edit Link").  
2. Clicking "Generate Edit Link" must create a unique single-use tokenized URL mapped to that Guest's SubcartID, automatically copy it to the Owner's clipboard, and display a notification: *"Edit link copied\! Share it with \[Guest Display Name\]"*.  
3. While the generated edit link remains unused by the Guest, the control button state must change to "Copy Edit Link", allowing the Owner to re-copy the same active link to the clipboard without creating a new token.  
4. Once the Guest successfully submits their modifications and consumes the token, the control state must automatically reset back to "Generate Edit Link", enabling the Owner to generate a new link if another edit request arises.  
5. The Owner's ability to directly add or remove items in the Guest's subcart (per US\_New\_1) must remain fully accessible even while an edit link is active or pending.  
6. The edit link generation and copying controls must be hidden if te parent Reservation status changes to "Completed" or "Cancelled".

### **US\_New\_3B: Single-Use Personal Pre-order Modification by Guest**


| Epic | Order Management |
| :---- | :---- |
| **US Name** | Single-Use Personal Pre-order Modification by Guest  |
| **Role** | Guest (Visitor, unregistered)  |
| **Description** | **As a** Guest, **I want to** open the shared one-time edit link to modify my pre-order items and confirm the updates, **so that** my order is updated once without remaining permanently open for late accidental edits.  |

#### Pre-conditions:

* The Guest has previously submitted a personal subcart in the group session.  
* The Guest has received a valid, active one\_time\_guest\_token generated by the Owner.  
* The parent Reservation status is "Reserved".

#### Acceptance Criteria (AC):

1. Opening the link must authenticate the Guest via the token without requiring registration and load their personal subcart pre-populated with all current items (including any recent edits made by the Owner).  
2. The Guest can adjust quantities, delete items, or navigate to the menu to add new items exclusively to their own subcart.  
3. The subcart screen must include a prominent "Confirm Changes" button.  
4. Clicking "Confirm Changes" must save the updated subcart, synchronize the changes with the Owner's consolidated overview screen in real-time, and immediately consume (deactivate) the one\_time\_guest\_token.  
5. Any subsequent attempt to access or submit changes using a consumed token must display a static page: *"This edit link has already been used or is no longer active."*  
6. If the parent Reservation is cancelled or completed before the Guest submits their changes, the session token must immediately expire.