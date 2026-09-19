# Credit Plus CRM

A small sales CRM: an ASP.NET Core 10 API backed by PostgreSQL, and an Angular 21 single-page app that speaks Arabic (RTL) and English (LTR) with light and dark themes.

## Layout

| Path | What it is |
| --- | --- |
| `src/CreditPlus.Crm.Api` | REST API, EF Core + PostgreSQL, OpenAPI document |
| `src/crm-web` | Angular client (dashboard, leads, deals, activities, reports, settings) |

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

Base path `/api/crm`. In development the OpenAPI document is at `/openapi/v1.json`.

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
