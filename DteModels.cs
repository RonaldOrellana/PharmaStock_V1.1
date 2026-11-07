using System.Text.Json.Serialization;

namespace PharmaStock.Models.DTE
{
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
        public string Ambiente { get; set; } = "00"; // "00" = Pruebas
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
}