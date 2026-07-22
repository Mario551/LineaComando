using System.Text.Json;
using AplicacionTest.Infraestructura;
using Microsoft.EntityFrameworkCore;
using PER.Mensajeria.Aplicacion.Contexto;
using PER.Mensajeria.Aplicacion.Contexto.EjecucionComando;
using PER.Mensajeria.Datos.Contexto;
using PER.Mensajeria.Entidad.DAO;
using PER.Mensajeria.Entidad.DTO;

namespace AplicacionTest;

public class EjecucionComandoContextoAplicacionTest
{
    public static IEnumerable<object[]> MotoresYEscenariosReintento
    {
        get
        {
            foreach (object[] motor in BaseDatosPrueba.Motores)
            {
                yield return [motor[0], "encolando"];
                yield return [motor[0], "procesando"];
                yield return [motor[0], "abandonando"];
                yield return [motor[0], "inexistente"];
            }
        }
    }

    public static IEnumerable<object[]> MotoresYEstadosRecuperables
    {
        get
        {
            foreach (object[] motor in BaseDatosPrueba.Motores)
            {
                yield return [motor[0], EstadoEjecucionComandoExternaContextoTipo.Pendiente];
                yield return [motor[0], EstadoEjecucionComandoExternaContextoTipo.Completado];
            }
        }
    }

    [Theory]
    [MemberData(nameof(BaseDatosPrueba.Motores), MemberType = typeof(BaseDatosPrueba))]
    public async Task ReanudarActivaAsync_Preparada_DebeEncolarYCompletar(
        MotorBaseDatosPrueba motor)
    {
        await using BaseDatosPrueba baseDatos = await BaseDatosPrueba.CrearAsync(motor);
        ContextoEjecucionPrueba contextoPrueba = await CrearContextoPruebaAsync(baseDatos);
        EjecutorComandoContextoServicioFake ejecutor = new(
            EstadoEjecucionComandoExternaContextoTipo.Pendiente,
            ResultadoComandoContexto.Exito("pedido encontrado"));
        EjecucionComandoContextoAplicacion aplicacion = CrearAplicacion(contextoPrueba, ejecutor);

        ResultadoEjecucionComandoContexto? resultado = await aplicacion.ReanudarActivaAsync(
            contextoPrueba.Solicitud,
            [contextoPrueba.Comando],
            CancellationToken.None);

        Assert.NotNull(resultado);
        Assert.True(resultado.Resultado.Exitoso);
        Assert.Equal(1, ejecutor.Encolados);
        Assert.Equal(1, ejecutor.Esperados);
        await AssertIntentosAsync(baseDatos, contextoPrueba.Solicitud.IDProcesamientoInternoMensaje, 1, "completada");
    }

    [Theory]
    [MemberData(nameof(MotoresYEstadosRecuperables))]
    public async Task ReanudarActivaAsync_EncoladaRecuperable_DebeEsperarMismoIdentificadorSinReencolar(
        MotorBaseDatosPrueba motor,
        EstadoEjecucionComandoExternaContextoTipo estadoExterno)
    {
        await using BaseDatosPrueba baseDatos = await BaseDatosPrueba.CrearAsync(motor);
        ContextoEjecucionPrueba contextoPrueba = await CrearContextoPruebaAsync(
            baseDatos,
            EstadosEjecucionComandoContexto.Encolada,
            "comando-existente");
        EjecutorComandoContextoServicioFake ejecutor = new(
            estadoExterno,
            ResultadoComandoContexto.Exito("resultado durable"));
        EjecucionComandoContextoAplicacion aplicacion = CrearAplicacion(contextoPrueba, ejecutor);

        ResultadoEjecucionComandoContexto? resultado = await aplicacion.ReanudarActivaAsync(
            contextoPrueba.Solicitud,
            [contextoPrueba.Comando],
            CancellationToken.None);

        Assert.NotNull(resultado);
        Assert.True(resultado.Resultado.Exitoso);
        Assert.Equal(0, ejecutor.Encolados);
        Assert.Equal(1, ejecutor.Esperados);
        Assert.Equal("comando-existente", Assert.Single(ejecutor.ReferenciasEsperadas));
        await AssertIntentosAsync(baseDatos, contextoPrueba.Solicitud.IDProcesamientoInternoMensaje, 1, "completada");
    }

    [Theory]
    [MemberData(nameof(BaseDatosPrueba.Motores), MemberType = typeof(BaseDatosPrueba))]
    public async Task ReanudarActivaAsync_ExternaFallida_DebeRegistrarToolYNoReencolar(
        MotorBaseDatosPrueba motor)
    {
        await using BaseDatosPrueba baseDatos = await BaseDatosPrueba.CrearAsync(motor);
        ContextoEjecucionPrueba contextoPrueba = await CrearContextoPruebaAsync(
            baseDatos,
            EstadosEjecucionComandoContexto.Encolada,
            "comando-fallido");
        EjecutorComandoContextoServicioFake ejecutor = new(
            EstadoEjecucionComandoExternaContextoTipo.Fallido,
            ResultadoComandoContexto.Fallo("fallo durable"));
        EjecucionComandoContextoAplicacion aplicacion = CrearAplicacion(contextoPrueba, ejecutor);

        ResultadoEjecucionComandoContexto? resultado = await aplicacion.ReanudarActivaAsync(
            contextoPrueba.Solicitud,
            [contextoPrueba.Comando],
            CancellationToken.None);

        Assert.NotNull(resultado);
        Assert.False(resultado.Resultado.Exitoso);
        Assert.Equal(0, ejecutor.Encolados);
        Assert.Equal(1, ejecutor.Esperados);
        await AssertIntentosAsync(baseDatos, contextoPrueba.Solicitud.IDProcesamientoInternoMensaje, 1, "fallida");
    }

    [Theory]
    [MemberData(nameof(MotoresYEscenariosReintento))]
    public async Task ReanudarActivaAsync_EjecucionNoContinuable_DebeCrearHijaYRelanzar(
        MotorBaseDatosPrueba motor,
        string escenario)
    {
        string estadoLocal = escenario switch
        {
            "encolando" => EstadosEjecucionComandoContexto.Encolando,
            "abandonando" => EstadosEjecucionComandoContexto.Abandonando,
            _ => EstadosEjecucionComandoContexto.Encolada
        };
        string? identificador = escenario == "encolando" ? null : "comando-anterior";
        EstadoEjecucionComandoExternaContextoTipo estadoExterno = escenario switch
        {
            "procesando" => EstadoEjecucionComandoExternaContextoTipo.Procesando,
            "inexistente" => EstadoEjecucionComandoExternaContextoTipo.Inexistente,
            _ => EstadoEjecucionComandoExternaContextoTipo.Pendiente
        };

        await using BaseDatosPrueba baseDatos = await BaseDatosPrueba.CrearAsync(motor);
        ContextoEjecucionPrueba contextoPrueba = await CrearContextoPruebaAsync(
            baseDatos,
            estadoLocal,
            identificador);
        EjecutorComandoContextoServicioFake ejecutor = new(
            estadoExterno,
            ResultadoComandoContexto.Exito("resultado reintentado"));
        EjecucionComandoContextoAplicacion aplicacion = CrearAplicacion(contextoPrueba, ejecutor);

        ResultadoEjecucionComandoContexto? resultado = await aplicacion.ReanudarActivaAsync(
            contextoPrueba.Solicitud,
            [contextoPrueba.Comando],
            CancellationToken.None);

        Assert.NotNull(resultado);
        Assert.True(resultado.Resultado.Exitoso);
        Assert.Equal(1, ejecutor.Encolados);
        Assert.Equal(1, ejecutor.Esperados);
        Assert.Equal(escenario is "procesando" or "abandonando" ? 1 : 0, ejecutor.Abandonados);

        await using MensajeriaContextoDB contexto = baseDatos.CrearContexto();
        List<DAOEjecucionComandoContexto> intentos = await contexto.EjecucionesComandoContexto
            .AsNoTracking()
            .Where(ejecucion => ejecucion.IDProcesamientoInternoMensaje == contextoPrueba.Solicitud.IDProcesamientoInternoMensaje)
            .OrderBy(ejecucion => ejecucion.NumeroIntento)
            .ToListAsync();
        Assert.Equal(2, intentos.Count);
        Assert.False(intentos[0].Activa);
        Assert.Equal(
            escenario is "procesando" or "abandonando" ? "abandonada" : "incierta",
            intentos[0].IDEstadoEjecucionComandoContexto);
        Assert.Equal(intentos[0].ID, intentos[1].IDEjecucionAnterior);
        Assert.Equal(2, intentos[1].NumeroIntento);
        Assert.Equal("completada", intentos[1].IDEstadoEjecucionComandoContexto);
        Assert.False(intentos[1].Activa);
        Assert.NotEqual(identificador, intentos[1].IdentificadorExterno);
    }

    [Theory]
    [MemberData(nameof(BaseDatosPrueba.Motores), MemberType = typeof(BaseDatosPrueba))]
    public async Task ReanudarActivaAsync_DebeDisponerUnitOfWorkAntesDeLlamadasExternas(
        MotorBaseDatosPrueba motor)
    {
        await using BaseDatosPrueba baseDatos = await BaseDatosPrueba.CrearAsync(motor);
        ContextoEjecucionPrueba contextoPrueba = await CrearContextoPruebaAsync(baseDatos);
        EjecutorComandoContextoServicioFake ejecutor = new(
            EstadoEjecucionComandoExternaContextoTipo.Pendiente,
            ResultadoComandoContexto.Exito("pedido encontrado"),
            () =>
            {
                Assert.True(contextoPrueba.UnitOfWorkFactory.AlcancesCreados > 0);
                Assert.Equal(0, contextoPrueba.UnitOfWorkFactory.AlcancesActivos);
                Assert.Equal(
                    contextoPrueba.UnitOfWorkFactory.AlcancesCreados,
                    contextoPrueba.UnitOfWorkFactory.AlcancesDispuestos);
            });
        EjecucionComandoContextoAplicacion aplicacion = CrearAplicacion(contextoPrueba, ejecutor);

        ResultadoEjecucionComandoContexto? resultado = await aplicacion.ReanudarActivaAsync(
            contextoPrueba.Solicitud,
            [contextoPrueba.Comando],
            CancellationToken.None);

        Assert.NotNull(resultado);
        Assert.True(resultado.Resultado.Exitoso);
        Assert.Equal(2, ejecutor.ValidacionesSinUnitOfWorkActivo);
        Assert.Equal(0, contextoPrueba.UnitOfWorkFactory.AlcancesActivos);
        Assert.Equal(
            contextoPrueba.UnitOfWorkFactory.AlcancesCreados,
            contextoPrueba.UnitOfWorkFactory.AlcancesDispuestos);
    }

    private static EjecucionComandoContextoAplicacion CrearAplicacion(
        ContextoEjecucionPrueba contextoPrueba,
        IEjecutorComandoContextoServicio ejecutor)
    {
        return new EjecucionComandoContextoAplicacion(
            contextoPrueba.UnitOfWorkFactory,
            ejecutor,
            contextoPrueba.RegistrarContextoIAAplicacion);
    }

    private static async Task<ContextoEjecucionPrueba> CrearContextoPruebaAsync(
        BaseDatosPrueba baseDatos,
        string estado = EstadosEjecucionComandoContexto.Preparada,
        string? identificadorExterno = null)
    {
        (DAOMensaje mensaje, DAOProcesamientoInternoMensaje procesamiento) =
            await baseDatos.CrearMensajeEntradaPendienteAsync();
        UnitOfWorkFactoryPrueba unitOfWorkFactory = new(baseDatos);
        RegistrarContextoIAAplicacion registrar = new(unitOfWorkFactory);
        await using MensajeriaContextoDB contexto = baseDatos.CrearContexto();
        DAOLineaConversacion linea = await contexto.LineasConversacion.AsNoTracking()
            .SingleAsync(lineaActual => lineaActual.ID == mensaje.IDLineaConversacion);
        SolicitudContextoConversacion solicitud = new()
        {
            IDProcesamientoInternoMensaje = procesamiento.ID,
            IDMensaje = mensaje.ID,
            IDConversacion = linea.IDConversacion,
            IDLineaConversacion = linea.ID,
            Contenido = mensaje.Contenido,
            FechaMensaje = mensaje.FechaMensaje
        };

        await registrar.RegistrarMetadataEntradaAsync(
            new SolicitudRegistrarMetadataEntradaContextoIA
            {
                IDLineaConversacion = linea.ID,
                IDMensaje = mensaje.ID,
                IDProcesamientoInternoMensaje = procesamiento.ID,
                IDRolContextoIA = "user",
                IDTipoEntradaContextoIA = "mensaje_entrada",
                Contenido = mensaje.Contenido,
                FechaEntrada = mensaje.FechaMensaje
            },
            CancellationToken.None);

        ResultadoRegistrarDecisionContextoIA registro = await registrar.RegistrarDecisionAsync(
            solicitud,
            CrearInformacionTecnicaLlamadaIA(),
            new SolicitudRegistrarMetadataEntradaContextoIA
            {
                IDLineaConversacion = linea.ID,
                IDMensaje = mensaje.ID,
                IDProcesamientoInternoMensaje = procesamiento.ID,
                IDRolContextoIA = "assistant",
                IDTipoEntradaContextoIA = "decision_comando",
                Contenido = "pedido consultar",
                ToolCallID = "tool-prueba",
                FechaEntrada = DateTime.Now
            },
            new SolicitudPrepararEjecucionComandoContexto
            {
                ProveedorEjecucion = "fake",
                CodigoComando = "pedido consultar",
                ParametrosJson = JsonSerializer.Serialize(new Dictionary<string, string>
                {
                    ["pedido"] = "54013"
                })
            },
            CancellationToken.None);
        EjecucionComandoContexto ejecucion = Assert.IsType<EjecucionComandoContexto>(registro.EjecucionComando);

        if (estado != EstadosEjecucionComandoContexto.Preparada || identificadorExterno is not null)
        {
            DAOEjecucionComandoContexto dao = await contexto.EjecucionesComandoContexto
                .SingleAsync(ejecucionActual => ejecucionActual.ID == ejecucion.ID);
            dao.IDEstadoEjecucionComandoContexto = estado;
            dao.IdentificadorExterno = identificadorExterno;
            dao.FechaInicioEncolado = DateTime.Now;
            dao.FechaEncolado = identificadorExterno is null ? null : DateTime.Now;
            await contexto.SaveChangesAsync();
            contexto.Entry(dao).State = EntityState.Detached;
        }

        return new ContextoEjecucionPrueba(
            unitOfWorkFactory,
            registrar,
            solicitud,
            new ComandoContexto
            {
                Codigo = "pedido consultar",
                Descripcion = "Consulta pedido",
                Autorizado = true
            });
    }

    private static InformacionTecnicaLlamadaIAContexto CrearInformacionTecnicaLlamadaIA()
    {
        return new InformacionTecnicaLlamadaIAContexto
        {
            Proveedor = "fake",
            Modelo = "fake",
            Adaptador = "fake",
            Iteracion = 1,
            AccionDecidida = nameof(AccionContextoTipo.Comando),
            Content = "pedido consultar"
        };
    }

    private static async Task AssertIntentosAsync(
        BaseDatosPrueba baseDatos,
        long idProcesamiento,
        int cantidad,
        string estado)
    {
        await using MensajeriaContextoDB contexto = baseDatos.CrearContexto();
        List<DAOEjecucionComandoContexto> intentos = await contexto.EjecucionesComandoContexto
            .AsNoTracking()
            .Where(ejecucion => ejecucion.IDProcesamientoInternoMensaje == idProcesamiento)
            .ToListAsync();
        Assert.Equal(cantidad, intentos.Count);
        Assert.Equal(estado, intentos[^1].IDEstadoEjecucionComandoContexto);
        Assert.False(intentos[^1].Activa);
        Assert.NotNull(intentos[^1].IDMetadataEntradaResultadoContextoIA);
    }

    private sealed record ContextoEjecucionPrueba(
        UnitOfWorkFactoryPrueba UnitOfWorkFactory,
        RegistrarContextoIAAplicacion RegistrarContextoIAAplicacion,
        SolicitudContextoConversacion Solicitud,
        ComandoContexto Comando);

    private sealed class EjecutorComandoContextoServicioFake : IEjecutorComandoContextoServicio
    {
        private readonly EstadoEjecucionComandoExternaContextoTipo estadoConsulta;
        private readonly ResultadoComandoContexto resultado;
        private readonly Action? validarSinUnitOfWorkActivo;

        public EjecutorComandoContextoServicioFake(
            EstadoEjecucionComandoExternaContextoTipo estadoConsulta,
            ResultadoComandoContexto resultado,
            Action? validarSinUnitOfWorkActivo = null)
        {
            this.estadoConsulta = estadoConsulta;
            this.resultado = resultado;
            this.validarSinUnitOfWorkActivo = validarSinUnitOfWorkActivo;
        }

        public string Proveedor => "fake";
        public int Encolados { get; private set; }
        public int Esperados { get; private set; }
        public int Abandonados { get; private set; }
        public int ValidacionesSinUnitOfWorkActivo { get; private set; }
        public List<string> ReferenciasEsperadas { get; } = [];

        public Task<ReferenciaEjecucionComandoContexto> EncolarAsync(
            SolicitudEjecutarComandoContexto solicitud,
            CancellationToken cancellationToken)
        {
            ValidarSinUnitOfWorkActivo();
            Encolados++;
            return Task.FromResult(new ReferenciaEjecucionComandoContexto
            {
                Proveedor = Proveedor,
                IdentificadorExterno = $"comando-nuevo-{Encolados}"
            });
        }

        public Task<ConsultaEjecucionComandoContexto> ConsultarAsync(
            ReferenciaEjecucionComandoContexto referencia,
            CancellationToken cancellationToken)
        {
            ValidarSinUnitOfWorkActivo();
            return Task.FromResult(new ConsultaEjecucionComandoContexto
            {
                Estado = estadoConsulta,
                Error = estadoConsulta == EstadoEjecucionComandoExternaContextoTipo.Inexistente
                    ? "No existe"
                    : null
            });
        }

        public Task<ResultadoComandoContexto> EsperarResultadoAsync(
            ReferenciaEjecucionComandoContexto referencia,
            CancellationToken cancellationToken)
        {
            ValidarSinUnitOfWorkActivo();
            Esperados++;
            ReferenciasEsperadas.Add(referencia.IdentificadorExterno);
            return Task.FromResult(resultado);
        }

        public Task AbandonarAsync(
            ReferenciaEjecucionComandoContexto referencia,
            string motivo,
            CancellationToken cancellationToken)
        {
            ValidarSinUnitOfWorkActivo();
            Abandonados++;
            return Task.CompletedTask;
        }

        private void ValidarSinUnitOfWorkActivo()
        {
            if (validarSinUnitOfWorkActivo is null)
            {
                return;
            }

            validarSinUnitOfWorkActivo();
            ValidacionesSinUnitOfWorkActivo++;
        }
    }
}
