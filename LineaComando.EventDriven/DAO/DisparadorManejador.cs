namespace PER.Comandos.LineaComandos.EventDriven.DAO
{
    public class DisparadorManejador
    {
        public int Id { get; set; }
        public int ManejadorEventoId { get; set; }
        public string Codigo { get; set; }

        /// <summary>
        /// Modo de disparo: "Evento" o "Programado".
        /// </summary>
        public string ModoDisparo { get; set; } = "Evento";
        public int? TipoEventoId { get; set; }

        /// <summary>
        /// Expresión cron (si ModoDisparo = "Programado").
        /// </summary>
        public string? Expresion { get; set; }
        public bool Activo { get; set; } = true;
        public int Prioridad { get; set; }
        public DateTime CreadoEn { get; set; } = DateTime.Now;
        public ManejadorEvento? ManejadorEvento { get; set; }
        public TipoEvento? TipoEvento { get; set; }
    }
}
