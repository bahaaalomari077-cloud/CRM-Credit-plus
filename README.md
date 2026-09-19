# Credit Plus CRM

A small sales CRM: an ASP.NET Core 10 API and an Angular 21 single-page app with an Arabic, right-to-left interface.

## Layout

| Path | What it is |
| --- | --- |
| `src/CreditPlus.Crm.Api` | REST API, JSON file storage, OpenAPI document |
| `src/crm-web` | Angular client (dashboard, leads, deals, activities) |

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

Everything lives in one file, `src/CreditPlus.Crm.Api/App_Data/crm.json`, created and seeded on first run and written through a temp file so an interrupted save cannot corrupt it. Delete the file to reset to the seed data. It is git-ignored.

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
