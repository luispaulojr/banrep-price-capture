# BanRepPriceCapture.Dtf

## Application Identity
BanRepPriceCapture.Dtf captures, processes, persists, and distributes the DTF rate from Banco de la República (Colombia) using SDMX. It exposes HTTP endpoints for daily/weekly series and runs an asynchronous daily capture flow that persists data and sends outbound notifications.

## High-Level Architecture
The solution follows a layered, modular architecture with inward-only dependencies:

- **ServiceLayer**: HTTP API, middleware, health checks, and composition (startup wiring).
- **ApplicationLayer**: workflows/jobs that orchestrate use cases and coordinate domain operations.
- **DomainLayer**: domain models shared across the application.
- **InfrastructureLayer**: external integrations (SDMX, RabbitMQ, PostgreSQL, outbound HTTP, AWS).
- **TestLayer**: automated tests.

**Dependency direction**: ServiceLayer → ApplicationLayer → DomainLayer, and InfrastructureLayer implements cross-cutting integrations consumed by ApplicationLayer. Dependencies only flow inward toward the DomainLayer.

## Execution Flows
### HTTP flow (daily and weekly endpoints)
1. Request arrives at `/dtf-daily` or `/dtf-weekly`.
2. `FlowIdMiddleware` reads `X-Flow-Id` and initializes the FlowId context.
3. `DtfSeriesWorkflow` invokes `DtfDailyJob` or `DtfWeeklyJob`, which pull SDMX data and return the response.
4. Errors are mapped to HTTP responses; unexpected failures trigger a notification.

### RabbitMQ asynchronous flow (daily capture)
1. A message is published to the queue configured in `DtfDailyCapture:QueueName`.
2. `DtfDailyRabbitConsumer` starts a scoped workflow per message and extracts FlowId from `MessageId`.
3. `DtfDailyCaptureWorkflow` pulls SDMX daily data, persists each observation to PostgreSQL, and posts the payload to the outbound HTTP endpoint.
4. On success, the consumer **ACKs** the message. On failure, it emits a critical notification and **NACKs** with requeue.

### FlowId generation and propagation
- **HTTP**: FlowId is parsed from the `X-Flow-Id` header (if present).
- **RabbitMQ**: FlowId is parsed from the message `MessageId` (if present).
- **Propagation**: outgoing HTTP calls attach `X-Flow-Id`; notifications use FlowId as the correlation identifier.

### Retry behavior (high level)
Transient errors are retried for SDMX, outbound HTTP, notification HTTP, database connections, and RabbitMQ connection creation. Retries use bounded attempts with backoff and produce structured warning logs.

## External Integrations
- **SDMX (Banco de la República)** for DTF data.
- **Notification Service** (Microsoft Teams via HTTP).
- **RabbitMQ** for asynchronous daily capture.
- **PostgreSQL** for persistence.
- **AWS** (CloudWatch Logs and Secrets Manager).

## Configuration Overview
The ServiceLayer reads `appsettings.json`, optional environment-specific files, and environment variables. Key settings are grouped under:

- **ExternalServices**: SDMX, Notification, and outbound HTTP settings.
- **Database** / **DatabaseSecrets**: database location, SSL, and secret resolution.
- **RabbitMq**: broker connection and credential environment variables.
- **AWS** / **AWSLogging**: region and log configuration.

Secrets are **never** stored in appsettings files; they are sourced from AWS Secrets Manager or environment variables.

## Observability
- **Logging**: structured JSON logs with consistent fields (level, method, description, message, exception, and FlowId).
- **FlowId correlation**: FlowId is emitted on logs and propagated across HTTP boundaries.
- **Health checks**: `/health` validates SDMX connectivity, RabbitMQ connectivity, and PostgreSQL connectivity.

## Deployment Overview
Kubernetes manifests are provided for **dev**, **uat**, and **prod**. Each manifest defines the deployment, service, environment variables, health probes, and resource limits appropriate for its environment.
