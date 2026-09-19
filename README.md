# Credit Plus CRM

A small sales CRM: an ASP.NET Core 10 API backed by PostgreSQL, and an Angular 21 single-page app that speaks Arabic (RTL) and English (LTR) with light and dark themes. Access is controlled by signed-in accounts with roles, and every change is written to an audit trail.

## Layout

| Path | What it is |
| --- | --- |
| `src/CreditPlus.Crm.Api` | REST API, EF Core + PostgreSQL, OpenAPI document |
| `src/crm-web` | Angular client (dashboard, leads, deals, activities, reports, users, audit, settings) |

## Prerequisites

- .NET 10 SDK
- Node.js 20+
- PostgreSQL 17 with a database named `crm-db`

Create the database once:

```bash
psql -U postgres -h localhost -p 5433 -c 'CREATE DATABASE "crm-db";'
```

## Running it

Both parts run side by side. Start the API first.

```bash
# Terminal 1 — API on http://localhost:5233
cd src/CreditPlus.Crm.Api
dotnet run

# Terminal 2 — web app on http://localhost:4200
cd src/crm-web
npm install
npm start
```

Open <http://localhost:4200>. If the API is not running, the app shows a banner and empty states rather than failing.

## Signing in

The first run seeds two accounts:

| Email | Password | Role |
| --- | --- | --- |
| `admin@creditplus.local` | `Admin123!` | Admin |
| `agent@creditplus.local` | `Agent123!` | Agent |

**Change these before putting the app anywhere other than your own machine.** They exist
so a fresh database can be signed into at all.

### Roles

| Role | May do |
| --- | --- |
| Viewer | Read every record; no changes |
| Agent | Everything a Viewer can, plus create, edit, delete, and send email |
| Manager | Everything an Agent can, plus read the audit trail |
| Admin | Everything, plus manage user accounts |

The API enforces these with authorization policies; the client hides controls it knows
would be refused, so the UI and the API agree. The last active administrator cannot be
deleted, demoted, or disabled.

Sessions use a JWT held in `localStorage`. `Jwt:SigningKey` in `appsettings.json` must be
at least 32 characters — the API refuses to start otherwise. Set a real secret through
`Jwt__SigningKey` in any environment that matters.

## Audit trail

Every create, update, delete, stage move, sign-in, failed sign-in, and sent email is
recorded with the acting user, their IP address, and a JSON diff of what changed. Entries
are written through their own database context, so they survive a rollback in the request
that produced them, and a failure to audit is logged rather than failing the operation.

Managers and admins read the trail at `/audit`, filtered by record type or action; each
row expands to show the before/after values.

## Email

`POST /api/emails` sends a message and files it against a lead or deal, so the record's
history shows what was sent, by whom, and whether it was delivered. Failures are stored
on the message rather than discarded.

With `Email:Enabled` set to `false` (the default) messages are written as `.eml` files to
`App_Data/outbox` instead of being delivered, so the feature works without credentials.
Set the SMTP host, port, and credentials in `appsettings.json` and flip `Email:Enabled`
to `true` to send for real.

## Data

Data lives in PostgreSQL. On startup the API applies any pending EF Core migrations and seeds demo rows into empty tables, so a fresh `crm-db` is usable immediately.

The connection string is `ConnectionStrings:CrmDatabase` in `appsettings.json`; it points at `localhost:5433` because that is the port this PostgreSQL install listens on (check `postgresql.conf` if yours differs — the default is 5432).

Schema changes go through EF Core migrations:

```bash
cd src/CreditPlus.Crm.Api
dotnet ef migrations add <Name>
```

To start over, drop and recreate `crm-db`; the next run re-creates and re-seeds it.

## Languages and themes

The interface ships in Arabic and English. Every string lives in `src/crm-web/src/app/core/i18n/ar.ts` and `en.ts` — never inline in a template — and `en.ts` is typed against `ar.ts`, so a missing or stray key fails the build. Templates read strings through the `t` pipe.

Switching language updates `<html lang>` and `<html dir>`, flips the layout between RTL and LTR, and reformats dates, numbers, and currency. Because Angular resolves `LOCALE_ID` once per injector, dates and money go through the `ldate` and `lmoney` pipes, which format via `Intl` against the current language signal and therefore react to a switch without a reload.

The light and dark themes are CSS custom properties defined on `:root` and `[data-theme="dark"]` in `src/crm-web/src/styles.scss`. Both choices persist in `localStorage`; the theme falls back to the system preference on first visit.

## API

Base path `/api/crm`. Every endpoint except `/api/auth/login` requires a bearer token (`Authorization: Bearer <token>`). In development the OpenAPI document is at `/openapi/v1.json`.

| Method | Route | Purpose |
| --- | --- | --- |
| GET | `/dashboard` | Headline metrics, pipeline by stage, open deals, open activities |
| GET | `/leads?search=&status=` | List leads, optionally filtered |
| POST · PUT · DELETE | `/leads`, `/leads/{id}` | Create, update, remove a lead |
| GET | `/deals?search=&stage=` | List deals, optionally filtered |
| GET | `/deals/stages` | The valid stage names |
| POST · PUT · DELETE | `/deals`, `/deals/{id}` | Create, update, remove a deal |
| PATCH | `/deals/{id}/stage` | Move a deal to another stage |
| GET | `/activities?completed=` | List activities |
| POST · PUT · DELETE | `/activities`, `/activities/{id}` | Create, update, remove an activity |
| PATCH | `/activities/{id}/complete`, `/activities/{id}/reopen` | Toggle completion |

Authentication and administration sit outside `/api/crm`:

| Method | Route | Purpose |
| --- | --- | --- |
| POST | `/api/auth/login` | Exchange credentials for a token |
| GET | `/api/auth/me` | The signed-in user, for restoring a session |
| POST | `/api/auth/change-password` | Change your own password |
| GET · POST · PUT · DELETE | `/api/users` | Manage accounts (Admin) |
| GET | `/api/audit?entityType=&entityId=&action=&limit=` | Read the audit trail (Manager, Admin) |
| GET · POST | `/api/emails` | List and send messages |

Deal stages are `Qualified`, `Proposal`, `Negotiation`, `Won`, `Lost`; lead statuses are `New`, `Contacted`, `Qualified`, `Converted`, `Lost`. The API stores these English values and the UI displays Arabic labels for them, mapped in `src/crm-web/src/app/core/crm.models.ts`.

Moving a deal to `Won` sets its probability to 100, and to `Lost` sets it to 0, so the dashboard's pipeline totals and conversion rate stay consistent.

Validation failures return 400 with a ProblemDetails body whose `detail` the UI shows inside the open dialog; unknown ids return 404.

## Configuration

The API reads allowed CORS origins from `Cors:AllowedOrigins` in `appsettings.json` (default `http://localhost:4200`). The client reads its API base URL from `src/crm-web/src/environments/environment.ts`; change both together if you move a port.

## Building

```bash
dotnet build CreditPlus.Crm.slnx
cd src/crm-web && npm run build
```
