using Microsoft.Extensions.Logging;
using PER.Comandos.LineaComandos.Atributo;
using PER.Comandos.LineaComandos.Cola.Almacen;
using PER.Comandos.LineaComandos.Comando;
using PER.Comandos.LineaComandos.Stream;

namespace ComandosColaTest.Helpers
{
    /// <summary>
    /// Comando de prueba que simula procesamiento y escribe resultado.
    /// </summary>
    public class ComandoPrueba : ComandoBase<string, ResultadoComando>
    {
        private ILogger? _logger;
        private string _mensaje = "Comando ejecutado correctamente";
        private bool _deberiaFallar = false;
        private int _tiempoEjecucionMs = 0;

        public static int ContadorEjecuciones { get; private set; } = 0;

        public ComandoPrueba(ILogger? logger = null)
        {
            _logger = logger;
        }

        public ComandoPrueba(string mensaje, bool deberiaFallar = false, int tiempoEjecucionMs = 0, ILogger? logger = null)
        {
            _mensaje = mensaje;
            _deberiaFallar = deberiaFallar;
            _tiempoEjecucionMs = tiempoEjecucionMs;
            _logger = logger;
        }

        public override void Preparar(ICollection<Parametro> parametros)
        {
            var paramMensaje = parametros.FirstOrDefault(p => p.Nombre == "--mensaje");
            if (paramMensaje != null)
            {
                _mensaje = paramMensaje.Valor?.ToString() ?? _mensaje;
            }

            var paramFallar = parametros.FirstOrDefault(p => p.Nombre == "--fallar");
            if (paramFallar != null)
            {
                _deberiaFallar = paramFallar.Valor?.ToString()?.ToLower() == "true";
            }
        }

        public override async Task EjecutarAsync(IStream<string, ResultadoComando> stream, CancellationToken token = default)
        {
            await EmpezarAsync(token);

            try
            {
                ContadorEjecuciones++;
                _logger?.LogInformation("Ejecutando comando de prueba: {Mensaje}", _mensaje);

                if (_tiempoEjecucionMs > 0)
                {
                    await Task.Delay(_tiempoEjecucionMs, token);
                }

                if (_deberiaFallar)
                {
                    stream.Writer.TryWrite(ResultadoComando.Fallo("Error simulado en comando de prueba"));
                }
                else
                {
                    stream.Writer.TryWrite(ResultadoComando.Exito(_mensaje));
                }
            }
            finally
            {
                await FinalizarAsync(token);
            }
        }

        public static void ResetearContador()
        {
            ContadorEjecuciones = 0;
        }
    }

    /// <summary>
    /// Comando de suma para pruebas con parámetros.
    /// </summary>
    public class ComandoSuma : ComandoBase<string, ResultadoComando>
    {
        private int _a;
        private int _b;

        public override void Preparar(ICollection<Parametro> parametros)
        {
            var paramA = parametros.FirstOrDefault(p => p.Nombre == "--a");
            var paramB = parametros.FirstOrDefault(p => p.Nombre == "--b");

            _a = int.Parse(paramA?.Valor?.ToString() ?? "0");
            _b = int.Parse(paramB?.Valor?.ToString() ?? "0");
        }

        public override async Task EjecutarAsync(IStream<string, ResultadoComando> stream, CancellationToken token = default)
        {
            await EmpezarAsync(token);

            try
            {
                var resultado = _a + _b;
                stream.Writer.TryWrite(ResultadoComando.Exito($"Resultado: {resultado}"));
            }
            finally
            {
                await FinalizarAsync(token);
            }
        }
    }
}
