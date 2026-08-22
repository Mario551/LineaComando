using PER.Comandos.LineaComandos.Cola.Almacen;

namespace PER.Comandos.LineaComandos.Cola.Resultados
{
    public sealed class ResultadosComandos : IResultadosComandos
    {
        private readonly IAlmacenColaComandos _almacenColaComandos;
        private readonly IRegistroProcesadoresSerializacionResultadosComando
            _registroProcesadoresSerializacionResultados;
        private readonly IAlmacenamientoPayloadResultadoComando _almacenamientoPayload;

        public ResultadosComandos(
            IAlmacenColaComandos almacenColaComandos,
            IRegistroProcesadoresSerializacionResultadosComando registroProcesadoresSerializacionResultados,
            IAlmacenamientoPayloadResultadoComando almacenamientoPayload)
        {
            _almacenColaComandos = almacenColaComandos ?? throw new ArgumentNullException(nameof(almacenColaComandos));
            _registroProcesadoresSerializacionResultados = registroProcesadoresSerializacionResultados
                ?? throw new ArgumentNullException(nameof(registroProcesadoresSerializacionResultados));
            _almacenamientoPayload = almacenamientoPayload ?? throw new ArgumentNullException(nameof(almacenamientoPayload));
        }

        public async Task<ResultadoComando?> ObtenerResultadoAsync(long comandoId, CancellationToken token = default)
        {
            ResultadoComandoPersistido? resultadoPersistido = await _almacenColaComandos.ObtenerResultadoPersistidoAsync(
                comandoId,
                token);

            if (resultadoPersistido is null)
                return null;

            if (resultadoPersistido.Estado is "pendiente" or "procesando")
                return null;

            if (resultadoPersistido.Estado == "fallido")
                return ResultadoComando.Fallo(
                    resultadoPersistido.MensajeError ?? "El comando falló sin registrar mensaje de error.",
                    resultadoPersistido.Duracion);

            if (resultadoPersistido.Estado != "completado")
                throw new InvalidOperationException($"El estado '{resultadoPersistido.Estado}' no es válido para recuperar resultado.");

            object? salida = null;
            if (resultadoPersistido.PayloadResultado is not null)
            {
                IProcesadorResultadoComando procesador =
                    _registroProcesadoresSerializacionResultados.ObtenerPorTipoVersion(
                    resultadoPersistido.PayloadResultado.Tipo,
                    resultadoPersistido.PayloadResultado.Version)
                    ?? throw new InvalidOperationException(
                        $"No existe procesador para el resultado '{resultadoPersistido.PayloadResultado.Tipo}' versión {resultadoPersistido.PayloadResultado.Version}.");

                string? contenido = await _almacenamientoPayload.LeerContenidoAsync(
                    resultadoPersistido.PayloadResultado,
                    token);

                salida = await procesador.DeserializarAsync(contenido, token);
            }

            return ResultadoComando.Exito(salida, resultadoPersistido.Duracion);
        }
    }
}
