# HELP — BanRepPriceCapture.Dtf Operations Guide

This guide explains how to configure, run, and operate BanRepPriceCapture.Dtf. It is intended for onboarding and production support.

---

## 1. Prerequisites
1. **.NET SDK 10.0** (matches the project target framework).
2. **RabbitMQ** accessible for the daily capture flow.
3. **PostgreSQL** accessible for persistence.
4. **AWS account access** with permissions for Secrets Manager and CloudWatch Logs.
5. **Kubernetes (optional)** for running the provided manifests.

---

## 2. Local Configuration
### 2.1 Required environment variables
The application relies on environment variables for credentials and secrets. At minimum, define the variables referenced in `DatabaseSecrets` and `RabbitMq` settings plus the outbound token environment variable.

**Example list (values are placeholders):**
```bash
# Database secrets (used directly or via Secrets Manager fallback)
export BANREP_DB_HOST=localhost
export BANREP_DB_HOST_RO=localhost
export BANREP_DB_USERNAME=banrep
export BANREP_DB_PASSWORD=changeme

# RabbitMQ credentials
export BANREP_RABBITMQ_USERNAME=guest
export BANREP_RABBITMQ_PASSWORD=guest

# Outbound HTTP token
export BANREP_BEARER_TOKEN=replace_me
```

### 2.2 AWS credentials setup
The AWS SDK uses the default credential chain (environment variables, shared credentials file, or IAM role). Configure whichever method is appropriate for your environment.

### 2.3 Secrets Manager expectations
If `DatabaseSecrets:SecretId` is set, the application expects a JSON secret with:
- `host`
- `host_ro`
- `username`
- `password`

If any value is missing, the application falls back to the environment variables listed above.

---

## 3. AppSettings Structure
The ServiceLayer loads configuration in the following order (later entries override earlier ones):
1. `appsettings.json`
2. `appsettings.global.json` (optional)
3. `appsettings.dev.json` (optional)
4. `appsettings.uat.json` (optional)
5. `appsettings.prod.json` (optional)
6. `appsettings.{Environment}.json` (optional, e.g., `appsettings.Development.json`)
7. Environment variables

### 3.1 ExternalServices structure
`ExternalServices` is the primary grouping for outbound integrations.

**Common fields**
- `BaseUrl`: absolute base URL for the external service.
- `SystemId`: identifier for this system (used in notifications).
- `TimeoutSeconds`: HTTP timeout in seconds.
- `Retry`:
  - `MaxAttempts`: maximum number of attempts.
  - `BackoffSeconds`: delay between attempts.

**Service-specific blocks in appsettings**
- `ExternalServices:Notification`
- `ExternalServices:Sdmx`
- `ExternalServices:DtfDailyOutbound`

> **Important:** Secrets are never stored in appsettings files. Use environment variables or AWS Secrets Manager.

---

## 4. ExternalServices – Notification
### 4.1 Integration behavior
- Uses an HTTP client that posts to a **fixed endpoint**: `/notificar`.
- The base URL comes from `ExternalServices:Notification:BaseUrl`.
- FlowId is sent as `CorrelationId` in the notification payload.

### 4.2 Retry and failure behavior
- Notification HTTP calls use the configured retry policy.
- If the notification fails after retries, the error is logged and propagated.

---

## 5. RabbitMQ Flow
1. A message arrives on the queue defined by `DtfDailyCapture:QueueName`.
2. The consumer creates a **new service scope per message**.
3. FlowId is parsed from the message `MessageId` and stored in the flow context.
4. On success, the message is **ACKed**.
5. On failure, a critical notification is emitted and the message is **NACKed** with requeue enabled.

**Retry considerations**
- Connection creation to RabbitMQ uses retry with backoff.
- Message processing failures lead to requeue (expect repeated attempts until resolved).

---

## 6. Database
### 6.1 PostgreSQL usage
- The application persists daily observations to PostgreSQL in `dtf_daily_prices`.

### 6.2 Dapper usage
- Data access uses **Dapper** for executing SQL and mapping parameters.

### 6.3 TypeHandlers
- `DateOnly` and `TimeOnly` are registered with Dapper type handlers to ensure correct serialization.

### 6.4 Persistence guarantees
- Inserts are **idempotent** for `(flow_id, data_price)` using a conditional insert.

---

## 7. Running the Application
### 7.1 Run locally (HTTP only)
1. Configure environment variables and appsettings.
2. Start the service:
```bash
dotnet run --project BanRepPriceCapture.ServiceLayer
```
3. Access endpoints:
   - `GET /dtf-daily`
   - `GET /dtf-weekly`

### 7.2 Run with RabbitMQ (daily capture)
1. Ensure RabbitMQ is running and the queue exists.
2. Start the service (same command as above).
3. Publish a message to the queue to trigger processing.

### 7.3 Run with mocked external services
- Use local stub services and point `ExternalServices` base URLs to them.
- The Notification service will use a stub implementation when `ExternalServices:Notification:BaseUrl` is not a valid URL, which is useful for local runs without notifications.

---

## 8. Health Checks
### Available health endpoint
- `GET /health`

### What it validates
- **Database** connectivity (simple query).
- **RabbitMQ** connectivity.
- **SDMX** connectivity.

---

## 9. Kubernetes Deployment
### Manifests
- `kubernetes.dev.yaml`
- `kubernetes.uat.yaml`
- `kubernetes.prod.yaml`

### Key considerations
- Each manifest defines deployment, service, environment variables, resource limits, and health probes.
- Secrets (database, RabbitMQ, and bearer token) are injected via environment variables.

---

## 10. Troubleshooting
### Common startup issues
- **Missing database secrets**: verify AWS Secrets Manager access or environment variables.
- **RabbitMQ connectivity**: confirm host/port/credentials and that the queue exists.
- **Notification service failures**: ensure `ExternalServices:Notification:BaseUrl` is reachable and supports `/notificar`.

### Using FlowId to trace problems
- FlowId is logged in every structured log entry.
- For HTTP requests, supply `X-Flow-Id` and search logs by FlowId.
- For RabbitMQ, inspect the message `MessageId` to correlate processing attempts.
