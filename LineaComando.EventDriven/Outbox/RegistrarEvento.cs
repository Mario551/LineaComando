using System.Text.Json;
using Microsoft.Extensions.Logging;
using PER.Comandos.LineaComandos.EventDriven.Colas;

namespace PER.Comandos.LineaComandos.EventDriven.Outbox
{
    public class RegistrarEvento : IRegistrarEvento
    {
        private IColaEventos _colaEventos;
        private IColaEventosMemoria _colaEventosMemoria;
        private ILogger<RegistrarEvento> _logger;
        private string? _tipoEvento;
        private string? _datos;
        private long? _agregadoId;
        private bool  _agrumentosLlamados;

        public RegistrarEvento(
            IColaEventos colaEventos,
            IColaEventosMemoria colaEventosMemoria,
            ILogger<RegistrarEvento> logger)
        {
            _colaEventos = colaEventos;
            _colaEventosMemoria = colaEventosMemoria;
            _logger = logger;
            _agrumentosLlamados = false;
        }

        public IRegistrarEvento Argumentos<TDato>(string tipoEvento, TDato datos, long? agregadoId = null)
        {
            _tipoEvento = tipoEvento;
            _datos = JsonSerializer.Serialize(datos);
            _agregadoId = agregadoId;
            _agrumentosLlamados = true;

            return this;
        }

        public async Task RegistrarEnColaAsync()
        {
            if (!_agrumentosLlamados)
                throw new InvalidOperationException("Debe llamar a Argumentos() antes de RegistrarEnColaAsync()");

            long eventoId = await _colaEventos.GuardarEventoAsync(new DatosEvento
            {
                TipoEvento = _tipoEvento!,
                Datos = _datos!,
                AgregadoId = _agregadoId
            });

            EventoOutbox evento = new EventoOutbox
            {
                Id = eventoId,
                CodigoTipoEvento = _tipoEvento!,
                AgregadoId = _agregadoId,
                DatosEvento = _datos!,
                CreadoEn = DateTime.Now
            };

            try
            {
                await _colaEventosMemoria.EncolarAsync(evento);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Evento {EventoId} guardado en base de datos pero no se pudo encolar en memoria.",
                    eventoId);
            }
        }
    }
}
