using System.Collections.Concurrent;
using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using PER.Comandos.LineaComandos.Cola.Almacen;
using PER.Comandos.LineaComandos.Cola.Resultados;

namespace PER.Comandos.LineaComandos.Cola.Colas
{
    public sealed class ColaComandosMemoria : IColaComandosMemoria
    {
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly Channel<ComandoEnCola> _channel;
        private readonly ConcurrentDictionary<long, EsperaComando> _esperas;
        private readonly ILogger<ColaComandosMemoria> _logger;

        public ColaComandosMemoria(IServiceScopeFactory serviceScopeFactory)
            : this(serviceScopeFactory, NullLogger<ColaComandosMemoria>.Instance)
        {
        }

        public ColaComandosMemoria(
            IServiceScopeFactory serviceScopeFactory,
            ILogger<ColaComandosMemoria> logger)
        {
            _serviceScopeFactory = serviceScopeFactory ?? throw new ArgumentNullException(nameof(serviceScopeFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _channel = Channel.CreateUnbounded<ComandoEnCola>();
            _esperas = new ConcurrentDictionary<long, EsperaComando>();
        }

        public async Task CargarPendientesDesdeBaseDatosAsync(CancellationToken token = default)
        {
            using IServiceScope scope = _serviceScopeFactory.CreateScope();
            IAlmacenColaComandos almacenColaComandos = scope.ServiceProvider.GetRequiredService<IAlmacenColaComandos>();

            IEnumerable<ComandoEnCola> comandosPendientes = await almacenColaComandos.ObtenerComandosPendientesAsync(
                int.MaxValue,
                token);

            foreach (ComandoEnCola comandoPendiente in comandosPendientes)
            {
                ObtenerOCrearEspera(comandoPendiente.Id, false, out bool creada);

                try
                {
                    await _channel.Writer.WriteAsync(comandoPendiente, token);
                }
                catch
                {
                    if (creada)
                        _esperas.TryRemove(comandoPendiente.Id, out _);

                    throw;
                }
            }
        }

        public async Task<ComandoEncolado> EncolarAsync(SolicitudComando solicitud, CancellationToken token = default)
        {
            ComandoEnCola comando = CrearComandoEnCola(solicitud);

            using IServiceScope scope = _serviceScopeFactory.CreateScope();
            IAlmacenColaComandos almacenColaComandos = scope.ServiceProvider.GetRequiredService<IAlmacenColaComandos>();

            comando.Id = await almacenColaComandos.EncolarAsync(comando, token);

            EsperaComando espera = ObtenerOCrearEspera(comando.Id, true, out bool creada);

            try
            {
                await _channel.Writer.WriteAsync(comando, token);

                return new ComandoEncolado
                {
                    ComandoId = comando.Id,
                    Resultado = espera.Fuente.Task
                };
            }
            catch (Exception ex)
            {
                if (creada)
                    _esperas.TryRemove(comando.Id, out _);

                _logger.LogError(
                    ex,
                    "Comando {ComandoId} persistido en base de datos pero no se pudo encolar en memoria.",
                    comando.Id);
                throw;
            }
        }

        public async Task<ComandoEncolado> EsperarComandoAsync(long comandoId, CancellationToken token = default)
        {
            if (comandoId <= 0)
                throw new ArgumentException("El id del comando debe ser mayor a cero.", nameof(comandoId));

            if (_esperas.TryGetValue(comandoId, out EsperaComando? esperaExistente))
            {
                esperaExistente.MarcarReclamada();
                return CrearComandoEncolado(comandoId, esperaExistente);
            }

            using IServiceScope scope = _serviceScopeFactory.CreateScope();
            IResultadosComandos resultadosComandos = scope.ServiceProvider.GetRequiredService<IResultadosComandos>();

            ResultadoComando? resultado = await resultadosComandos.ObtenerResultadoAsync(comandoId, token);
            if (resultado is not null)
            {
                return new ComandoEncolado
                {
                    ComandoId = comandoId,
                    Resultado = Task.FromResult(resultado)
                };
            }

            IAlmacenColaComandos almacenColaComandos = scope.ServiceProvider.GetRequiredService<IAlmacenColaComandos>();
            ResultadoComandoPersistido? resultadoPersistido = await almacenColaComandos.ObtenerResultadoPersistidoAsync(
                comandoId,
                token);

            if (resultadoPersistido is null)
                throw new InvalidOperationException($"El comando {comandoId} no existe.");

            if (resultadoPersistido.Estado is not "pendiente" and not "procesando")
                throw new InvalidOperationException($"El estado '{resultadoPersistido.Estado}' no es válido para esperar el comando {comandoId}.");

            EsperaComando espera = ObtenerOCrearEspera(comandoId, true, out _);

            resultado = await resultadosComandos.ObtenerResultadoAsync(comandoId, token);
            if (resultado is not null)
                CompletarResultado(comandoId, resultado);

            return CrearComandoEncolado(comandoId, espera);
        }

        public IAsyncEnumerable<ComandoEnCola> LeerAsync(CancellationToken token = default)
        {
            return _channel.Reader.ReadAllAsync(token);
        }

        public void CompletarResultado(long comandoId, ResultadoComando resultado)
        {
            if (_esperas.TryRemove(comandoId, out EsperaComando? espera))
                espera.Fuente.TrySetResult(resultado);
        }

        private EsperaComando ObtenerOCrearEspera(long comandoId, bool reclamada, out bool creada)
        {
            while (true)
            {
                if (_esperas.TryGetValue(comandoId, out EsperaComando? esperaExistente))
                {
                    if (reclamada)
                        esperaExistente.MarcarReclamada();

                    creada = false;
                    return esperaExistente;
                }

                EsperaComando espera = new EsperaComando();
                if (reclamada)
                    espera.MarcarReclamada();

                if (_esperas.TryAdd(comandoId, espera))
                {
                    creada = true;
                    return espera;
                }
            }
        }

        private static ComandoEncolado CrearComandoEncolado(long comandoId, EsperaComando espera)
        {
            return new ComandoEncolado
            {
                ComandoId = comandoId,
                Resultado = espera.Fuente.Task
            };
        }

        private static ComandoEnCola CrearComandoEnCola(SolicitudComando solicitud)
        {
            return new ComandoEnCola
            {
                RutaComando = solicitud.RutaComando,
                Argumentos = solicitud.Argumentos,
                DatosDeComando = solicitud.DatosDeComando,
                FechaCreacion = DateTime.Now,
                Estado = "pendiente",
                Intentos = 0
            };
        }

        private sealed class EsperaComando
        {
            public TaskCompletionSource<ResultadoComando> Fuente { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

            public bool Reclamada { get; private set; }

            public void MarcarReclamada()
            {
                Reclamada = true;
            }
        }
    }
}
