PHARMASTOCK - ASP.NET Core 8 MVC + Entity Framework Core + SQL Server

REQUISITOS: Visual Studio 2022 o .NET 8 SDK, y SQL Server (Express/LocalDB sirve).

PASOS
1. Abrir PharmaStock.sln. Editar appsettings.json -> ConnectionStrings:PharmaStockConn con su servidor.
2. F5. Crea la BD (EnsureCreated) y carga roles, usuarios y medicamentos de ejemplo (idempotente).
3. Usuarios de prueba:
   admin        / Admin123*   -> Administrador (acceso total)
   supervisor1  / Super123*   -> Supervisor (lotes, ajustes, reportes)
   farma1       / Farma123*   -> Farmaceutico (POS, catalogo, alertas)
   vendedor1    / Vende123*   -> Vendedor (solo punto de venta)
   Si su BD ya tenia roles "Admin"/"Cajero", siguen funcionando (Cajero = Farmaceutico).

ESTRUCTURA
 Controllers/PagesController.cs   Rutas MVC protegidas por rol ([Authorize(Roles=...)])
 Views/Shared/_Layout.cshtml      Sidebar por rol + campana de alertas
 Views/Pages/*.cshtml             Dashboard, Facturacion, Catalogo, Lotes, Movimientos, Reportes, Login
 wwwroot/css/pharma.css           Tema farmaceutico (sin dependencias)
 wwwroot/js/pharma.js             Estado, alertas, menu por rol, utilidades
 Program.cs                       Auth por cookie + API (PEPS, RN-01/02/04 validados tambien en servidor)

Chart.js y SweetAlert2 se cargan desde jsdelivr (requiere internet). PDF: Imprimir > Guardar como PDF.
