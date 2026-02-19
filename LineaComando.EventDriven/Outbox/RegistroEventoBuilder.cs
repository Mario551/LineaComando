using System.Text.Json;

namespace PER.Comandos.LineaComandos.EventDriven.Outbox
{
    public class RegistroEventoBuilder : IRegistroEventoBuilder 
    {
        private IColaEventos _colaEventos;
        private string? _tipoEvento;
        private string? _datos;
        private long? _agregadoId;
        private bool  _agrumentosLlamados;

        public RegistroEventoBuilder(IColaEventos colaEventos)
        {
            _colaEventos = colaEventos;
            _agrumentosLlamados = false;
        }
        
        public IRegistroEventoBuilder Argumentos<TDato>(string tipoEvento, TDato datos, long? agregadoId)
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

            await _colaEventos.GuardarEventoAsync(new DatosEvento
            {
                TipoEvento = _tipoEvento!,
                Datos = _datos!,
                AgregadoId = _agregadoId    
            });
        }
    }
}