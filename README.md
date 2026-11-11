# ICS2026 – Backend API (.NET 8)

API REST de comercio electrónico construida con ASP.NET Core 8, Entity Framework Core, SQL Server, Identity y JWT.  
Containerizada con Docker multi-stage y desplegada en Azure App Service con pipeline CI/CD automatizado.

##  Estructura del Proyecto

```
Dsw2025Tpi.sln
├── Dsw2025Tpi.Api          → Controllers, Program.cs, Middleware
├── Dsw2025Tpi.Application  → Servicios, DTOs, Mapeos
├── Dsw2025Tpi.Domain       → Entidades y reglas de negocio
├── Dsw2025Tpi.Data         → DbContext, Migrations, Repositorios
├── Dockerfile              → Build multi-stage (sdk:8.0 → aspnet:8.0)
├── .env.example             → Variables de entorno documentadas
└── .github/workflows/       → Pipeline CI/CD (GitHub Actions)
```

**Stack:** C# 12 · .NET 8 · EF Core 8 · Identity + JWT · SQL Server · Docker · GitHub Actions · Azure

## ☁️ Producción — Azure

### Pipeline CI/CD

Cada push a `development` dispara el pipeline de GitHub Actions:

```
Push a development
    │
    ├─ 1. Checkout
    ├─ 2. Build & Restore (.NET 8)
    ├─ 3. Docker Build (multi-stage)
    ├─ 4. Push → Docker Hub
    └─ 5. Deploy → Azure App Service
                │
                └─→ Azure SQL Database
```

### Endpoints en vivo

| Recurso | URL |
|---|---|
| Swagger UI | https://ics-ecommerce-api-hcdef4c3cadkhza7.centralus-01.azurewebsites.net/swagger |
| API Base | https://ics-ecommerce-api-hcdef4c3cadkhza7.centralus-01.azurewebsites.net/api |
| Docker Hub | https://hub.docker.com/r/bernabefigueroa/ics-backend |



### Infraestructura

| Recurso | Nombre | Plan |
|---|---|---|
| App Service | `ics-ecommerce-api` | Linux, Free F1 |
| SQL Server | `sql-server-ics-figueroa.database.windows.net` | — |
| SQL Database | `dsw-ecommerce-db` | Free Serverless, 32 GB |
| Región | Central US | — |

---

## 🔁 Migraciones

```bash
# Nueva migración
dotnet ef migrations add <Nombre> --project Dsw2025Tpi.Data --startup-project Dsw2025Tpi.Api

# Aplicar pendientes (después de cada pull)
dotnet ef database update --project Dsw2025Tpi.Data --startup-project Dsw2025Tpi.Api
```

## 🌿 Convención de Ramas

No se permiten commits directos a `main` ni `development`. Todo cambio entra por Pull Request.

| Rama | Propósito |
|---|---|
| `main` | Código estable, desplegable |
| `development` | Integración de features |
| `feature/<nombre>` | Funcionalidades nuevas (nace de `development`) |
| `hotfix/<descripcion>` | Correcciones urgentes (nace de `main`) |

Commits con formato [Conventional Commits](https://www.conventionalcommits.org/): `tipo(scope): descripción`

## 📄 Sobre el Proyecto

Plataforma e-commerce — módulo de Órdenes para la cátedra de Desarrollo de Software (UTN FRT, 2025/2026).

**Funcionalidades principales:**
- Consulta pública de productos (sin autenticación)
- Gestión de órdenes con verificación de stock
- Roles: Administrador (ABM productos + estados) y Cliente (crear/consultar órdenes)
- Autenticación JWT con Identity
