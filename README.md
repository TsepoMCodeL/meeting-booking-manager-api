# MeetingManagerAPI

## Run with Docker Compose

Set `FRONTEND_ORIGIN` to the front-end's exact origin, and set the JWT
authority and audience values from your identity provider, then start the API:

```powershell
$env:FRONTEND_ORIGIN = "https://meetings.example.com"
$env:JWT_AUTHORITY = "https://login.microsoftonline.com/<tenant-id>/v2.0"
$env:JWT_AUDIENCE = "<the API audience configured with your identity provider>"
docker compose up --build -d
```

The API uses the authority's OpenID Connect discovery document and JWKS to
validate bearer tokens. It requires valid signatures, issuer, audience, and
expiration. All `/api` endpoints require a token sent in the
`Authorization: Bearer <token>` header. The authority must use HTTPS.

Booking start and end values must be ISO 8601 date-times with an explicit
timezone, for example `2026-11-11T09:00:00+02:00` or
`2026-11-11T07:00:00Z`. The API stores these instants in UTC.

The API applies EF Core migrations at startup to create and update its schema.

Room and booking persistence is accessed through repository interfaces, with
transactions handled through a unit-of-work interface. A future database
provider can replace these EF Core implementations without changing booking
logic; provider configuration and migrations still need to be updated for that
database.

The application service returns application outcomes; the handlers in
`Endpoints/MeetingRoomEndpoints.cs` map those outcomes to HTTP status codes and
response bodies.

The API listens on port `8080` by default. Set `API_PORT` to change the host
port. SQLite data is stored in the `meeting-room-data` named volume and
survives container recreation.

For local development, Vue's `http://localhost:5173` is the default origin.
Angular users can set `FRONTEND_ORIGIN` to `http://localhost:4200`.

Put the container behind an HTTPS reverse proxy for deployment. CORS only
controls browser origins; it does not authenticate API clients.

When running outside Docker, configure `Authentication:Authority` and
`Authentication:Audience` using application settings or the corresponding
environment variables `Authentication__Authority` and
`Authentication__Audience`. The API refuses to start if either value is
missing or the authority is not HTTPS.
