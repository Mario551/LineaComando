namespace PER.Comandos.LineaComandos.Persistence.DAO
{
        private class ComandoEnCola
        {
            public long Id { get; set; }
            public int IdComandoRegistrado { get; set; }
            public string RutaComando { get; set; } = string.Empty;
            public string? Argumentos { get; set; }
            public string? DatosComando { get; set; }
            public DateTime FechaCreacion { get; set; }
            public DateTime? FechaLeido { get; set; }
            public DateTime? FechaEjecucion { get; set; }
            public string Estado { get; set; } = "Pendiente";
            public string? MensajeError { get; set; }
            public string? Salida { get; set; }
            public long? DuracionMs { get; set; }
            public int Intentos { get; set; }
        }
}