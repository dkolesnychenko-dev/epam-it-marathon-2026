# Homework: QA — Guest Reservation and Menu View (US11) 🧪

## Task Overview

Analyse the user story US11 (Guest Reservation and Menu View), find gaps in the requirements, cover the story with test cases, execute them against the implemented part of the application, and report the defects found.

## Links

- User story: https://drive.google.com/file/d/13MZpQFxvRLquSdIPF7-wPU95x8gAVElv/view?usp=sharing
- Figma design: https://www.figma.com/design/P9rkBNLJZ3WabXSQX9b5DO/EPAM-IT-Marathon-6.0?node-id=111683-86716
- Test case and defect report template: https://docs.google.com/spreadsheets/d/1nOY-mdac7DvBux4VyclO6BOyY456S51a7pEfb0LFWUI/edit?pli=1&gid=0#gid=0 (sheet "Test cases"; sheet "Defect reports" is gid=1194140512)
- Screenshot of the implemented part of the application: https://drive.google.com/file/d/1qWITmyQg35sTWr9MLnzWQs1DJ6wpp_OA/view?usp=drive_link
- My Frontend: https://d21fik7wr2oipe.cloudfront.net/main
- My Backend (dev): https://kdjjot9ejh.execute-api.eu-north-1.amazonaws.com/develop

## Tasks

### 1. Requirements analysis
Analyse the requirements, find gaps and defects in them, and describe them.

### 2. Test case design
Cover the user story with test cases using the template.

### 3. Test execution
Test the user story using the screenshot of the implemented part of the application.

### 4. Defect reports
Create defect reports using the template.

## User Story: US11_Guest Reservation and Menu View

| Field | Value |
|---|---|
| Epic | Order Management |
| US name | Guest Reservation and Menu View |
| Role | Guest (Visitor, unregistered) |
| Description | As a Guest, I want to see the full restaurant menu, so that I know what options are available and can decide what to choose. |

### Pre-conditions

- Guest has successfully joined the session (US_G10).
- The parent reservation or group session status is in Created/"Reserved".

### Acceptance Criteria

- The Guest can view the full restaurant menu structured logically by category (e.g., Appetizers, Main Course, Desserts).
- Each menu item must clearly display name, price, serving size in grams, image and current availability status.
- The Guest can add an available dish directly to their personal subcart by interacting with a clear action button (e.g., "Pre-order").
- Items currently marked "On Stop" are clearly indicated as unavailable and cannot be selected or added to the cart.
- The menu reflects the current data at the time the Guest opens or refreshes the screen.

## Context: how the guest reaches the menu

The owner creates the reservation and receives a group pre-order link. The owner sends this link to the guest. The guest opens the link, enters their name, and then sees the menu as a guest. US11 starts from this point.
