using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Reflection.Emit;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

var b = WebApplication.CreateBuilder(args);

b.Services.AddDbContext<Db>(o => o.UseSqlServer(b.Configuration.GetConnectionString("PharmaStockConn")));
b.Services.AddControllersWithViews();

// Autenticación por cookie: protege vistas (MVC) y API. /api responde 401/403 en vez de redirigir.
b.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(o =>
{
    o.LoginPath = "/Login";
    o.AccessDeniedPath = "/Denied";
    o.ExpireTimeSpan = TimeSpan.FromHours(8);
    o.SlidingExpiration = true;
    o.Cookie.HttpOnly = true;
    o.Cookie.SameSite = SameSiteMode.Strict;
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

    await h.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(id));

    return Results.Ok(new { ok = true, role, home = role == V ? "/Facturacion" : "/Dashboard" });
});

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
app.MapPost("/api/sales", async (SaleReq r, Db d, HttpContext h) =>
{
    if (r.items == null || r.items.Count == 0) return Err("Agrega al menos un producto.");
    if (r.items.Any(i => i.q <= 0)) return Err("Cantidad inválida.");

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
        d.MovimientosInventario.Add(new Movimiento { IdMedicamento = l.IdMedicamento, IdLote = l.IdLote, TipoMovimiento = "SALIDA", Cantidad = -i.q, Motivo = "Venta " + num, IdUsuario = v.IdUsuario });
        v.Subtotal += sub;
    }
    v.IVA = Math.Round(v.Subtotal * 0.13m, 2);
    v.Total = v.Subtotal + v.IVA;

    if (!string.IsNullOrWhiteSpace(r.cli))
        v.Cliente = await d.Clientes.FirstOrDefaultAsync(c => c.NombreCompleto == r.cli) ?? new Cliente { NombreCompleto = r.cli.Trim() };
    else
        v.IdCliente = (await d.Clientes.OrderBy(c => c.IdCliente).Select(c => c.IdCliente).FirstAsync());

    d.Ventas.Add(v);
    await d.SaveChangesAsync();
    await tx.CommitAsync();
    return Results.Ok(new { ok = true, num, sub = v.Subtotal, iva = v.IVA, total = v.Total });
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
record SaleReq(int uid, string cli, List<Item> items);
record AdjReq(int lot, int q, string why, int uid);
record UserReq(string nombre, string u, string p, int idRol);

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

    protected override void OnModelCreating(ModelBuilder mb)
    {
        foreach (var fk in mb.Model.GetEntityTypes().SelectMany(t => t.GetForeignKeys()))
            fk.DeleteBehavior = DeleteBehavior.Restrict;

        mb.Entity<Usr>().HasIndex(u => u.Usuario).IsUnique();
        mb.Entity<Medicamento>().HasIndex(m => m.CodigoBarras).IsUnique();
        mb.Entity<Venta>().HasIndex(v => v.NumeroFactura).IsUnique();
        // 💡 Indicar a EF Core que la tabla Lotes maneja Triggers en SQL Server
        mb.Entity<Lote>()
            .ToTable(tb => tb.HasTrigger("TR_Lotes_ActualizarStockMedicamento"));
    }

}