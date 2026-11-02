---
name: syndicate-config-gen
description: >
  Generates, updates, and validates AWS Syndicate IaC configuration files
  (lambda_config.json and deployment_resources.json) for .NET 10 Lambdas in restaurant-backend-app.
---

# AWS Syndicate Configuration Generator Skill (`syndicate-config-gen`)

## Purpose
Ensures accurate, schema-compliant AWS Syndicate configuration files for AWS Lambda functions, DynamoDB tables, SQS queues, and IAM roles without hallucinated fields or broken variable references.

---

## Target Files
- `backend/restaurant-backend-app/dnapp/lambdas/{lambda-name}/lambda_config.json`
- `backend/restaurant-backend-app/dnapp/lambdas/{lambda-name}/deployment_resources.json`
- `backend/restaurant-backend-app/deployment_resources.json` (Root definition)

---

## Standard Specifications for .NET 10 Lambdas

### 1. `lambda_config.json` Template
Place in `backend/restaurant-backend-app/dnapp/lambdas/{lambda-name}/lambda_config.json`:

```json
{
  "version": "1.0",
  "name": "{lambda-name}",
  "func_name": "{ProjectNamespace}::{ProjectNamespace}.Function::FunctionHandler",
  "resource_type": "lambda",
  "iam_role_name": "{lambda-name}-role",
  "runtime": "dotnet10",
  "memory": 256,
  "timeout": 30,
  "lambda_path": "lambdas\\{lambda-name}",
  "dependencies": [],
  "event_sources": [],
  "env_variables": {
    "REGION": "${region}"
  },
  "publish_version": true,
  "alias": "${lambdas_alias_name}",
  "snap_start": "None"
}
```

### 2. SQS / Event-Driven Lambdas
For workers consuming SQS (e.g. `reports-handler` or `report-sender`), configure `event_sources` in `lambda_config.json`:
```json
"event_sources": [
  {
    "resource_type": "sqs_trigger",
    "target_queue": "${target_queue_name}",
    "batch_size": 1
  }
]
```

### 3. Local `deployment_resources.json` Template
Place in `backend/restaurant-backend-app/dnapp/lambdas/{lambda-name}/deployment_resources.json`:

```json
{
  "{lambda-name}-role": {
    "predefined_policies": [],
    "principal_service": "lambda",
    "custom_policies": [
      "{lambda-name}-policy"
    ],
    "resource_type": "iam_role"
  },
  "{lambda-name}-policy": {
    "policy_content": {
      "Statement": [
        {
          "Action": [
            "logs:CreateLogGroup",
            "logs:CreateLogStream",
            "logs:PutLogEvents"
          ],
          "Effect": "Allow",
          "Resource": "*"
        },
        {
          "Action": [
            "dynamodb:GetItem",
            "dynamodb:PutItem",
            "dynamodb:UpdateItem",
            "dynamodb:Query"
          ],
          "Effect": "Allow",
          "Resource": [
            "arn:aws:dynamodb:${region}:${account_id}:table/${target_table_name}",
            "arn:aws:dynamodb:${region}:${account_id}:table/${target_table_name}/index/*"
          ]
        }
      ],
      "Version": "2012-10-17"
    },
    "resource_type": "iam_policy"
  }
}
```

---

## Validation Checklist
- [ ] Runtime is explicitly `"dotnet10"`.
- [ ] IAM policy adheres to principle of least privilege (avoid wildcard `*` on data tables).
- [ ] Environment variable aliases use `${alias_name}` syntax matching `syndicate_aliases.yml`.
- [ ] Path in `lambda_path` matches the folder structure inside `backend/restaurant-backend-app/dnapp/`.

