# Restaurant System — Backend & Frontend

Monorepo containing the Restaurant booking & ordering system.

## 📂 Project Structure
- `backend/restaurant-backend-app/`: Serverless C# (.NET 10) AWS Lambda microservices deployed via AWS Syndicate.
  - `seed/`: **Database seed data, fixtures, and automated seeder scripts**. See [Seed Guide](backend/restaurant-backend-app/seed/README.md).
- `frontend/restaurant-frontend-app/`: Angular standalone frontend with restaurant location overviews, table booking, and dish menu.
- `infra/`: Terraform infrastructure configuration.
- `howeworks/`: Task specifications and user stories.
