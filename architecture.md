# Architecture

## Overview

MeetingManagerAPI is a standalone ASP.NET Core minimal API for listing meeting
rooms and creating, updating, listing, and cancelling room bookings. The
solution separates HTTP handling, booking use cases, persistence contracts,
and their EF Core/SQLite implementations.

## Layers

```text
HTTP client
    |
    v
Endpoints/MeetingRoomEndpoints.cs
    |  HTTP routes, status codes, response bodies
    v
Domain/MeetingRoomService.cs
    |  booking validation and use-case rules
    +------------------------+
    |                        |
    v                        v
Interfaces/*Repository    Interfaces/IUnitOfWork
    |                        |
    v                        v
Data/Repositories/*       EF transaction adapter
    \                        /
     v                      v
          MeetingRoomDbContext
                   |
                   v
             SQLite database
```

### Endpoints

`MeetingRoomEndpoints` maps authenticated `/api/rooms` and `/api/bookings`
routes. It translates application results into HTTP responses, including
validation problems, not-found responses, conflicts, and successful create,
update, and cancel responses.

### Application/domain

`MeetingRoomService` implements room and booking use cases. It validates booking
inputs, normalizes times to UTC, checks room existence and booking conflicts,
and commits changes through the unit-of-work abstraction. It returns
application results and does not depend on ASP.NET response types.

### Persistence contracts and adapters

`IMeetingRoomRepository`, `IMeetingBookingRepository`, `IUnitOfWork`, and
`IUnitOfWorkTransaction` define the persistence boundary. Their current
implementations in `Data/Repositories` use `MeetingRoomDbContext` and EF Core.
The repositories share the scoped DbContext instance with the unit of work,
so reads, tracked entities, saves, and transactions participate in the same
operation.

`MeetingRoomDbContext` configures UTC date-time storage. EF Core migrations
create and update the database schema.

## Request flow

1. A client sends an HTTP request with a bearer access token.
2. ASP.NET Core validates the token before an authorized endpoint runs.
3. The endpoint calls `MeetingRoomService` and maps its result to HTTP.
4. The application service uses repository interfaces to read or change data.
5. For booking writes, the service starts a transaction, checks for conflicts,
   saves changes, and commits.
6. Unexpected exceptions pass through the centralized exception handler.

## Authentication and browser access

JWT bearer authentication uses the configured HTTPS OpenID Connect authority.
The authority's discovery document and JWKS provide signing keys. Tokens are
validated for signature, issuer, audience, and expiration. All `/api` routes
require authorization.

CORS allows only configured origins. Development settings allow the default
Vue and Angular development origins; deployment configuration should specify
the exact front-end origin. CORS is not authentication.

## Database and startup

SQLite is the current database provider. `MeetingRoomDbContextFactory` supplies
the EF design-time context for migration tools. At startup, the API applies
migrations and seeds the three sample rooms when the room table is empty.

The initial migration creates the database schema, which is applied at startup.

Changing the database provider requires replacing the EF provider
configuration and creating provider-appropriate migrations. Repository
contracts keep provider-specific query code out of the application service,
but do not make SQL behavior, migrations, or provider setup interchangeable.

## Errors and validation

The application returns validation and business-rule errors as typed
application outcomes. Endpoint handlers produce their HTTP representation.
`ApiExceptionHandler` turns unreadable requests into safe client errors and
SQLite busy/locked errors, including errors wrapped by EF Core, into
retryable service-unavailable responses. Other unexpected exceptions are
handled by ASP.NET Core Problem Details middleware.

Booking request dates must be ISO 8601 values with an explicit UTC marker or
offset, such as `2026-11-11T09:00:00+02:00`. They are converted to UTC before
validation and storage.

## Deployment and configuration

Docker Compose builds the API image and mounts a named volume at `/data` for
the SQLite database. The container listens on port `8080`; deployment should
place it behind an HTTPS reverse proxy.

Configure these values for deployment:

- `Authentication__Authority`: HTTPS OIDC authority.
- `Authentication__Audience`: expected API token audience.
- `Cors__AllowedOrigins__0`: exact browser front-end origin.
- `ConnectionStrings__MeetingRooms`: SQLite connection string.
- `API_PORT`: optional host port for Compose.

## Tests

`tests/MeetingManagerAPI.Tests` covers booking validation and conflicts,
concurrent overlapping booking attempts, UTC conversion, fresh SQLite schema
creation, wrapped SQLite busy errors, bearer-token acceptance and rejection,
and endpoint response mapping.
