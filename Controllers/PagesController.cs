using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[Authorize]
public class PagesController : Controller
{
    // Control de acceso por URL: un rol sin permiso recibe /Denied (403) aunque escriba la ruta a mano.
    const string A = "Administrador", S = "Supervisor", F = "Farmaceutico", V = "Vendedor";

    string Home() => User.IsInRole(V) ? "/Facturacion" : "/Dashboard";

    [AllowAnonymous]
    public IActionResult Login() => User.Identity?.IsAuthenticated == true ? Redirect(Home()) : View();

    [AllowAnonymous]
    public async Task<IActionResult> Logout() { await HttpContext.SignOutAsync(); return Redirect("/Login"); }

    public IActionResult Index() => Redirect(Home());
    public IActionResult Denied() => View();

    [Authorize(Roles = $"{A},{S},{F}")] public IActionResult Dashboard() => View();
    [Authorize(Roles = $"{A},{S},{F},{V}")] public IActionResult Facturacion() => View();
    [Authorize(Roles = $"{A},{S},{F}")] public IActionResult Catalogo() => View();
    [Authorize(Roles = $"{A},{S}")] public IActionResult Lotes() => View();
    [Authorize(Roles = $"{A},{S}")] public IActionResult Movimientos() => View();
    [Authorize(Roles = $"{A},{S}")] public IActionResult Reportes() => View();
    [Authorize(Roles = $"{A}")] public IActionResult Usuarios() => View();
}
public class SaleItemDto
{
    public int Lot { get; set; }
    public int Q { get; set; }
}

public class SaleRequestDto
{
    public string Cli { get; set; }
    public string Email { get; set; }          // Correo ingresado en el frontend
    public bool SendEmail { get; set; }        // Checkbox del frontend
    public List<SaleItemDto> Items { get; set; }
}