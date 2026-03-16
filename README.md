# Cog Crankers Monorepo

A full-stack cog-themed economy platform:

- UI: Angular (`apps/ui`)
- API: ASP.NET Core MVC controllers + EF Core SQL Server (`apps/api/CogCrankers.Api`)
- Database-first SQL schema scripts (`database/sql`)

Users authenticate with Google, receive and spend cogs, and buy gear. Admins manage the cog economy with grants and gear catalog control.

## Repo Layout

- `apps/api/CogCrankers.Api`: API + auth + economy logic
- `apps/ui`: Angular front-end
- `database/sql`: DB create scripts for database-first workflow
- `database/README.md`: EF scaffolding instructions

## 1) Database Setup

1. Run `database/sql/001_create_cogcrankers.sql` in SQL Server.
2. Confirm the API connection string in `apps/api/CogCrankers.Api/appsettings*.json`.

## 2) Google Auth Setup

Set these values in `apps/api/CogCrankers.Api/appsettings.Development.json`:

- `Authentication:Google:ClientId`
- `Authentication:Google:ClientSecret`

Google OAuth callback URL should include:

- `https://localhost:7298/signin-google`

## 3) Run API

```powershell
dotnet run --project apps/api/CogCrankers.Api
```

Default dev URLs from launch settings:

- `https://localhost:7298`
- `http://localhost:5212`

## 4) Run UI

```powershell
cd apps/ui
cmd /c npm install
cmd /c npm start
```

The UI runs on `http://localhost:4200` and calls the API at `https://localhost:7298` (configured in `apps/ui/src/app/app.settings.ts`).

## 5) First Admin User

After first Google login creates a user row, run the SQL snippet at the bottom of `database/sql/001_create_cogcrankers.sql` to assign `CogAdmin` to your email.

## Build Checks

- API: `dotnet build CogCrankers.sln`
- UI: `cd apps/ui && cmd /c npm run build`
