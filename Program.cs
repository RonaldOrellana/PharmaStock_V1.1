using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Globalization;
using System.Net;
using System.Net.Mail;
using System.Reflection.Emit;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

var b = WebApplication.CreateBuilder(args);

b.Services.AddDbContext<Db>(o => o.UseSqlServer(b.Configuration.GetConnectionString("PharmaStockConn")));
b.Services.AddControllersWithViews();
b.Services.AddSingleton<IDteService, DteService>();

// Autenticación por cookie: protege vistas (MVC) y API. /api responde 401/403 en vez de redirigir.
b.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(o =>
{
    o.LoginPath = "/Login";
    o.AccessDeniedPath = "/Denied";
    o.ExpireTimeSpan = TimeSpan.FromHours(8);
    o.SlidingExpiration = true;
    o.Cookie.HttpOnly = true;
    o.Cookie.SameSite = SameSiteMode.Strict;
    o.Cookie.MaxAge = null;
    o.Events.OnRedirectToLogin = c => { if (c.Request.Path.StartsWithSegments("/api")) c.Response.StatusCode = 401; else c.Response.Redirect(c.RedirectUri); return Task.CompletedTask; };
    o.Events.OnRedirectToAccessDenied = c => { if (c.Request.Path.StartsWithSegments("/api")) c.Response.StatusCode = 403; else c.Response.Redirect(c.RedirectUri); return Task.CompletedTask; };
});
b.Services.AddAuthorization();

var app = b.Build();

using (var s = app.Services.CreateScope())
{
    try
    {
        var d = s.ServiceProvider.GetRequiredService<Db>();
        d.Database.EnsureCreated();
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error al conectar/crear BD: {ex.Message}");
    }
}

app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllerRoute("default", "{action=Index}", new { controller = "Pages" });

const string A = "Administrador", S = "Supervisor", F = "Farmaceutico", V = "Vendedor";
IResult Err(string m) => Results.BadRequest(new { error = m });
int Uid(HttpContext h) => int.Parse(h.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
AuthorizeAttribute Roles(params string[] r) => new() { Roles = string.Join(",", r) };

// ---------- LOGIN (RF-01): emite cookie con el rol normalizado ----------
app.MapPost("/api/login", async (LoginReq r, Db d, HttpContext h) =>
{
    if (string.IsNullOrWhiteSpace(r.u) || string.IsNullOrWhiteSpace(r.p))
        return Results.Ok(new { ok = false, error = "Ingresa usuario y contraseña." });

    var u = await d.Usuarios.Include(x => x.Rol)
        .FirstOrDefaultAsync(x => x.Usuario.ToLower() == r.u.ToLower() && x.Estado);

    var passMatch = u != null && (
        string.Equals(u.ClaveHash, Hash(r.p), StringComparison.OrdinalIgnoreCase) ||
        string.Equals(u.ClaveHash, HashSql(r.p), StringComparison.OrdinalIgnoreCase)
    );

    if (u == null || !passMatch)
        return Results.Ok(new { ok = false, error = "Credenciales incorrectas. Revisa usuario y contraseña." });

    var role = NormRole(u.Rol.NombreRol);
    var id = new ClaimsIdentity(new[] {
        new Claim(ClaimTypes.NameIdentifier, u.IdUsuario.ToString()),
        new Claim(ClaimTypes.Name, u.Nombre),
        new Claim(ClaimTypes.Role, role)
    }, CookieAuthenticationDefaults.AuthenticationScheme);

    await h.SignInAsync(
         CookieAuthenticationDefaults.AuthenticationScheme,
         new ClaimsPrincipal(id),
         new AuthenticationProperties { IsPersistent = false, AllowRefresh = true });

    return Results.Ok(new { ok = true, role, home = role == V ? "/Facturacion" : "/Dashboard" });
});

// El usuario solicita recuperar su contraseña.
// No revela si el usuario existe.
app.MapPost("/api/password-recovery/request",
    async (SolicitarRecuperacionReq r, Db d) =>
    {
        var nombreUsuario = (r.usuario ?? "").Trim();

        if (nombreUsuario.Length < 3 || nombreUsuario.Length > 50)
            return Results.Ok(new
            {
                ok = true,
                mensaje = "Si la cuenta es válida, la solicitud será revisada."
            });

        var usuario = await d.Usuarios
            .Include(x => x.Rol)
            .FirstOrDefaultAsync(x =>
                x.Usuario.ToLower() == nombreUsuario.ToLower() &&
                x.Estado);

        // Nunca permitir recuperación de cuentas Administrador.
        if (usuario != null &&
            NormRole(usuario.Rol.NombreRol) != "Administrador")
        {
            var pendiente = await d.SolicitudesCambioClave
                .AnyAsync(x =>
                    x.IdUsuario == usuario.IdUsuario &&
                    x.Estado == "Pendiente");

            if (!pendiente)
            {
                d.SolicitudesCambioClave.Add(
                    new SolicitudCambioClave
                    {
                        IdUsuario = usuario.IdUsuario,
                        Estado = "Pendiente",
                        FechaSolicitud = DateTime.UtcNow
                    });

                await d.SaveChangesAsync();
            }
        }

        return Results.Ok(new
        {
            ok = true,
            mensaje = "Si la cuenta es válida, la solicitud será revisada por el Administrador."
        });
    })
.AllowAnonymous();


// El Administrador consulta solicitudes pendientes.
app.MapGet("/api/password-recovery/pending",
    async (Db d) =>
    {
        var solicitudes = await d.SolicitudesCambioClave
            .Include(x => x.Usuario)
            .Where(x => x.Estado == "Pendiente")
            .OrderBy(x => x.FechaSolicitud)
            .Select(x => new
            {
                id = x.IdSolicitud,
                usuario = x.Usuario.Usuario,
                nombre = x.Usuario.Nombre,
                fecha = x.FechaSolicitud
            })
            .ToListAsync();

        return Results.Ok(new
        {
            ok = true,
            solicitudes
        });
    })
.RequireAuthorization(Roles(A));


// Aprobar solicitud y generar código temporal.
app.MapPost("/api/password-recovery/approve/{id}",
    async (int id, Db d) =>
    {
        var solicitud = await d.SolicitudesCambioClave
            .Include(x => x.Usuario)
                .ThenInclude(x => x.Rol)
            .FirstOrDefaultAsync(x => x.IdSolicitud == id);

        if (solicitud == null)
            return Results.NotFound(new
            {
                ok = false,
                error = "La solicitud no existe."
            });

        if (solicitud.Estado != "Pendiente")
            return Err("La solicitud ya fue procesada.");

        if (solicitud.Usuario == null ||
            !solicitud.Usuario.Estado ||
            NormRole(solicitud.Usuario.Rol.NombreRol) == "Administrador")
            return Err("Esta cuenta no puede utilizar la recuperación.");

        // Código aleatorio criptográficamente seguro.
        string codigo = RandomNumberGenerator
            .GetInt32(0, 1_000_000)
            .ToString("D6");

        solicitud.CodigoHash = Hash(codigo);
        solicitud.Estado = "Aprobada";
        solicitud.FechaResolucion = DateTime.UtcNow;
        solicitud.FechaExpiracion = DateTime.UtcNow.AddMinutes(30);

        await d.SaveChangesAsync();

        // El código solo se devuelve en esta respuesta al Administrador.
        return Results.Ok(new
        {
            ok = true,
            codigo,
            usuario = solicitud.Usuario.Usuario,
            nombre = solicitud.Usuario.Nombre,
            expira = solicitud.FechaExpiracion,
            mensaje = "Entrega este código al usuario. No volverá a mostrarse."
        });
    })
.RequireAuthorization(Roles(A));


// Rechazar solicitud.
app.MapPost("/api/password-recovery/reject/{id}",
    async (int id, Db d) =>
    {
        var solicitud = await d.SolicitudesCambioClave
            .FirstOrDefaultAsync(x => x.IdSolicitud == id);

        if (solicitud == null)
            return Results.NotFound(new { error = "Solicitud no encontrada." });

        if (solicitud.Estado != "Pendiente")
            return Err("La solicitud ya fue procesada.");

        solicitud.Estado = "Rechazada";
        solicitud.FechaResolucion = DateTime.UtcNow;

        await d.SaveChangesAsync();

        return Results.Ok(new { ok = true });
    })
.RequireAuthorization(Roles(A));


// El usuario establece una nueva contraseña con su código.
app.MapPost("/api/password-recovery/reset",
    async (RecuperarClaveReq r, Db d) =>
    {
        var usuarioNombre = (r.usuario ?? "").Trim();
        var codigo = (r.codigo ?? "").Trim();
        var nuevaClave = r.nuevaClave ?? "";

        if (string.IsNullOrWhiteSpace(usuarioNombre) ||
            string.IsNullOrWhiteSpace(codigo) ||
            nuevaClave.Length < 8)
        {
            return Err("Completa los datos. La contraseña debe tener al menos 8 caracteres.");
        }

        if (codigo.Length != 6 || !codigo.All(char.IsDigit))
            return Err("El código de autorización no es válido.");

        await using var tx = await d.Database.BeginTransactionAsync();

        var usuario = await d.Usuarios
            .Include(x => x.Rol)
            .FirstOrDefaultAsync(x =>
                x.Usuario.ToLower() == usuarioNombre.ToLower() &&
                x.Estado);

        if (usuario == null ||
            NormRole(usuario.Rol.NombreRol) == "Administrador")
        {
            return Err("No fue posible validar la solicitud.");
        }

        var ahora = DateTime.UtcNow;
        var codigoHash = Hash(codigo);

        var solicitud = await d.SolicitudesCambioClave
            .Where(x =>
                x.IdUsuario == usuario.IdUsuario &&
                x.Estado == "Aprobada" &&
                x.CodigoHash == codigoHash &&
                x.FechaExpiracion != null &&
                x.FechaExpiracion > ahora)
            .OrderByDescending(x => x.FechaResolucion)
            .FirstOrDefaultAsync();

        if (solicitud == null)
            return Err("Código incorrecto, vencido o ya utilizado.");

        // Mantener compatibilidad con el login actual.
        usuario.ClaveHash = HashSql(nuevaClave);

        // Consumir el código: no podrá volver a utilizarse.
        solicitud.Estado = "Utilizada";
        solicitud.CodigoHash = null;
        solicitud.FechaResolucion = ahora;

        await d.SaveChangesAsync();
        await tx.CommitAsync();

        return Results.Ok(new
        {
            ok = true,
            mensaje = "Tu contraseña fue actualizada. Ya puedes iniciar sesión."
        });
    })
.AllowAnonymous();

// ---------- ESTADO PARA LA INTERFAZ (se recorta según el rol) ----------
app.MapGet("/api/state", async (Db d, HttpContext h) =>
{
    var role = h.User.FindFirstValue(ClaimTypes.Role);
    var meds = await d.Medicamentos.Include(m => m.Categoria).ToListAsync();
    var lots = await d.Lotes.ToListAsync();
    var sales = role is A or S or F
        ? await d.Ventas.Include(v => v.Usuario).Include(v => v.Cliente).Include(v => v.Detalles).ThenInclude(x => x.Medicamento).Include(v => v.Detalles).ThenInclude(x => x.Lote)
            .OrderByDescending(v => v.IdVenta).Take(500).ToListAsync()
        : new List<Venta>();
    var movs = role is A or S
        ? await d.MovimientosInventario.Include(m => m.Usuario).OrderByDescending(m => m.IdMovimiento).Take(300).ToListAsync()
        : new List<Movimiento>();

    var users = role is A
        ? await d.Usuarios.Include(u => u.Rol).Select(u => new {
            id = u.IdUsuario,
            name = u.Nombre,
            username = u.Usuario,
            role = u.Rol.NombreRol,
            active = u.Estado
        }).ToListAsync()
        : null;

    return new
    {
        cats = await d.Categorias.Select(c => c.NombreCategoria).ToListAsync(),
        provs = await d.Proveedores.Select(p => new { id = p.IdProveedor, n = p.NombreEmpresa, t = p.Telefono }).ToListAsync(),
        meds = meds.Select(m => new { id = m.IdMedicamento, code = m.CodigoBarras, n = m.NombreComercial, cat = m.Categoria.NombreCategoria, price = m.PrecioVenta, min = m.StockMinimo, stock = m.StockActual, on = m.Estado }),
        lots = lots.Select(l => new { id = l.IdLote, med = l.IdMedicamento, prov = l.IdProveedor, num = l.NumeroLote, exp = l.FechaVencimiento.ToString("yyyy-MM-dd"), ini = l.CantidadInicial, cur = l.CantidadActual }),
        sales = sales.Select(v => new
        {
            num = v.NumeroFactura,
            fecha = v.FechaVenta,
            user = v.Usuario != null ? v.Usuario.Nombre : "Sistema",
            cli = v.Cliente != null ? v.Cliente.NombreCompleto : "Consumidor final",
            items = v.Detalles.Select(x => new { n = x.Medicamento.NombreComercial, lote = x.Lote.NumeroLote, q = x.Cantidad, p = x.PrecioUnitario }),
            sub = v.Subtotal,
            iva = v.IVA,
            total = v.Total
        }),
        movs = movs.Select(m => new { id = m.IdMovimiento, med = m.IdMedicamento, lot = m.IdLote, t = m.TipoMovimiento, q = m.Cantidad, why = m.Motivo, u = m.Usuario != null ? m.Usuario.Nombre : "Sistema", d = m.FechaMovimiento.ToString("yyyy-MM-ddTHH:mm:ss") }),
        users = users
    };
}).RequireAuthorization();

// ---------- MEDICAMENTOS (RF-02, RN-04: solo Administrador) ----------
app.MapPost("/api/meds", async (MedReq r, Db d) =>
{
    var code = (r.code ?? "").Trim();
    if (code.Length == 0 || string.IsNullOrWhiteSpace(r.n)) return Err("Código de barras y nombre son obligatorios.");
    if (r.price <= 0) return Err("El precio debe ser mayor que cero (RN-04).");
    if (r.min < 0) return Err("El stock mínimo no puede ser negativo (RN-04).");
    if (await d.Medicamentos.AnyAsync(m => m.CodigoBarras == code)) return Err("Error: el código de barras ya existe.");
    var c = await d.Categorias.FirstOrDefaultAsync(x => x.NombreCategoria == r.cat);
    if (c == null) return Err("Categoría inválida.");

    d.Medicamentos.Add(new Medicamento { CodigoBarras = code, NombreComercial = r.n.Trim(), IdCategoria = c.IdCategoria, PrecioVenta = r.price, StockMinimo = r.min });
    await d.SaveChangesAsync();
    return Results.Ok(new { ok = true });
}).RequireAuthorization(Roles(A));

app.MapPost("/api/meds/{id}/toggle", async (int id, Db d) =>
{
    var m = await d.Medicamentos.FindAsync(id);
    if (m == null) return Err("No existe el medicamento.");
    m.Estado = !m.Estado;
    await d.SaveChangesAsync();
    return Results.Ok(new { ok = true });
}).RequireAuthorization(Roles(A));

// ---------- LOTES (RF-03: Administrador y Supervisor) ----------
app.MapPost("/api/lots", async (LotReq r, Db d, HttpContext h) =>
{
    try
    {
        if (r.med <= 0) return Err("Selecciona un medicamento válido.");
        if (r.prov <= 0) return Err("Selecciona un proveedor válido.");
        if (string.IsNullOrWhiteSpace(r.num)) return Err("El número de lote es obligatorio.");
        if (r.q <= 0) return Err("La cantidad debe ser mayor a 0.");
        if (!DateTime.TryParse(r.exp, out var expDate)) return Err("Fecha de vencimiento inválida.");
        if (expDate.Date < DateTime.Today) return Err("No se permite ingresar lotes con fecha de vencimiento pasada.");

        var num = r.num.Trim();
        if (await d.Lotes.AnyAsync(l => l.IdMedicamento == r.med && l.NumeroLote == num))
            return Err($"El número de lote '{num}' ya existe para este medicamento.");

        var m = await d.Medicamentos.FindAsync(r.med);
        if (m == null) return Err("El medicamento seleccionado no existe.");
        if (await d.Proveedores.FindAsync(r.prov) == null) return Err("El proveedor seleccionado no existe.");

        var l = new Lote { IdMedicamento = r.med, IdProveedor = r.prov, NumeroLote = num, FechaFabricacion = DateTime.Today, FechaVencimiento = expDate.Date, CantidadInicial = r.q, CantidadActual = r.q };
        d.Lotes.Add(l);
        m.StockActual += r.q;
        d.MovimientosInventario.Add(new Movimiento { IdMedicamento = m.IdMedicamento, Lote = l, TipoMovimiento = "ENTRADA", Cantidad = r.q, Motivo = "Ingreso de lote " + num, IdUsuario = Uid(h) });
        await d.SaveChangesAsync();
        return Results.Ok(new { ok = true, id = l.IdLote });
    }
    catch (DbUpdateException ex) { return Err("Error al guardar en la base de datos: " + (ex.InnerException?.Message ?? ex.Message)); }
}).RequireAuthorization(Roles(A, S));


// ---------- VENTAS (RF-05, RF-06: todos los roles). PEPS validado en servidor ----------
app.MapPost("/api/sales", async (SaleReq r, Db d, HttpContext h, IConfiguration config, IDteService dteService) =>
{
    if (r.items == null || r.items.Count == 0) return Err("Agrega al menos un producto.");
    if (r.items.Any(i => i.q <= 0)) return Err("Cantidad inválida.");

    // Correo del cliente (opcional): se envía salvo que el cliente haya desmarcado explícitamente el envío.
    string toEmail = null;
    if (!string.IsNullOrWhiteSpace(r.email) && r.sendEmail != false)
    {
        if (!System.Net.Mail.MailAddress.TryCreate(r.email.Trim(), out var ma))
            return Err("El correo del cliente no es válido.");
        toEmail = ma.Address;
    }
    var lines = new List<MailLine>();

    await using var tx = await d.Database.BeginTransactionAsync();
    var lotIds = r.items.Select(i => i.lot).Distinct().ToList();
    var medIds = await d.Lotes.Where(l => lotIds.Contains(l.IdLote)).Select(l => l.IdMedicamento).Distinct().ToListAsync();
    if (medIds.Count == 0) return Err("Lote inválido.");
    var pool = await d.Lotes.Include(l => l.Medicamento).Where(l => medIds.Contains(l.IdMedicamento)).ToListAsync();
    if (lotIds.Any(id => pool.All(l => l.IdLote != id))) return Err("Lote inválido.");

    var num = "FAC-" + ((await d.Ventas.CountAsync()) + 1).ToString("D6");
    var v = new Venta { NumeroFactura = num, IdUsuario = Uid(h), FechaVenta = DateTime.Now };

    foreach (var i in r.items.OrderBy(i => pool.First(l => l.IdLote == i.lot).FechaVencimiento))
    {
        var l = pool.First(x => x.IdLote == i.lot);
        if (!l.Medicamento.Estado) return Err("El medicamento " + l.Medicamento.NombreComercial + " está inactivo.");
        if (l.FechaVencimiento.Date < DateTime.Today) return Err("Bloqueado: el lote " + l.NumeroLote + " está vencido (RN-01).");
        if (l.CantidadActual < i.q) return Err("Existencias insuficientes en el lote " + l.NumeroLote + " (RN-02).");
        var earlier = pool.FirstOrDefault(x => x.IdMedicamento == l.IdMedicamento && x.IdLote != l.IdLote && x.CantidadActual > 0 && x.FechaVencimiento.Date >= DateTime.Today && x.FechaVencimiento < l.FechaVencimiento);
        if (earlier != null) return Err($"PEPS: debe venderse primero el lote {earlier.NumeroLote} (vence antes que {l.NumeroLote}).");

        l.CantidadActual -= i.q;
        l.Medicamento.StockActual -= i.q;
        var sub = l.Medicamento.PrecioVenta * i.q;
        v.Detalles.Add(new DetalleVenta { IdMedicamento = l.IdMedicamento, IdLote = l.IdLote, Cantidad = i.q, PrecioUnitario = l.Medicamento.PrecioVenta, Subtotal = sub });

        var codBarras = l.Medicamento.CodigoBarras ?? $"MED-{l.Medicamento.IdMedicamento:D3}";
        lines.Add(new MailLine(codBarras, l.Medicamento.NombreComercial, l.NumeroLote, i.q, l.Medicamento.PrecioVenta));

        d.MovimientosInventario.Add(new Movimiento { IdMedicamento = l.IdMedicamento, IdLote = l.IdLote, TipoMovimiento = "SALIDA", Cantidad = -i.q, Motivo = "Venta " + num, IdUsuario = v.IdUsuario });
        v.Subtotal += sub;
    }
    v.IVA = Math.Round(v.Subtotal * 0.13m, 2);
    v.Total = v.Subtotal + v.IVA;

    string clienteNombre = string.IsNullOrWhiteSpace(r.cli) ? "Consumidor final" : r.cli.Trim();
    if (!string.IsNullOrWhiteSpace(r.cli))
        v.Cliente = await d.Clientes.FirstOrDefaultAsync(c => c.NombreCompleto == r.cli) ?? new Cliente { NombreCompleto = clienteNombre };
    else
        v.IdCliente = (await d.Clientes.OrderBy(c => c.IdCliente).Select(c => c.IdCliente).FirstAsync());

    d.Ventas.Add(v);
    await d.SaveChangesAsync();
    await tx.CommitAsync();

    // Procesamiento DTE (Generación de Código de Generación UUID y Sello)
    var resDte = await dteService.GenerarYProcesarDteAsync(v, lines, toEmail, clienteNombre);

    // La venta ya está guardada: si el correo falla, NO se revierte; solo se informa al cajero.
    bool mailSent = false; string mailError = null;
    if (toEmail != null)
    {
        try
        {
            await InvoiceMailer.SendAsync(config, toEmail, clienteNombre,
                num, v.FechaVenta, h.User.FindFirstValue(ClaimTypes.Name),
                lines, v.Subtotal, v.IVA, v.Total,
                resDte.CodigoGeneracion, resDte.SelloRecibido);
            mailSent = true;
        }
        catch (Exception ex)
        {
            mailError = ex.InnerException?.InnerException?.Message ?? ex.InnerException?.Message ?? ex.Message;
            Console.WriteLine($"[Error SMTP] {toEmail}: {ex}");
        }
    }

    return Results.Ok(new
    {
        ok = true,
        num,
        sub = v.Subtotal,
        iva = v.IVA,
        total = v.Total,
        mail = mailSent,
        dte = resDte.Exito,
        codigoGeneracion = resDte.CodigoGeneracion,
        sello = resDte.SelloRecibido,
        mailError
    });
}).RequireAuthorization();

// ---------- AJUSTES (RF-08: Administrador y Supervisor, motivo obligatorio) ----------
app.MapPost("/api/adjust", async (AdjReq r, Db d, HttpContext h) =>
{
    var l = await d.Lotes.Include(x => x.Medicamento).FirstOrDefaultAsync(x => x.IdLote == r.lot);
    if (l == null || r.q == 0) return Err("Indica lote y una cantidad distinta de 0.");
    if (string.IsNullOrWhiteSpace(r.why) || r.why.Trim().Length < 5) return Err("La justificación es obligatoria (mín. 5 caracteres).");
    if (l.CantidadActual + r.q < 0) return Err("El ajuste dejaría el lote en negativo.");

    l.CantidadActual += r.q;
    l.Medicamento.StockActual += r.q;
    d.MovimientosInventario.Add(new Movimiento { IdMedicamento = l.IdMedicamento, IdLote = l.IdLote, TipoMovimiento = "AJUSTE", Cantidad = r.q, Motivo = r.why.Trim(), IdUsuario = Uid(h) });
    await d.SaveChangesAsync();
    return Results.Ok(new { ok = true });
}).RequireAuthorization(Roles(A, S));

// ---------- USUARIOS (RF-04: solo Administrador) ----------
app.MapGet("/api/users", async (Db d) =>
{
    var usuarios = await d.Usuarios
        .Include(u => u.Rol)
        .OrderBy(u => u.IdUsuario)
        .Select(u => new {
            id = u.IdUsuario,
            name = u.Nombre,
            username = u.Usuario,
            role = u.Rol.NombreRol,
            active = u.Estado
        })
        .ToListAsync();

    return Results.Ok(usuarios);
}).RequireAuthorization(Roles(A));

app.MapPost("/api/users", async (UserReq r, Db d) =>
{
    if (string.IsNullOrWhiteSpace(r.nombre)) return Err("El nombre completo es obligatorio.");
    if (string.IsNullOrWhiteSpace(r.u)) return Err("El nombre de usuario es obligatorio.");
    if (string.IsNullOrWhiteSpace(r.p) || r.p.Length < 6) return Err("La contraseña debe tener al menos 6 caracteres.");
    if (r.idRol <= 0) return Err("Debe seleccionar un rol válido.");

    var userClean = r.u.Trim();
    if (await d.Usuarios.AnyAsync(u => u.Usuario.ToLower() == userClean.ToLower()))
        return Err($"El usuario '{userClean}' ya está registrado.");

    var rol = await d.Roles.FindAsync(r.idRol);
    if (rol == null) return Err("El rol seleccionado no existe.");

    var nuevoUsuario = new Usr
    {
        Nombre = r.nombre.Trim(),
        Usuario = userClean,
        ClaveHash = HashSql(r.p),
        IdRol = r.idRol,
        Estado = true
    };

    d.Usuarios.Add(nuevoUsuario);
    await d.SaveChangesAsync();

    return Results.Ok(new { ok = true, id = nuevoUsuario.IdUsuario });
}).RequireAuthorization(Roles(A));

// ---------- RESTABLECER CONTRASEÑAS (SOLO ADMINISTRADOR) ----------
app.MapPost("/api/users/{id}/reset-password",
    async (int id, PasswordResetReq r, Db d) =>
    {
        var usuario = await d.Usuarios.FindAsync(id);

        if (usuario == null)
            return Results.NotFound(new
            {
                ok = false,
                error = "El usuario no existe."
            });

        string nuevaClave;

        if (r.generar)
        {
            // Generar contraseña temporal criptográficamente segura.
            const string caracteres =
                "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789!@#$%";

            var resultado = new StringBuilder(14);

            for (int i = 0; i < 14; i++)
            {
                int indice = RandomNumberGenerator.GetInt32(caracteres.Length);
                resultado.Append(caracteres[indice]);
            }

            nuevaClave = resultado.ToString();
        }
        else
        {
            nuevaClave = r.p ?? "";

            if (nuevaClave.Length < 8)
                return Results.BadRequest(new
                {
                    ok = false,
                    error = "La contraseña debe tener al menos 8 caracteres."
                });

            if (string.IsNullOrWhiteSpace(nuevaClave))
                return Results.BadRequest(new
                {
                    ok = false,
                    error = "Debes ingresar una contraseña."
                });
        }

        // Mantener compatibilidad con el login actual.
        usuario.ClaveHash = HashSql(nuevaClave);

        await d.SaveChangesAsync();

        // La contraseña se devuelve una sola vez para entregarla al usuario.
        return Results.Ok(new
        {
            ok = true,
            nombre = usuario.Nombre,
            usuario = usuario.Usuario,
            password = nuevaClave,
            mensaje = "Contraseña restablecida correctamente."
        });

    }).RequireAuthorization(Roles(A));

app.MapPost("/api/users/{id}/toggle", async (int id, Db d) =>
{
    var u = await d.Usuarios.FindAsync(id);
    if (u == null) return Err("El usuario especificado no existe.");

    u.Estado = !u.Estado;
    await d.SaveChangesAsync();
    return Results.Ok(new { ok = true, estado = u.Estado });
}).RequireAuthorization(Roles(A));

// ---------- INICIO DE APLICACIÓN ----------
app.Run();

// ---------- FUNCIONES AUXILIARES ----------
static string NormRole(string n) => (n ?? "").Trim().ToLowerInvariant() switch
{
    "administrador" or "admin" => "Administrador",
    "supervisor" => "Supervisor",
    "farmaceutico" or "farmacéutico" or "cajero" or "cajero/farmaceutico" => "Farmaceutico",
    _ => "Vendedor"
};

static string Hash(string s) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(s)));
static string HashSql(string s) => Convert.ToHexString(SHA256.HashData(Encoding.Unicode.GetBytes(s)));

// ---------- DTOs ----------
record LoginReq(string u, string p);
record MedReq(int uid, string code, string n, string cat, decimal price, int min);
record LotReq(int med, int prov, string num, string exp, int q, int uid);
record Item(int lot, int q);
record SaleReq(int uid, string cli, string email, bool? sendEmail, List<Item> items);
record AdjReq(int lot, int q, string why, int uid);
record UserReq(string nombre, string u, string p, int idRol);
record PasswordResetReq(string? p, bool generar);
record SolicitarRecuperacionReq(string usuario);
record AprobarRecuperacionReq(int id);
record RecuperarClaveReq(
    string usuario,
    string codigo,
    string nuevaClave
);




// ---------- ENTIDADES ----------
[Table("Roles")]
class Rol
{
    [Key] public int IdRol { get; set; }
    [MaxLength(50)] public string NombreRol { get; set; }
    [MaxLength(250)] public string Descripcion { get; set; }
}

[Table("Usuarios")]
class Usr
{
    [Key] public int IdUsuario { get; set; }
    [MaxLength(150)] public string Nombre { get; set; }
    [MaxLength(50)] public string Usuario { get; set; }
    [MaxLength(255)] public string ClaveHash { get; set; }
    public int IdRol { get; set; }
    [ForeignKey("IdRol")] public Rol Rol { get; set; }
    public bool Estado { get; set; } = true;
}

[Table("SolicitudesCambioClave")]
class SolicitudCambioClave
{
    [Key]
    public int IdSolicitud { get; set; }

    public int IdUsuario { get; set; }

    [ForeignKey(nameof(IdUsuario))]
    public Usr Usuario { get; set; }

    [MaxLength(20)]
    public string Estado { get; set; } = "Pendiente";

    [MaxLength(64)]
    public string CodigoHash { get; set; }

    public DateTime FechaSolicitud { get; set; } = DateTime.UtcNow;

    public DateTime? FechaExpiracion { get; set; }

    public DateTime? FechaResolucion { get; set; }
}

[Table("Categorias")]
class Categoria
{
    [Key] public int IdCategoria { get; set; }
    [MaxLength(100)] public string NombreCategoria { get; set; }
    [MaxLength(250)] public string Descripcion { get; set; }
}

[Table("Proveedores")]
class Proveedor
{
    [Key] public int IdProveedor { get; set; }
    [MaxLength(150)] public string NombreEmpresa { get; set; }
    [MaxLength(20)] public string Telefono { get; set; }
    [MaxLength(120)] public string Email { get; set; }
    [MaxLength(250)] public string Direccion { get; set; }
}

[Table("Medicamentos")]
class Medicamento
{
    [Key] public int IdMedicamento { get; set; }
    [MaxLength(30)] public string CodigoBarras { get; set; }
    [MaxLength(150)] public string NombreComercial { get; set; }
    public int IdCategoria { get; set; }
    [ForeignKey("IdCategoria")] public Categoria Categoria { get; set; }
    [Precision(10, 2)] public decimal PrecioVenta { get; set; }
    public int StockActual { get; set; }
    public int StockMinimo { get; set; } = 10;
    public bool Estado { get; set; } = true;
}

[Table("Lotes")]
class Lote
{
    [Key] public int IdLote { get; set; }
    public int IdMedicamento { get; set; }
    [ForeignKey("IdMedicamento")] public Medicamento Medicamento { get; set; }
    public int IdProveedor { get; set; }
    [ForeignKey("IdProveedor")] public Proveedor Proveedor { get; set; }
    [MaxLength(50)] public string NumeroLote { get; set; }
    [Column(TypeName = "date")] public DateTime? FechaFabricacion { get; set; }
    [Column(TypeName = "date")] public DateTime FechaVencimiento { get; set; }
    public int CantidadInicial { get; set; }
    public int CantidadActual { get; set; }
}

[Table("Clientes")]
class Cliente
{
    [Key] public int IdCliente { get; set; }
    [MaxLength(150)] public string NombreCompleto { get; set; }
    [MaxLength(20)] public string NIT_DUI { get; set; }
    [MaxLength(20)] public string Telefono { get; set; }
    [MaxLength(250)] public string Direccion { get; set; }
}

[Table("Ventas")]
class Venta
{
    [Key] public int IdVenta { get; set; }
    [MaxLength(20)] public string NumeroFactura { get; set; }
    public int IdUsuario { get; set; }
    [ForeignKey("IdUsuario")] public Usr Usuario { get; set; }
    public int IdCliente { get; set; }
    [ForeignKey("IdCliente")] public Cliente Cliente { get; set; }
    public DateTime FechaVenta { get; set; } = DateTime.Now;
    [Precision(12, 2)] public decimal Subtotal { get; set; }
    [Precision(12, 2)] public decimal IVA { get; set; }
    [Precision(12, 2)] public decimal Total { get; set; }
    [MaxLength(15)] public string Estado { get; set; } = "EMITIDA";
    public List<DetalleVenta> Detalles { get; set; } = new();
}

[Table("DetalleVentas")]
class DetalleVenta
{
    [Key] public int IdDetalle { get; set; }
    public int IdVenta { get; set; }
    [ForeignKey("IdVenta")] public Venta Venta { get; set; }
    public int IdMedicamento { get; set; }
    [ForeignKey("IdMedicamento")] public Medicamento Medicamento { get; set; }
    public int IdLote { get; set; }
    [ForeignKey("IdLote")] public Lote Lote { get; set; }
    public int Cantidad { get; set; }
    [Precision(10, 2)] public decimal PrecioUnitario { get; set; }
    [Precision(12, 2)] public decimal Subtotal { get; set; }
}

[Table("MovimientosInventario")]
class Movimiento
{
    [Key] public int IdMovimiento { get; set; }
    public int IdMedicamento { get; set; }
    [ForeignKey("IdMedicamento")] public Medicamento Medicamento { get; set; }
    public int? IdLote { get; set; }
    [ForeignKey("IdLote")] public Lote Lote { get; set; }
    [MaxLength(10)] public string TipoMovimiento { get; set; }
    public int Cantidad { get; set; }
    public DateTime FechaMovimiento { get; set; } = DateTime.Now;
    [MaxLength(250)] public string Motivo { get; set; }
    public int IdUsuario { get; set; }
    [ForeignKey("IdUsuario")] public Usr Usuario { get; set; }
}

// ---------- DB CONTEXT ----------
class Db : DbContext
{
    public Db(DbContextOptions<Db> o) : base(o) { }

    public DbSet<Rol> Roles { get; set; }
    public DbSet<Usr> Usuarios { get; set; }
    public DbSet<Categoria> Categorias { get; set; }
    public DbSet<Proveedor> Proveedores { get; set; }
    public DbSet<Medicamento> Medicamentos { get; set; }
    public DbSet<Lote> Lotes { get; set; }
    public DbSet<Cliente> Clientes { get; set; }
    public DbSet<Venta> Ventas { get; set; }
    public DbSet<DetalleVenta> DetalleVentas { get; set; }
    public DbSet<Movimiento> MovimientosInventario { get; set; }
    public DbSet<SolicitudCambioClave> SolicitudesCambioClave { get; set; }

    protected override void OnModelCreating(ModelBuilder mb)
    {
        foreach (var fk in mb.Model.GetEntityTypes().SelectMany(t => t.GetForeignKeys()))
            fk.DeleteBehavior = DeleteBehavior.Restrict;

        mb.Entity<Usr>().HasIndex(u => u.Usuario).IsUnique();
        mb.Entity<Medicamento>().HasIndex(m => m.CodigoBarras).IsUnique();
        mb.Entity<Venta>().HasIndex(v => v.NumeroFactura).IsUnique();
        mb.Entity<Lote>()
            .ToTable(tb => tb.HasTrigger("TR_Lotes_ActualizarStockMedicamento"));
        mb.Entity<SolicitudCambioClave>()
    .HasIndex(x => new { x.IdUsuario, x.Estado });
    }
}

// ---------- ENVÍO DE FACTURA POR CORREO CON FORMATO DTE, CÓDIGO DE BARRAS Y ADJUNTO ----------
record MailLine(string Codigo, string Producto, string Lote, int Cant, decimal PU);

static class InvoiceMailer
{
    static string M(decimal v) => "$" + v.ToString("N2", CultureInfo.InvariantCulture);
    static string H(string s) => WebUtility.HtmlEncode(s ?? "");

    public static async Task SendAsync(IConfiguration cfg, string to, string cliente, string num,
        DateTime fecha, string atendio, List<MailLine> lines, decimal sub, decimal iva, decimal total,
        string codigoGen = null, string sello = null)
    {
        var user = cfg["Smtp:Username"];
        var pass = cfg["Smtp:Password"];
        if (string.IsNullOrWhiteSpace(user) || string.IsNullOrWhiteSpace(pass))
            throw new InvalidOperationException("SMTP no configurado (appsettings.json > Smtp).");

        var cleanPass = pass.Replace(" ", "").Trim();

        Console.WriteLine($"[SMTP] usuario='{user}' | largo de clave={cleanPass.Length}");
        var rows = new StringBuilder();
        int index = 0;
        foreach (var l in lines)
        {
            var bg = index % 2 == 0 ? "#ffffff" : "#f8fafc";
            rows.Append($@"<tr style='background-color:{bg}'>
<td style='padding:10px 12px;border-bottom:1px solid #e2e8f0;font-family:monospace;font-size:12px;color:#475569'>{H(l.Codigo)}</td>
<td style='padding:10px 12px;border-bottom:1px solid #e2e8f0;font-weight:600;color:#0f172a'>{H(l.Producto)}</td>
<td style='padding:10px 12px;border-bottom:1px solid #e2e8f0;color:#64748b'>{H(l.Lote)}</td>
<td style='padding:10px 12px;border-bottom:1px solid #e2e8f0;text-align:center;font-weight:600'>{l.Cant}</td>
<td style='padding:10px 12px;border-bottom:1px solid #e2e8f0;text-align:right;color:#334155'>{M(l.PU)}</td>
<td style='padding:10px 12px;border-bottom:1px solid #e2e8f0;text-align:right;font-weight:600;color:#0f172a'>{M(l.PU * l.Cant)}</td></tr>");
            index++;
        }

        // Generación dinámica de la imagen del CÓDIGO DE BARRAS (Code 128)
        string barcodeUrl = !string.IsNullOrWhiteSpace(codigoGen)
            ? $"https://barcodeapi.org/api/128/{WebUtility.UrlEncode(codigoGen)}"
            : $"https://barcodeapi.org/api/128/{WebUtility.UrlEncode(num)}";

        string bloqDte = string.IsNullOrWhiteSpace(codigoGen) ? "" : $@"
<div style='background-color:#f0f9ff;border:1px solid #bae6fd;border-left:4px solid #0284c7;padding:16px;border-radius:8px;margin:18px 0;text-align:center;'>
  <div style='font-size:12px;font-weight:700;color:#0369a1;margin-bottom:8px;text-transform:uppercase;letter-spacing:0.5px;'>
    📄 DOCUMENTO TRIBUTARIO ELECTRÓNICO (DTE - TIPO 01)
  </div>

  <div style='background:#ffffff;padding:10px;border-radius:6px;display:inline-block;margin:6px 0;border:1px solid #e2e8f0;'>
    <img src='{barcodeUrl}' alt='Código de Barras DTE' style='max-width:100%;height:60px;display:block;margin:0 auto;' />
    <span style='font-family:monospace;font-size:11px;color:#475569;margin-top:4px;display:block;'>{H(codigoGen)}</span>
  </div>

  <div style='font-size:11px;color:#64748b;margin-top:6px;word-break:break-all;'>
    <b>Sello de Recepción:</b> <span style='font-family:monospace;color:#0f172a;'>{H(sello)}</span>
  </div>
</div>";

        var html = $@"
<!DOCTYPE html>
<html>
<head>
  <meta charset='utf-8'>
  <meta name='viewport' content='width=device-width, initial-scale=1.0'>
  <title>Factura Electrónica DTE {H(num)}</title>
</head>
<body style='margin:0;padding:20px;background-color:#f1f5f9;font-family:-apple-system,BlinkMacSystemFont,""Segoe UI"",Roboto,Helvetica,Arial,sans-serif;'>
  <div style='max-width:680px;margin:0 auto;background-color:#ffffff;border-radius:12px;overflow:hidden;box-shadow:0 4px 6px -1px rgba(0,0,0,0.1);border:1px solid #e2e8f0;'>
    
    <!-- Header -->
    <div style='background:linear-gradient(135deg, #1e3a8a 0%, #1e40af 100%);padding:28px 24px;color:#ffffff;'>
      <table style='width:100%;border-collapse:collapse;'>
        <tr>
          <td>
            <h1 style='margin:0;font-size:24px;font-weight:800;color:#ffffff;'>PharmaStock</h1>
            <p style='margin:4px 0 0;font-size:13px;color:#93c5fd;text-transform:uppercase;'>Comprobante Digital DTE</p>
          </td>
          <td style='text-align:right;vertical-align:top;'>
            <span style='background-color:rgba(255,255,255,0.15);padding:6px 12px;border-radius:20px;font-size:13px;font-weight:600;'>{H(num)}</span>
          </td>
        </tr>
      </table>
    </div>

    <!-- Datos generales -->
    <div style='padding:16px 24px;background-color:#fafafa;border-bottom:1px solid #e2e8f0;font-size:13px;color:#475569;'>
      <table style='width:100%;border-collapse:collapse;'>
        <tr>
          <td style='padding-bottom:4px;'><b>Cliente:</b> <span style='color:#0f172a;'>{H(cliente)}</span></td>
          <td style='padding-bottom:4px;text-align:right;'><b>Fecha:</b> <span style='color:#0f172a;'>{fecha:dd/MM/yyyy HH:mm}</span></td>
        </tr>
        <tr>
          <td><b>Atendió:</b> <span style='color:#0f172a;'>{H(atendio)}</span></td>
          <td style='text-align:right;'><b>Estado:</b> <span style='color:#16a34a;font-weight:600;'>● EMITIDA</span></td>
        </tr>
      </table>
    </div>

    <!-- Cuerpo del correo -->
    <div style='padding:24px;'>
      {bloqDte}

      <!-- Tabla de Productos -->
      <div style='overflow-x:auto;margin-top:16px;'>
        <table style='width:100%;border-collapse:collapse;font-size:13px;'>
          <thead>
            <tr style='background-color:#1e3a8a;color:#ffffff;'>
              <th style='padding:10px 12px;text-align:left;border-top-left-radius:6px;'>Código</th>
              <th style='padding:10px 12px;text-align:left;'>Producto</th>
              <th style='padding:10px 12px;text-align:left;'>Lote</th>
              <th style='padding:10px 12px;text-align:center;'>Cant.</th>
              <th style='padding:10px 12px;text-align:right;'>P. Unit.</th>
              <th style='padding:10px 12px;text-align:right;border-top-right-radius:6px;'>Subtotal</th>
            </tr>
          </thead>
          <tbody>
            {rows}
          </tbody>
        </table>
      </div>

      <!-- Resumen de Totales -->
      <div style='margin-top:20px;'>
        <table style='width:240px;margin-left:auto;border-collapse:collapse;font-size:13px;'>
          <tr>
            <td style='padding:6px 0;color:#64748b;'>Subtotal:</td>
            <td style='padding:6px 0;text-align:right;font-weight:600;color:#334155;'>{M(sub)}</td>
          </tr>
          <tr>
            <td style='padding:6px 0;color:#64748b;'>IVA (13%):</td>
            <td style='padding:6px 0;text-align:right;font-weight:600;color:#334155;'>{M(iva)}</td>
          </tr>
          <tr style='border-top:2px solid #e2e8f0;'>
            <td style='padding:10px 0 0;font-size:16px;font-weight:700;color:#1e3a8a;'>TOTAL:</td>
            <td style='padding:10px 0 0;text-align:right;font-size:18px;font-weight:800;color:#1e3a8a;'>{M(total)}</td>
          </tr>
        </table>
      </div>
    </div>

    <!-- Footer -->
    <div style='background-color:#f8fafc;padding:16px 24px;border-top:1px solid #e2e8f0;text-align:center;font-size:12px;color:#64748b;'>
      📎 <b>Comprobante de Facturación Electrónica DTE adjunto y en línea.</b><br/>
      Gracias por su compra en <b>PharmaStock</b>.
    </div>

  </div>
</body>
</html>";

        var builder = new MimeKit.BodyBuilder { HtmlBody = html };

        // Adjuntar como documento descargable interactivo de la factura
        byte[] docBytes = Encoding.UTF8.GetBytes(html);
        builder.Attachments.Add($"Comprobante_Factura_{num}.html", docBytes, MimeKit.ContentType.Parse("text/html"));

        var msg = new MimeKit.MimeMessage();
        msg.From.Add(new MimeKit.MailboxAddress(cfg["Smtp:SenderName"] ?? "PharmaStock DTE", cfg["Smtp:SenderEmail"] ?? user));
        msg.To.Add(MimeKit.MailboxAddress.Parse(to));
        msg.Subject = $"Factura Electrónica DTE {num} - PharmaStock";
        msg.Body = builder.ToMessageBody();

        using var smtp = new MailKit.Net.Smtp.SmtpClient();
        await smtp.ConnectAsync(cfg["Smtp:Server"] ?? "smtp.gmail.com",
            int.Parse(cfg["Smtp:Port"] ?? "587"),
            MailKit.Security.SecureSocketOptions.StartTls);
        await smtp.AuthenticateAsync(user, cleanPass);
        await smtp.SendAsync(msg);
        await smtp.DisconnectAsync(true);
    }
}