# Notification Application

A .NET 8 Web API that receives notifications via HTTP and forwards qualifying ones to a Discord channel, with a built-in sliding-window rate limiter.

---

## Table of Contents

- [Overview](#overview)
- [Architecture](#architecture)
- [Business Rules](#business-rules)
- [Project Structure](#project-structure)
- [Getting Started](#getting-started)
- [Configuration](#configuration)
- [API Reference](#api-reference)
- [Running Tests](#running-tests)
- [Design Decisions](#design-decisions)

---

## Overview

| Capability | Detail |
|---|---|
| Framework | .NET 8 / ASP.NET Core |
| API documentation | Swagger UI (opens automatically at `/`) |
| External channel | Discord incoming webhook |
| Rate limit | 10 dispatched messages per sliding 60-second window |
| Architecture | Clean Architecture (Domain → Application → Infrastructure → API) |

---

## Architecture

```
┌─────────────────────────────────────────────────────────────┐
│  NotificationApp  (API)                                      │
│   • NotificationsController  POST /api/notifications         │
│   • Swagger / OpenAPI                                        │
└────────────────────────┬────────────────────────────────────┘
                         │ references
        ┌────────────────┴────────────────┐
        │                                 │
┌───────▼──────────────┐   ┌─────────────▼───────────────────┐
│  NotificationApp.    │   │  NotificationApp.               │
│  Application         │   │  Infrastructure                 │
│   • NotificationSvc  │   │   • DiscordWebhookService       │
│   • DTOs / Interfaces│   │   • SlidingWindowRateLimiter    │
└───────┬──────────────┘   └─────────────┬───────────────────┘
        │ references                      │ references
        └──────────┬──────────────────────┘
                   │
        ┌──────────▼──────────────┐
        │  NotificationApp.Domain │
        │   • NotificationLevel   │
        │   • Notification entity │
        │   • IDiscordService     │
        │   • IRateLimiter        │
        └─────────────────────────┘
```

The **Domain** layer has zero external dependencies. All other layers point inward — nothing in Domain knows about Infrastructure or API.

---

## Business Rules

1. **Level threshold** — a notification is dispatched to Discord only if its `level` is `Warning`, `Error`, or `Critical`. `Debug` and `Info` are silently acknowledged (HTTP 200, `dispatched: false`).
2. **Rate limiting** — a maximum of **10 messages per minute** are forwarded. Any excess request returns HTTP **429 Too Many Requests** with the response body indicating `RateLimitExceeded`.
3. **Discord not configured** — if `Discord:WebhookUrl` is empty the service no-ops gracefully (useful for local development without a real webhook).

### Notification levels (ordered)

| Level | Value | Dispatched? |
|-------|-------|-------------|
| Debug | 0 | No |
| Info | 1 | No |
| **Warning** | **2** | **Yes** |
| Error | 3 | Yes |
| Critical | 4 | Yes |

---

## Project Structure

```
NoificationApplication/
├── src/
│   ├── NotificationApp.Domain/          # Entities, enums, domain interfaces
│   ├── NotificationApp.Application/     # Business logic, DTOs, application interfaces
│   └── NotificationApp.Infrastructure/  # Discord HTTP client, rate limiter, DI wiring
├── NotificationApp/                     # ASP.NET Core Web API (controllers, Swagger)
├── tests/
│   ├── NotificationApp.UnitTests/       # xUnit unit tests for service + rate limiter
│   └── NotificationApp.IntegrationTests/# xUnit integration tests via WebApplicationFactory
└── README.md
```

---

## Getting Started

### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

### Run the API

```bash
cd NotificationApp
dotnet run
```

The Swagger UI opens automatically at **http://localhost:5137** (or the HTTPS URL printed in the terminal).

### (Optional) Configure Discord

1. Create an [Incoming Webhook](https://support.discord.com/hc/en-us/articles/228383668) in your Discord server.
2. Copy the webhook URL into `appsettings.json` (or use an environment variable / user secrets):

```json
{
  "Discord": {
    "WebhookUrl": "https://discord.com/api/webhooks/<id>/<token>"
  }
}
```

---

## Configuration

| Key | Default | Description |
|-----|---------|-------------|
| `Discord:WebhookUrl` | `""` | Discord incoming webhook URL. Empty = disable forwarding. |

---

## API Reference

### `POST /api/notifications`

Submit a notification for processing.

#### Request body

```json
{
  "level": "Warning",
  "message": "Disk usage above 90% on prod-db-01",
  "source": "MonitoringAgent"
}
```

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `level` | `string` (enum) | Yes | One of: `Debug`, `Info`, `Warning`, `Error`, `Critical` |
| `message` | `string` | Yes | 1–2 000 characters |
| `source` | `string` | No | Up to 200 characters |

#### Responses

| Status | When | Body |
|--------|------|------|
| `200 OK` | Notification received and processed | `SendNotificationResponse` |
| `400 Bad Request` | Validation failure | Validation problem details |
| `429 Too Many Requests` | Rate limit exceeded (>10/min) | `SendNotificationResponse` |

#### Response body (`SendNotificationResponse`)

```json
{
  "notificationId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "status": "Accepted",
  "statusMessage": "Notification dispatched to Discord.",
  "dispatched": true
}
```

| Field | Type | Values |
|-------|------|--------|
| `notificationId` | `guid` | Unique ID assigned to this notification |
| `status` | `string` | `Accepted` \| `BelowThreshold` \| `RateLimitExceeded` |
| `statusMessage` | `string` | Human-readable explanation |
| `dispatched` | `bool` | `true` only when `status == "Accepted"` |

---

## Running Tests

```bash
# All tests
dotnet test

# Unit tests only
dotnet test tests/NotificationApp.UnitTests

# Integration tests only
dotnet test tests/NotificationApp.IntegrationTests
```

### Test coverage

| Suite | Tests | What is covered |
|-------|-------|-----------------|
| Unit | 15 | `NotificationService` routing logic (5 level variants × success/failure), `SlidingWindowRateLimiter` boundary conditions, concurrency safety |
| Integration | 11 | Full HTTP pipeline: happy path (below/above threshold), Discord payload content, validation (400), rate-limit enforcement (429) |

Integration tests replace `IDiscordService` and `IRateLimiter` with in-process test doubles — no real network calls are made.

---

## Design Decisions

### Clean Architecture
Each layer depends only on the layer beneath it. The Domain has no framework references, making business rules easy to test and port.

### Sliding-Window Rate Limiter
Implemented as an in-process singleton (`SlidingWindowRateLimiter`). A `ConcurrentQueue<DateTime>` records the timestamp of each dispatched message; a lock evicts stale entries on every `TryAcquire()` call. This is simple, correct, and sufficient for a single-instance deployment. For a multi-replica deployment, this should be replaced with a distributed cache (e.g. Redis + Lua script).

### 429 vs 200 for Rate Limit
Returning **HTTP 429** (instead of 200 with a flag) follows REST conventions and lets clients implement backoff purely on status code without parsing the body.

### Discord Webhook Graceful No-Op
When `WebhookUrl` is empty, `DiscordWebhookService.SendAsync` returns immediately without throwing. This means the app runs fully in development without needing a real Discord server.
