# ICS2026 – Backend API (.NET 8)

API REST de comercio electrónico construida con ASP.NET Core 8, Entity Framework Core, SQL Server, Identity y JWT.  
Containerizada con Docker multi-stage y desplegada en Azure App Service con pipeline CI/CD automatizado.

##  Estructura del Proyecto

```
Dsw2025Tpi.sln
├── tests/
│   ├── Dsw2025Tpi.UnitTests        → Pruebas unitarias con xUnit y Moq
│   └── Dsw2025Tpi.IntegrationTests → Pruebas de integración con SQLite en memoria
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

## Pruebas automatizadas

Desde la raíz del repositorio, con el SDK de .NET 8, ejecutá todas las pruebas:

```bash
dotnet test Dsw2025Tpi.sln
```

Para ejecutar solo las pruebas de creación de productos:

```bash
dotnet test tests/Dsw2025Tpi.UnitTests/Dsw2025Tpi.UnitTests.csproj --filter FullyQualifiedName~ProductServiceTests
```

### Creación de productos (TP4)

Las pruebas de [`ProductServiceTests`](tests/Dsw2025Tpi.UnitTests/ProductServiceTests.cs) llaman a `ProductService.CreateAsync` y simulan el repositorio y la unidad de trabajo con Moq. Siguen el patrón **Arrange, Act, Assert**: preparar los datos, ejecutar la operación y verificar el resultado.

| Escenario | Comportamiento esperado |
|---|---|
| iPhone con datos válidos | Devuelve el producto y llama una sola vez a `Add` y a `SaveChangesAsync`. |
| AirPods existentes con el mismo SKU | Lanza `ProductSkuAlreadyExistsException` y no agrega ni guarda otro producto. |
| Stock cero, stock negativo o precio negativo | Lanza el error correspondiente antes de consultar el repositorio o guardar cambios. |

Los tres escenarios se ejecutan como cinco casos. La validación de valores inválidos es una prueba parametrizada con tres entradas. El stock inicial debe ser positivo al crear un producto. Un producto existente puede quedar con stock cero.

Las pruebas unitarias no necesitan una base de datos. Las de integración inician la API en entorno `Testing` con SQLite en memoria.

## 📄 Sobre el Proyecto

Plataforma e-commerce — módulo de Órdenes para la cátedra de Desarrollo de Software (UTN FRT, 2025/2026).

**Funcionalidades principales:**
- Consulta pública de productos (sin autenticación)
- Gestión de órdenes con verificación de stock
- Roles: Administrador (ABM productos + estados) y Cliente (crear/consultar órdenes)
- Autenticación JWT con Identity
