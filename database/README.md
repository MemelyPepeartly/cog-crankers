# Database-first setup

1. Run `database/sql/001_create_cogcrankers.sql` in SQL Server Management Studio or Azure Data Studio.
2. Update the API connection string (`apps/api/CogCrankers.Api/appsettings*.json`).
3. Scaffold EF models/context from this schema when needed:

```powershell
dotnet ef dbcontext scaffold "Server=(localdb)\\MSSQLLocalDB;Database=CogCrankers;Trusted_Connection=True;TrustServerCertificate=True" Microsoft.EntityFrameworkCore.SqlServer --project apps/api/CogCrankers.Api --context CogCrankersDbContext --context-dir Data --output-dir Models/Entities --use-database-names --force
```

4. Keep the SQL script as the source of truth for database-first changes.
