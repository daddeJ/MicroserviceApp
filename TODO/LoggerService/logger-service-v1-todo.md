# 🧾 Logger Service Design Plan

**Version:** 1.0  
**Date:** October 13, 2025  
**Phase:** 2025 Phase 1 Priority  
**Author:** Development Team

---

## 📌 Overview

The **Logger Service** is a centralized logging microservice responsible for collecting, formatting, and storing log activities from other services through **RabbitMQ events**.

It provides consistent, structured logging across all microservices, allowing future integration with monitoring and analytics tools (e.g., ELK, Loki, Prometheus).

---

## 🎯 Goals

- **Standardize logging** across all microservices
- **Consume activity events** from RabbitMQ (Auth, User, etc.)
- **Write structured logs** to text files (daily rotation)
- **Be extensible** to log into databases or monitoring stacks later
- **Provide API endpoints** for health checks and manual log testing

---

## 🧩 Folder Structure

Below is the `LoggerService` structure with new additions marked with 🟥:

```
Development/MicroserviceApp
├── backend
│   ├── src
│   │   ├── LoggerService
│   │   │   ├── appsettings.Development.json
│   │   │   ├── appsettings.json
│   │   │   ├── Consumers
│   │   │   │   ├── AuthActivityConsumer.cs
│   │   │   │   └── UserActivityConsumer.cs
│   │   │   ├── Dockerfile
│   │   │   ├── LoggerService.csproj
│   │   │   ├── LoggerService.http
│   │   │   ├── logs
│   │   │   │   ├── logger20251007.text
│   │   │   │   ├── logger20251008.text
│   │   │   │   ├── logger20251010.text
│   │   │   │   └── logger20251012.text
│   │   │   ├── Program.cs
│   │   │   ├── Properties
│   │   │   │   └── launchSettings.json
│   │   │   ├── Startup
│   │   │   │   └── LoggerStartupHelper.cs
│   │   │   ├── 🟥 Controllers
│   │   │   │   └── 🟥 LoggerController.cs
│   │   │   ├── 🟥 Services
│   │   │   │   ├── 🟥 ILoggerWriter.cs
│   │   │   │   ├── 🟥 FileLoggerWriter.cs
│   │   │   │   ├── 🟥 DatabaseLoggerWriter.cs (optional)
│   │   │   │   └── 🟥 LoggerServiceImp.cs
│   │   │   ├── 🟥 Data
│   │   │   │   ├── 🟥 LogDbContext.cs (optional future)
│   │   │   │   └── 🟥 Entities
│   │   │   │       └── 🟥 LogEntry.cs
│   │   │   ├── 🟥 Mapping
│   │   │   │   └── 🟥 LogProfile.cs
│   │   │   └── 🟥 Helpers
│   │   │       └── 🟥 LogFormatter.cs
│   │   └── Shared
│   │       └── Logging
│   │           └── LogContextHelper.cs
```

---

## ⚙️ Architecture Flow

```text
[AuthService / UserService]
     │
     │ Publishes Event → RabbitMQ
     ▼
 [RabbitMQ Exchange]
     │
     │ Routed to Queues (auth-activity, user-activity)
     ▼
 [LoggerService]
     │
     ├─ Consumers/
     │    ├─ AuthActivityConsumer → receives AuthActivityEvent
     │    └─ UserActivityConsumer → receives UserActivityEvent
     │
     ├─ Services/
     │    ├─ LoggerServiceImp → main coordinator
     │    ├─ FileLoggerWriter → writes to /logs/loggerYYYYMMDD.text
     │    └─ DatabaseLoggerWriter (future)
     │
     ├─ Helpers/
     │    └─ LogFormatter → formats log messages consistently
     │
     └─ logs/
          └─ loggerYYYYMMDD.text (daily rolling log file)
```

---

## 🧠 Components & Responsibilities

| Component | Description | Example Function |
|-----------|-------------|------------------|
| **ILoggerWriter.cs** | Abstraction for all log writers (file, db, etc.) | `Task WriteLogAsync(LogEntry entry);` |
| **FileLoggerWriter.cs** | Writes formatted logs to a daily text file | `logger20251013.text` |
| **DatabaseLoggerWriter.cs** (optional) | Stores structured logs in SQL | Future-proof for analytics |
| **LoggerServiceImp.cs** | Core service coordinating log writing | Uses DI to inject writers |
| **LogEntry.cs** | Log entity structure | `{ Id, Source, Message, Timestamp, Severity }` |
| **LogFormatter.cs** | Formats log text consistently | `"[INFO][AuthService] User logged in: user123"` |
| **AuthActivityConsumer.cs** | RabbitMQ consumer for auth events | Converts event → LogEntry |
| **UserActivityConsumer.cs** | RabbitMQ consumer for user events | Converts event → LogEntry |
| **LoggerController.cs** | Simple API for manual test & health check | `/api/logger/test` |
| **LogProfile.cs** | AutoMapper profile | Maps events → LogEntry DTO |

---

## 🪜 Development Phases

| Phase | Deliverable | Description |
|-------|-------------|-------------|
| **1️⃣ Setup & Foundation** | `ILoggerWriter`, `FileLoggerWriter`, `LogFormatter` | Establish logging abstraction and file output |
| **2️⃣ Core Service Logic** | `LoggerServiceImp` | Handles message formatting and writing via DI |
| **3️⃣ RabbitMQ Consumers** | `AuthActivityConsumer`, `UserActivityConsumer` | Listens to RabbitMQ and logs received events |
| **4️⃣ API Interface** | `LoggerController` | Adds health/test endpoint for debugging |
| **5️⃣ Optional Future** | `DatabaseLoggerWriter`, `LogDbContext`, `LogEntry` | SQL logging and analytics readiness |

---

## 🧩 Integration Points

| Service | Interaction | Event Type |
|---------|-------------|------------|
| **AuthService** | Publishes login/logout events | `AuthActivityEvent`, `AuthTokenGeneratedEvent` |
| **UserService** | Publishes user actions | `UserActivityEvent` |
| **LoggerService** | Consumes events & writes logs | Logs all messages to files |

---

## 🚀 Future Extensions

- Add **structured JSON logs** for ingestion into ElasticSearch / Loki
- Add **gRPC endpoint** for internal log streaming
- Integrate with **Prometheus + Grafana** for metric visualization
- Add **MSSQL persistence** for searchable historical logs

---

## ✅ Deliverable Definition of Done

- ✅ RabbitMQ consumers are listening and writing events
- ✅ Logs are stored in `/logs/loggerYYYYMMDD.text` with structured format
- ✅ LoggerController API is functional (`/api/logger/test`)
- ✅ Configurable log level (Info, Warning, Error) via `appsettings.json`
- ✅ Future extension ready (DI and abstraction implemented cleanly)

---

## 🧱 Example Log Format

```text
[2025-10-13 21:40:12][INFO][AuthService][SessionID:7aa...]
User 'john.doe' successfully logged in.
```

---

## 🗂 Related Shared Components

| Location | File | Purpose |
|----------|------|---------|
| `/Shared/Logging/` | `LogContextHelper.cs` | Provides context tracing between services |
| `/Shared/Events/` | `AuthActivityEvent.cs` | Event payload for authentication logs |
| `/Shared/Events/` | `UserActivityEvent.cs` | Event payload for user activity logs |
| `/Shared/Helpers/` | `RabbitMqConnectionHelper.cs` | Simplifies RabbitMQ setup and consumption |

---

## 📍 Next Steps

### ✅ Proceed with Phase 1 Implementation:

1. `ILoggerWriter.cs` - Interface definition
2. `FileLoggerWriter.cs` - File-based implementation
3. `LogFormatter.cs` - Log formatting utility
4. `LoggerServiceImp.cs` - Core service logic

---

**Document Status:** ✅ Ready for Implementation  
**Last Updated:** October 13, 2025