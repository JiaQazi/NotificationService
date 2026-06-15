# NotificationService

NotificationService is a .NET 8 microservice for reliably delivering notifications (Email, SMS, Push) to recipients. It uses the Transactional Outbox pattern to guarantee at-least-once message delivery via RabbitMQ, even in the face of transient failures. The service exposes a REST API to create and query notifications, processes the outbox on a background timer, and consumes delivered messages from RabbitMQ to update notification status — all built on a Clean Architecture foundation with no cross-layer leakage.


## Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                        API Layer                            │
│          NotificationService.API (ASP.NET Core)             │
│   Controllers · Program.cs · appsettings.json               │
└───────────────────────┬─────────────────────────────────────┘
                        │ depends on
┌───────────────────────▼─────────────────────────────────────┐
│                   Application Layer                         │
│              NotificationService.Application                │
│   Services · Interfaces (ports) · DTOs                      │
└──────────┬────────────────────────────────┬─────────────────┘
           │ depends on                     │ depends on
┌──────────▼──────────┐        ┌────────────▼────────────────┐
│    Domain Layer     │        │    Infrastructure Layer      │
│  NotificationService│        │  NotificationService         │
│      .Domain        │        │      .Infrastructure         │
│                     │        │                              │
│  Entities           │        │  AppDbContext (EF Core)      │
│  · Notification     │        │  NotificationRepository      │
│  · OutboxMessage    │        │    (EF writes / Dapper reads)│
│                     │        │  OutboxRepository (Dapper)   │
│  Enums              │        │  RabbitMqPublisher           │
│  · NotificationType │        │  OutboxProcessorService      │
│  · NotificationStatus        │  NotificationConsumerService │
└─────────────────────┘        └──────────────────────────────┘

Flow:
  POST /api/notifications
        │
        ▼
  NotificationService.CreateAsync
        │
        ├──► Save Notification (Pending)  ──► SQL Server (EF Core)
        └──► Save OutboxMessage           ──► SQL Server (Dapper)
                                                    │
                              OutboxProcessorService │ (every 10s)
                                                    ▼
                                             RabbitMQ queue
                                                    │
                              NotificationConsumerService
                                                    ▼
                                  Update Notification → Delivered
```


## Key Patterns

### Transactional Outbox
Writing a notification and its outbox message in the same database transaction eliminates the dual-write problem. If the RabbitMQ publish fails, the outbox row remains and will be retried. `OutboxProcessorService` polls every 10 seconds, applies exponential backoff (`2^RetryCount` seconds), and stops retrying after 3 attempts.

### Clean Architecture
Dependencies point inward: Infrastructure and API depend on Application; Application depends on Domain; Domain depends on nothing. All cross-boundary communication goes through interfaces(`INotificationRepository`, `IOutboxRepository`, `IMessagePublisher`) defined in the Application layer and implemented in Infrastructure. This makes the core logic testable without a database or message broker.

### CQRS-lite (Read/Write Separation)
Writes use EF Core (`NotificationRepository.AddAsync`, `UpdateAsync`) for change tracking and model validation. Reads use Dapper (`GetAllAsync`) for raw SQL performance without the ORM overhead. The same SQL Server database backs both paths with no separate read store, hence "lite".

## Running Locally

### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8)
- SQL Server (local instance or LocalDB)
- Docker (for RabbitMQ)
- RabbitMQ running on `localhost:5672`

### 1. Start RabbitMQ

```bash
docker run -d --hostname rabbit --name rabbit -p 5672:5672 -p 15672:15672 rabbitmq:3-management
```

Management UI available at `http://localhost:15672` (guest / guest).

> **Note:** The API will start without RabbitMQ running. Publishing will be skipped with a warning logged and retried on the next outbox cycle. Check `GET /health` to see RabbitMQ connection status.

### 2. Create the Database

```bash
cd src/NotificationService.API
dotnet ef database update
```

> If EF tools are not installed: `dotnet tool install --global dotnet-ef`

### 3. Configure appsettings

Edit `src/NotificationService.API/appsettings.json` (or use `appsettings.Development.json`) — see [Environment Variables](#environment-variables--appsettings) below.

### 4. Run the API

```bash
dotnet run --project src/NotificationService.API
```

Swagger UI: `https://localhost:{port}/swagger`

### 5. Run the Tests

```bash
dotnet test
```

---

## Example API Request

### Create a Notification

```http
POST /api/notifications
Content-Type: application/json

{
  "recipientId": "user-123",
  "type": 0,
  "subject": "Welcome aboard!",
  "body": "Thanks for signing up. Your account is ready."
}
```

`type` values: `0` = Email, `1` = SMS, `2` = Push

**Response — 201 Created**

```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "recipientId": "user-123",
  "type": 0,
  "subject": "Welcome aboard!",
  "body": "Thanks for signing up. Your account is ready.",
  "status": 0,
  "createdAt": "2024-11-01T10:00:00Z",
  "retryCount": 0
}
```

### Get a Notification by ID

```http
GET /api/notifications/3fa85f64-5717-4562-b3fc-2c963f66afa6
```

### Get All Notifications

```http
GET /api/notifications
```

---

## Environment Variables / appsettings

All configuration lives in `appsettings.json`. Override per-environment using `appsettings.Development.json` or environment variables (ASP.NET Core binding convention: `Section__Key`).

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=NotificationServiceDb;Trusted_Connection=True;TrustServerCertificate=True;"
  },
  "RabbitMQ": {
    "Host": "localhost",
    "Port": 5672,
    "Username": "guest",
    "Password": "guest",
    "QueueName": "notifications"
  }
}
```

| Key | Description | Default |
|---|---|---|
| `ConnectionStrings:DefaultConnection` | SQL Server connection string used by both EF Core and Dapper | LocalDB / localhost |
| `RabbitMQ:Host` | RabbitMQ broker hostname | `localhost` |
| `RabbitMQ:Port` | AMQP port | `5672` |
| `RabbitMQ:Username` | Broker username | `guest` |
| `RabbitMQ:Password` | Broker password | `guest` |
| `RabbitMQ:QueueName` | Queue name for outbox publishing and consumer listening | `notifications` |

**Environment variable equivalents** (useful in Docker / CI):

```bash
ConnectionStrings__DefaultConnection="Server=db;Database=NotificationServiceDb;..."
RabbitMQ__Host=rabbitmq
RabbitMQ__Username=admin
RabbitMQ__Password=secret
```
