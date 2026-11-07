using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

public class DteFactura
{
    public Identificacion Identificacion { get; set; } = new();
    public Emisor Emisor { get; set; } = new();
    public Receptor Receptor { get; set; } = new();
    public List<CuerpoDocumento> CuerpoDocumento { get; set; } = new();
    public Resumen Resumen { get; set; } = new();
}

public class Identificacion
{
    public int Version { get; set; } = 1;
    public string Ambiente { get; set; } = "00"; // "00" = Pruebas, "01" = Producción
    public string TipoDte { get; set; } = "01";   // "01" = Factura
    public string NumeroControl { get; set; }
    public string CodigoGeneracion { get; set; }  // UUID v4
    public string FecEmi { get; set; }
    public string HorEmi { get; set; }
    public string TipoMoneda { get; set; } = "USD";
}

public class Emisor
{
    public string Nit { get; set; }
    public string Nrc { get; set; }
    public string Nombre { get; set; }
    public string CodActividad { get; set; }
    public string DescActividad { get; set; }
    public string Correo { get; set; }
}

public class Receptor
{
    public string TipoDocumento { get; set; } = "13"; // 13 = DUI / 36 = NIT
    public string NumDocumento { get; set; }
    public string Nombre { get; set; }
    public string Correo { get; set; }
}

public class CuerpoDocumento
{
    public int NumItem { get; set; }
    public int TipoItem { get; set; } = 1;
    public decimal Cantidad { get; set; }
    public string Codigo { get; set; }
    public string Descripcion { get; set; }
    public decimal PrecioUni { get; set; }
    public decimal VentaGravada { get; set; }
}

public class Resumen
{
    public decimal TotalGravada { get; set; }
    public decimal SubTotalVentas { get; set; }
    public decimal TotalIva { get; set; }
    public decimal TotalPagar { get; set; }
    public string TotalLetras { get; set; }
}

public class DteResult
{
    public bool Exito { get; set; }
    public string CodigoGeneracion { get; set; }
    public string NumeroControl { get; set; }
    public string SelloRecibido { get; set; }
    public string Error { get; set; }
}

// 💡 Cambiados a 'internal' para coincidir con la accesibilidad de Venta y MailLine en Program.cs
internal interface IDteService
{
    Task<DteResult> GenerarYProcesarDteAsync(Venta venta, List<MailLine> lineas, string correoCliente, string clienteNombre);
}

internal class DteService : IDteService
{
    private readonly IConfiguration _cfg;

    public DteService(IConfiguration cfg)
    {
        _cfg = cfg;
    }

    public async Task<DteResult> GenerarYProcesarDteAsync(Venta venta, List<MailLine> lineas, string correoCliente, string clienteNombre)
    {
        try
        {
            // Generación de UUID v4 oficial y Número de Control DTE
            string uuid = Guid.NewGuid().ToString().ToUpper();
            string numControl = $"DTE-01-F001-{venta.IdVenta:D15}";

            var dte = new DteFactura
            {
                Identificacion = new Identificacion
                {
                    CodigoGeneracion = uuid,
                    NumeroControl = numControl,
                    FecEmi = venta.FechaVenta.ToString("yyyy-MM-dd"),
                    HorEmi = venta.FechaVenta.ToString("HH:mm:ss")
                },
                Emisor = new Emisor
                {
                    Nit = _cfg["Dte:NitEmisor"] ?? "06141501901011",
                    Nrc = _cfg["Dte:NrcEmisor"] ?? "1234567",
                    Nombre = _cfg["Dte:NombreEmisor"] ?? "FARMACIA PHARMASTOCK S.A. DE C.V.",
                    CodActividad = "47720",
                    DescActividad = "Ventas al por menor de productos farmacéuticos",
                    Correo = _cfg["Smtp:Username"]
                },
                Receptor = new Receptor
                {
                    Nombre = string.IsNullOrWhiteSpace(clienteNombre) ? "Consumidor Final" : clienteNombre,
                    NumDocumento = "00000000-0",
                    Correo = correoCliente ?? ""
                },
                CuerpoDocumento = lineas.Select((l, i) => new CuerpoDocumento
                {
                    NumItem = i + 1,
                    Codigo = $"MED-{i + 1:D3}",
                    Descripcion = l.Producto,
                    Cantidad = l.Cant,
                    PrecioUni = l.PU,
                    VentaGravada = l.PU * l.Cant
                }).ToList(),
                Resumen = new Resumen
                {
                    TotalGravada = venta.Subtotal,
                    SubTotalVentas = venta.Subtotal,
                    TotalIva = venta.IVA,
                    TotalPagar = venta.Total,
                    TotalLetras = $"{venta.Total:N2} DÓLARES"
                }
            };

            // Simulación / Generación del Sello de Recepción
            string selloSimulado = Convert.ToHexString(SHA256.HashData(
                Encoding.UTF8.GetBytes(uuid + DateTime.Now.Ticks)
            )).Substring(0, 40);

            await Task.Delay(50);

            return new DteResult
            {
                Exito = true,
                CodigoGeneracion = uuid,
                NumeroControl = numControl,
                SelloRecibido = "SELLO-MH-" + selloSimulado,
                Error = null
            };
        }
        catch (Exception ex)
        {
            return new DteResult { Exito = false, Error = ex.Message };
        }
    }
}