using System.Text;

namespace PER.Comandos.LineaComandos.Cola.Resultados
{
    public sealed class AlmacenamientoPayloadResultadoComando : IAlmacenamientoPayloadResultadoComando
    {
        private readonly OpcionesResultadosComandos _opciones;

        public AlmacenamientoPayloadResultadoComando(OpcionesResultadosComandos opciones)
        {
            _opciones = opciones ?? throw new ArgumentNullException(nameof(opciones));
        }

        public async Task<PayloadResultadoComando?> GuardarAsync(
            long comandoId,
            PayloadResultadoComando payload,
            CancellationToken token = default)
        {
            if (payload is null)
                throw new ArgumentNullException(nameof(payload));

            if (payload.Contenido is null)
                return null;

            int tamanoBytes = Encoding.UTF8.GetByteCount(payload.Contenido);
            if (tamanoBytes <= OpcionesResultadosComandos.TamanoMaximoPayloadBytes)
            {
                return new PayloadResultadoComando
                {
                    Tipo = payload.Tipo,
                    Version = payload.Version,
                    Formato = payload.Formato,
                    Contenido = payload.Contenido
                };
            }

            string rutaBase = ObtenerRutaBase();
            string rutaRelativa = CrearRutaRelativa(comandoId, payload);
            string rutaCompleta = CrearRutaCompleta(rutaBase, rutaRelativa);
            string? directorio = Path.GetDirectoryName(rutaCompleta);

            if (!string.IsNullOrWhiteSpace(directorio))
                Directory.CreateDirectory(directorio);

            await File.WriteAllTextAsync(rutaCompleta, payload.Contenido, Encoding.UTF8, token);

            return new PayloadResultadoComando
            {
                Tipo = payload.Tipo,
                Version = payload.Version,
                Formato = payload.Formato,
                RutaPayload = rutaRelativa
            };
        }

        public async Task<string?> LeerContenidoAsync(
            PayloadResultadoComando payload,
            CancellationToken token = default)
        {
            if (payload is null)
                throw new ArgumentNullException(nameof(payload));

            if (payload.Contenido is not null)
                return payload.Contenido;

            if (string.IsNullOrWhiteSpace(payload.RutaPayload))
                return null;

            string rutaCompleta = CrearRutaCompleta(ObtenerRutaBase(), payload.RutaPayload);

            return await File.ReadAllTextAsync(rutaCompleta, Encoding.UTF8, token);
        }

        private string ObtenerRutaBase()
        {
            if (string.IsNullOrWhiteSpace(_opciones.RutaBase))
                throw new InvalidOperationException("Debe configurar la ruta base de resultados de comandos para payloads mayores a 256KB.");

            return _opciones.RutaBase;
        }

        private static string CrearRutaRelativa(long comandoId, PayloadResultadoComando payload)
        {
            string tipo = SanitizarSegmento(payload.Tipo);
            string version = $"v{payload.Version}";
            string archivo = $"{comandoId}.{Guid.NewGuid():N}.payload";

            return $"{tipo}/{version}/{archivo}";
        }

        private static string CrearRutaCompleta(string rutaBase, string rutaRelativa)
        {
            return Path.Combine(rutaBase, rutaRelativa.Replace('/', Path.DirectorySeparatorChar));
        }

        private static string SanitizarSegmento(string valor)
        {
            char[] caracteres = valor
                .Select(c => char.IsLetterOrDigit(c) || c == '_' || c == '-' || c == '.' ? c : '_')
                .ToArray();

            string segmento = new string(caracteres);

            return string.IsNullOrWhiteSpace(segmento) ? "resultado" : segmento;
        }
    }
}
