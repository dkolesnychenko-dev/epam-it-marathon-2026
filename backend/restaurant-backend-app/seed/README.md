# Restaurant System — Database Seeding & Reset Guide

This directory contains seed data, automated tools, and cleanup scripts to populate and manage the DynamoDB database and Cognito users for the Restaurant management application.

---

## 📌 1. Data Schema & Models

### 🏢 1.1 Locations (`Location`)
DynamoDB Table: **`Locations`** (Hash Key: `id`, String).

| Field | C# / DynamoDB Type | Description | Example |
|---|---|---|---|
| `id` | `string` / `S` | Unique location GUID | `"11111111-1111-1111-1111-111111111111"` |
| `address` | `string` / `S` | Street address of the restaurant branch | `"9 Irakli Abashidze Street"` |
| `description` | `string` / `S` | Description of venue, ambiance, and cuisine | `"An elegant dining space in the heart of Vake..."` |
| `totalCapacity` | `int` / `N` | Seating/table capacity (see dynamic calculation below) | `6` |
| `averageOccupancy` | `string` / `S` | Daily occupancy percentage (0% to 100%) | `"65%"` |
| `imageUrl` | `string?` / `S` | S3 public URL or frontend asset path | `"assets/img/locations/abashidze-street-9.jpg"` |
| `rating` | `double` / `N` | Location average review score | `4.8` |
| `specialtyDishIds` | `List<string>` / `L` | Array of dish IDs available at this location | `["a111...", "a222..."]` |

> ⚠️ **Important: Dynamic Calculations for `totalCapacity` and `rating`**
> In `GetLocationsHandler.cs` and `GetLocationByIdHandler.cs`, these values are recalculated dynamically at query time:
> 1. **`totalCapacity`** is calculated by counting the number of table records in the **`Tables`** table where `locationId` matches. If no tables exist, the API returns `0`.
> 2. **`rating`** is calculated as the arithmetic average of ratings in the **`Feedback`** table for this `locationId`. If no feedbacks exist, the API returns `0`.
> 
> *The seed dataset includes corresponding `Tables` and `Feedback` entries so that restaurant branches show accurate capacity and star ratings in the UI.*

---

### 🥗 1.2 Dishes (`Dish`)
DynamoDB Table: **`Dishes`** (Hash Key: `id`, String).

| Field | C# Type | DynamoDB Type | Description |
|---|---|---|---|
| `id` | `string` | `S` | Unique dish GUID |
| `name` | `string` | `S` | Dish title |
| `description` | `string` | `S` | Ingredients and culinary description |
| `dishType` | `DishType` (Enum) | **`N` (Integer)** | `0` = `"Appetizer"`, `1` = `"MainCourse"`, `2` = `"Dessert"` |
| `price` | `decimal` | `N` | Price in USD (e.g., `16.50`) |
| `weight` | `string` | `S` | Serving weight (e.g., `"320g"`) |
| `calories` | `string` | `S` | Energy value (e.g., `"410"`) |
| `proteins` | `string` | `S` | Protein content (e.g., `"9g"`) |
| `fats` | `string` | `S` | Fat content (e.g., `"28g"`) |
| `carbohydrates` | `string` | `S` | Carbohydrate content (e.g., `"32g"`) |
| `vitamins` | `string` | `S` | Vitamins & minerals (e.g., `"A, B6, C, E, K"`) |
| `imageUrl` | `string?` | `S` | Relative asset path or public S3 URL |
| `state` | `DishState` (Enum) | **`N` (Integer)** | `0` = `"OnStop"`, `1` = `"Available"`, `2` = `"OutOfStock"` |
| `locationId` | `string` | `S` | Primary restaurant location ID |

---

### 🧑‍🍳 1.3 Waiters (`WaiterListItem`)
DynamoDB Table: **`Waiters`** (Hash Key: `userId`, String).
Global Secondary Index: **`locationId-index`** (Partition Key: `locationId`, String).

| Field | DynamoDB Type | Description |
|---|---|---|
| `userId` | `S` | Waiter unique identifier (GUID) |
| `locationId` | `S` | Restaurant location ID where the waiter works (GSI) |
| `createdAt` | `S` | ISO8601 creation timestamp |
| `reservations` | `L` | Array of reservation IDs assigned to this waiter |

> ⚠️ **Why Waiters are Required for Reservations**:
> When any customer creates a reservation (`POST /reservations`), the backend queries the `Waiters` table using the `locationId-index` GSI to find the least busy waiter (`GetLeastBusyWaiterAsync`). If no waiters are registered for that location, the reservation is rejected with:
> `ValidationException: "No waiters found for location ID <locationId>."`
> The seeder assigns 2 waiters for each of the 3 restaurant branches with an empty `reservations: []` list.

---

## 🖼️ 2. Dish Photos & Images Guide

### 2.1 Specifications
The frontend clips dish images into circles (`border-radius: 50%`, `aspect-ratio: 1 / 1`, `object-fit: cover`):
* **Aspect Ratio**: `1:1` (Square). Keep the dish centered so edges are not cut off.
* **Resolution**: `800x800 px` recommended (any square between `600x600` and `1000x1000 px`).
* **Format**: `.jpg` (JPEG) or `.png`.
* **File Size**: Between `150 KB` and `800 KB`.
* **Folder Location**: `frontend/restaurant-frontend-app/src/assets/img/dishes/`

### 2.2 How Photo Deployment Works
* **Local Development**:
  When running `npm start` in the frontend, Angular serves static assets directly from `src/assets/`. Any image dropped into `src/assets/img/dishes/` is immediately available in the browser without any redeployment.
* **Production Deployment**:
  When building the frontend with `npm run build`, all assets from `src/assets/` are automatically packaged into the build output directory (`dist/`). Deploying the frontend to S3 / CloudFront automatically includes these images.
* **Fallback Behavior**:
  If a dish does not have an image file yet, the UI displays `assets/img/dish.png` as a fallback.

### 2.3 How to Add or Change a Photo
1. Save your image into:
   ```text
   frontend/restaurant-frontend-app/src/assets/img/dishes/<image-name>.jpg
   ```
2. Reference the path in `data/dishes.json` and `data/dynamodb-batch/batch-dishes.json`:
   ```json
   "imageUrl": "assets/img/dishes/<image-name>.jpg"
   ```

### 2.4 Pushing Photos to an Already Deployed Frontend (S3 & CloudFront)
If the frontend is already deployed and you want to upload new/updated photos without a full Angular rebuild:

#### Quick S3 Sync (Fastest — 5 seconds):
```powershell
# 1. Get your AWS Account ID (if not already set):
$ACCOUNT_ID = aws sts get-caller-identity --query Account --output text

# 2. Sync local dishes images directly to the deployed frontend S3 bucket:
aws s3 sync ../../../frontend/restaurant-frontend-app/src/assets/img/dishes s3://restaurant-frontend-$ACCOUNT_ID/assets/img/dishes/

# 3. (Optional) Invalidate CloudFront cache if you replaced existing images:
$DIST_ID = aws cloudfront list-distributions --query "DistributionList.Items[?DefaultRootObject=='index.html'].Id | [0]" --output text
aws cloudfront create-invalidation --distribution-id $DIST_ID --paths "/assets/img/dishes/*"
```

---

## 🚀 3. How to Push Seed Data

Writes Locations, Dishes, Tables, Feedback, and Waiters directly to DynamoDB in batches. It takes under 3 seconds and ensures all 3 restaurant branches have tables and waiters ready for reservations.

Ensure AWS CLI is configured (`aws configure`).

```powershell
# Run from backend\restaurant-backend-app\seed:
.\seed-aws-cli.ps1 -Region eu-north-1
```

---

## 🧹 4. Complete Database & User Pool Reset

When you want to reset everything back to a clean slate, use `clean-dynamodb.ps1`.

It completely wipes:
1. **Cognito Users**: Deletes all registered users from `restaurant-backend-user-pool`.
2. **All DynamoDB Tables**:
   - `Reservations`, `GroupReservations`, `GuestSessions`, `Orders`, `Notifications`, `Reports`, `ReportsHistory`
   - `Waiters`, `Feedback`, `Tables`, `Dishes`, `Locations`, `ProfileImages`

```powershell
# Run from backend\restaurant-backend-app\seed:
.\clean-dynamodb.ps1
```

To skip the interactive confirmation prompt:
```powershell
.\clean-dynamodb.ps1 -Force
```

After cleaning, run `.\seed-aws-cli.ps1` to re-populate the database.

---

## 🔍 5. Verification

Verify the seeded records directly via AWS CLI:

```powershell
aws dynamodb scan --table-name Locations --region eu-north-1 --select COUNT
aws dynamodb scan --table-name Dishes --region eu-north-1 --select COUNT
aws dynamodb scan --table-name Tables --region eu-north-1 --select COUNT
aws dynamodb scan --table-name Feedback --region eu-north-1 --select COUNT
aws dynamodb scan --table-name Waiters --region eu-north-1 --select COUNT
```

Expected record counts:
- `Locations`: 3
- `Dishes`: 24
- `Tables`: 18
- `Feedback`: 12
- `Waiters`: 6 (2 per location)
