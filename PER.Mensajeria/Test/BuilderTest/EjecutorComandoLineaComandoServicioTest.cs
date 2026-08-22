using System.Runtime.CompilerServices;
using PER.Comandos.LineaComandos.Cola.Almacen;
using PER.Comandos.LineaComandos.Cola.Colas;
using PER.Comandos.LineaComandos.Cola.Resultados;
using PER.Mensajeria.Aplicacion.Contexto;
using PER.Mensajeria.Builder.Contexto.EjecutorComandos.LineaComando;

namespace BuilderTest;

public class EjecutorComandoLineaComandoServicioTest
{
    [Fact]
    public async Task EncolarAsync_ConProcesador_DebeRetornarComandoId()
    {
        ColaComandosMemoriaFake cola = new(41);
        RegistroProcesadoresSerializacionResultadosComandoFake registro = new(true);
        EjecutorComandoLineaComandoServicio servicio = CrearServicio(cola, new AlmacenColaComandosFake(), registro);

        ReferenciaEjecucionComandoContexto referencia = await servicio.EncolarAsync(
            CrearSolicitud(),
            CancellationToken.None);

        Assert.Equal("lineacomando", referencia.Proveedor);
        Assert.Equal("41", referencia.IdentificadorExterno);
        Assert.Equal(1, cola.Encolados);
    }

    [Fact]
    public async Task EncolarAsync_SinProcesadorResultado_DebeFallarAntesDeEncolar()
    {
        ColaComandosMemoriaFake cola = new(41);
        EjecutorComandoLineaComandoServicio servicio = CrearServicio(
            cola,
            new AlmacenColaComandosFake(),
            new RegistroProcesadoresSerializacionResultadosComandoFake(false));

        InvalidOperationException excepcion = await Assert.ThrowsAsync<InvalidOperationException>(
            () => servicio.EncolarAsync(CrearSolicitud(), CancellationToken.None));

        Assert.Contains("Resultado", excepcion.Message);
        Assert.Equal(0, cola.Encolados);
    }

    [Theory]
    [InlineData("pendiente", EstadoEjecucionComandoExternaContextoTipo.Pendiente)]
    [InlineData("procesando", EstadoEjecucionComandoExternaContextoTipo.Procesando)]
    [InlineData("completado", EstadoEjecucionComandoExternaContextoTipo.Completado)]
    [InlineData("fallido", EstadoEjecucionComandoExternaContextoTipo.Fallido)]
    public async Task ConsultarAsync_DebeMapearEstadoPersistido(
        string estado,
        EstadoEjecucionComandoExternaContextoTipo esperado)
    {
        AlmacenColaComandosFake almacen = new()
        {
            Persistido = new ResultadoComandoPersistido
            {
                ComandoId = 41,
                Estado = estado
            }
        };
        EjecutorComandoLineaComandoServicio servicio = CrearServicio(
            new ColaComandosMemoriaFake(41),
            almacen,
            new RegistroProcesadoresSerializacionResultadosComandoFake(true));

        ConsultaEjecucionComandoContexto consulta = await servicio.ConsultarAsync(
            CrearReferencia(),
            CancellationToken.None);

        Assert.Equal(esperado, consulta.Estado);
    }

    [Fact]
    public async Task EsperarResultadoAsync_CompletadoSinPayload_DebeRetornarErrorConfiguracion()
    {
        AlmacenColaComandosFake almacen = new()
        {
            Persistido = new ResultadoComandoPersistido
            {
                ComandoId = 41,
                Estado = "completado"
            }
        };
        EjecutorComandoLineaComandoServicio servicio = CrearServicio(
            new ColaComandosMemoriaFake(41),
            almacen,
            new RegistroProcesadoresSerializacionResultadosComandoFake(true));

        ResultadoComandoContexto resultado = await servicio.EsperarResultadoAsync(
            CrearReferencia(),
            CancellationToken.None);

        Assert.False(resultado.Exitoso);
        Assert.Contains("payload durable", resultado.Error);
    }

    [Fact]
    public async Task AbandonarAsync_Procesando_DebeMarcarComandoFallido()
    {
        AlmacenColaComandosFake almacen = new()
        {
            Persistido = new ResultadoComandoPersistido
            {
                ComandoId = 41,
                Estado = "procesando"
            }
        };
        EjecutorComandoLineaComandoServicio servicio = CrearServicio(
            new ColaComandosMemoriaFake(41),
            almacen,
            new RegistroProcesadoresSerializacionResultadosComandoFake(true));

        await servicio.AbandonarAsync(CrearReferencia(), "reinicio", CancellationToken.None);

        Assert.Equal(41, almacen.IDAbandonado);
        Assert.Equal("reinicio", almacen.ResultadoAbandono?.MensajeError);
    }

    private static EjecutorComandoLineaComandoServicio CrearServicio(
        ColaComandosMemoriaFake cola,
        AlmacenColaComandosFake almacen,
        RegistroProcesadoresSerializacionResultadosComandoFake registro)
    {
        return new EjecutorComandoLineaComandoServicio(
            cola,
            almacen,
            new ResultadosComandosFake(),
            registro);
    }

    private static SolicitudEjecutarComandoContexto CrearSolicitud()
    {
        return new SolicitudEjecutarComandoContexto
        {
            Comando = new ComandoContexto
            {
                Codigo = "pedido consultar",
                Autorizado = true
            },
            Parametros = new Dictionary<string, string>
            {
                ["pedido"] = "54013"
            }
        };
    }

    private static ReferenciaEjecucionComandoContexto CrearReferencia()
    {
        return new ReferenciaEjecucionComandoContexto
        {
            Proveedor = "lineacomando",
            IdentificadorExterno = "41"
        };
    }

    private sealed class ColaComandosMemoriaFake : IColaComandosMemoria
    {
        private readonly long comandoId;

        public ColaComandosMemoriaFake(long comandoId)
        {
            this.comandoId = comandoId;
        }

        public int Encolados { get; private set; }

        public Task CargarPendientesDesdeBaseDatosAsync(CancellationToken token = default)
        {
            return Task.CompletedTask;
        }

        public Task<ComandoEncolado> EncolarAsync(SolicitudComando solicitud, CancellationToken token = default)
        {
            Encolados++;
            return Task.FromResult(new ComandoEncolado
            {
                ComandoId = comandoId,
                Resultado = Task.FromResult(ResultadoComando.Exito("resultado"))
            });
        }

        public Task<ComandoEncolado> EsperarComandoAsync(long id, CancellationToken token = default)
        {
            return Task.FromResult(new ComandoEncolado
            {
                ComandoId = id,
                Resultado = Task.FromResult(ResultadoComando.Exito("resultado"))
            });
        }

        public async IAsyncEnumerable<ComandoEnCola> LeerAsync(
            [EnumeratorCancellation] CancellationToken token = default)
        {
            await Task.CompletedTask;
            yield break;
        }

        public void CompletarResultado(long id, ResultadoComando resultado)
        {
        }
    }

    private sealed class AlmacenColaComandosFake : IAlmacenColaComandos
    {
        public ResultadoComandoPersistido? Persistido { get; set; }
        public long? IDAbandonado { get; private set; }
        public ResultadoComando? ResultadoAbandono { get; private set; }

        public Task<long> EncolarAsync(ComandoEnCola comando, CancellationToken token = default)
        {
            return Task.FromResult(comando.Id);
        }

        public Task<IEnumerable<ComandoEnCola>> ObtenerComandosPendientesAsync(
            int tamanioLote = 50,
            CancellationToken token = default)
        {
            return Task.FromResult<IEnumerable<ComandoEnCola>>([]);
        }

        public Task<IEnumerable<ComandoEnCola>> MarcarComandosProcesandoAsync(
            long[] ids,
            CancellationToken token = default)
        {
            return Task.FromResult<IEnumerable<ComandoEnCola>>([]);
        }

        public Task MarcarComoProcesadoAsync(
            long comandoId,
            ResultadoComando resultado,
            CancellationToken token = default)
        {
            IDAbandonado = comandoId;
            ResultadoAbandono = resultado;
            return Task.CompletedTask;
        }

        public Task MarcarComoProcesadoAsync(
            long comandoId,
            ResultadoComando resultado,
            PayloadResultadoComando? payloadResultado,
            CancellationToken token = default)
        {
            return MarcarComoProcesadoAsync(comandoId, resultado, token);
        }

        public Task<ResultadoComandoPersistido?> ObtenerResultadoPersistidoAsync(
            long comandoId,
            CancellationToken token = default)
        {
            return Task.FromResult(Persistido);
        }

        public Task ActualizarFechaLeidoAsync(long[] ids, CancellationToken token = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class ResultadosComandosFake : IResultadosComandos
    {
        public Task<ResultadoComando?> ObtenerResultadoAsync(long comandoId, CancellationToken token = default)
        {
            return Task.FromResult<ResultadoComando?>(ResultadoComando.Exito("resultado"));
        }
    }

    private sealed class RegistroProcesadoresSerializacionResultadosComandoFake
        : IRegistroProcesadoresSerializacionResultadosComando
    {
        private readonly IProcesadorResultadoComando? procesador;

        public RegistroProcesadoresSerializacionResultadosComandoFake(bool tieneProcesador)
        {
            procesador = tieneProcesador ? new ProcesadorResultadoComandoFake() : null;
        }

        public void Registrar(string rutaComando, IProcesadorResultadoComando procesadorResultado)
        {
        }

        public IProcesadorResultadoComando? ObtenerPorRutaComando(string rutaComando)
        {
            return procesador;
        }

        public IProcesadorResultadoComando? ObtenerPorTipoVersion(string tipo, int version)
        {
            return procesador;
        }
    }

    private sealed class ProcesadorResultadoComandoFake : IProcesadorResultadoComando
    {
        public string Tipo => "prueba";
        public int Version => 1;
        public string Formato => "json";

        public Task<string?> SerializarAsync(object? salida, CancellationToken token = default)
        {
            return Task.FromResult<string?>(salida?.ToString());
        }

        public Task<object?> DeserializarAsync(string? contenido, CancellationToken token = default)
        {
            return Task.FromResult<object?>(contenido);
        }
    }
}
