# Restaurant System — Database Seeding Guide

This directory contains seed data and automated tools to populate the DynamoDB database and REST API for the Restaurant management application.

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

| Field | C# Type | DynamoDB Type | REST API (`POST /dishes`) | Description |
|---|---|---|---|---|
| `id` | `string` | `S` | (auto-generated) | Unique dish GUID |
| `name` | `string` | `S` | `string` | Dish title |
| `description` | `string` | `S` | `string` | Ingredients and culinary description |
| `dishType` | `DishType` (Enum) | **`N` (Integer)** | `string` | `0` = `"Appetizer"`, `1` = `"MainCourse"`, `2` = `"Dessert"` |
| `price` | `decimal` | `N` | `number` | Price in USD (e.g., `16.50`) |
| `weight` | `string` | `S` | `string` | Serving weight (e.g., `"320g"`) |
| `calories` | `string` | `S` | `string` | Energy value (e.g., `"410"`) |
| `proteins` | `string` | `S` | `string` | Protein content (e.g., `"9g"`) |
| `fats` | `string` | `S` | `string` | Fat content (e.g., `"28g"`) |
| `carbohydrates` | `string` | `S` | `string` | Carbohydrate content (e.g., `"32g"`) |
| `vitamins` | `string` | `S` | `string` | Vitamins & minerals (e.g., `"A, B6, C, E, K"`) |
| `imageUrl` | `string?` | `S` | `imageBase64` / path | Relative asset path or public S3 URL |
| `state` | `DishState` (Enum) | **`N` (Integer)** | `string` | `0` = `"OnStop"`, `1` = `"Available"`, `2` = `"OutOfStock"` |
| `locationId` | `string` | `S` | `locationIds: [...]` | Primary restaurant location ID |

> ⚠️ **DynamoDB Enum Storage**:
> In `Dish.cs`, `DishType` and `State` use `EnumAsIntConverter`. When saving directly into DynamoDB, they **must be numbers** (`{"N": "0"}`), whereas the REST API (`POST /dishes`) accepts string names (`"Appetizer"`, `"Available"`). The provided batch JSON files and scripts handle this conversion automatically.

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

### Method 1: Direct DynamoDB Batch Seeding (PowerShell / AWS CLI)
Writes Locations, Dishes, Tables, and Feedback directly to DynamoDB in batches. It takes under 3 seconds and does not require an active API Gateway.

Ensure AWS CLI is configured (`aws configure`).

```powershell
# Run from backend\restaurant-backend-app\seed:
.\seed-aws-cli.ps1 -Region eu-north-1
```

---

### Method 2: Seeding via REST API Gateway (Node.js)
Sends HTTP `POST` requests through the deployed AWS API Gateway (`/locations`, `/dishes`, `/tables`), invoking the Lambda functions and verifying backend validation rules.

#### Where does the API URL come from?
The API Gateway base URL is created during backend deployment. You can find it:
1. In the console output immediately after running `syndicate deploy`.
2. In `frontend/restaurant-frontend-app/src/environments/environment.development.ts` under `backendUrl`.
3. In AWS Console: **API Gateway** -> select `restaurant-backend-api` -> **Stages** -> `develop` (field **Invoke URL**).

#### Running the Script:
The script `seed-api.mjs` runs on Node.js (v18+ or v24+) with zero external dependencies (uses native `fetch`):

```powershell
# Run from backend\restaurant-backend-app\seed:
node seed-api.mjs <YOUR_API_BASE_URL>
```

Example:
```powershell
node seed-api.mjs https://YOUR_API_ID.execute-api.eu-north-1.amazonaws.com/develop
```

You can also pass it via environment variable:
```powershell
$env:API_BASE_URL = "https://YOUR_API_ID.execute-api.eu-north-1.amazonaws.com/develop"
node seed-api.mjs
```

---

## 🔍 4. Verification

### 1. Verify via AWS CLI
```powershell
aws dynamodb scan --table-name Locations --region eu-north-1 --select COUNT
aws dynamodb scan --table-name Dishes --region eu-north-1 --select COUNT
aws dynamodb scan --table-name Tables --region eu-north-1 --select COUNT
aws dynamodb scan --table-name Feedback --region eu-north-1 --select COUNT
```

### 2. Verify via Frontend
Start the Angular application:
```powershell
cd ..\..\..\frontend\restaurant-frontend-app
npm start
```
Navigate to `http://localhost:4200` to inspect:
* Restaurant cards on the landing page with addresses, ratings, and table counts.
* The location detail page with customer reviews.
* The menu page with categorized appetizers, main courses, and desserts.
