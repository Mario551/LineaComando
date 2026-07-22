using PER.Mensajeria.Datos.Contexto;
using PER.Mensajeria.Datos.UnitOfWork;

namespace AplicacionTest.Infraestructura;

public sealed class UnitOfWorkFactoryPrueba : IUnitOfWorkFactory
{
    private readonly Func<MensajeriaContextoDB> crearContexto;
    private int alcancesActivos;
    private int alcancesCreados;
    private int alcancesDispuestos;

    public UnitOfWorkFactoryPrueba(BaseDatosPrueba baseDatos)
        : this(baseDatos.CrearContexto)
    {
    }

    public UnitOfWorkFactoryPrueba(Func<MensajeriaContextoDB> crearContexto)
    {
        this.crearContexto = crearContexto;
    }

    public int AlcancesActivos => Volatile.Read(ref alcancesActivos);

    public int AlcancesCreados => Volatile.Read(ref alcancesCreados);

    public int AlcancesDispuestos => Volatile.Read(ref alcancesDispuestos);

    public IUnitOfWorkScope Crear()
    {
        MensajeriaContextoDB contexto = crearContexto();
        Interlocked.Increment(ref alcancesActivos);
        Interlocked.Increment(ref alcancesCreados);

        return new UnitOfWorkScopePrueba(
            new UnitOfWork(contexto),
            contexto,
            RegistrarDisposicion);
    }

    private void RegistrarDisposicion()
    {
        Interlocked.Decrement(ref alcancesActivos);
        Interlocked.Increment(ref alcancesDispuestos);
    }

    private sealed class UnitOfWorkScopePrueba : IUnitOfWorkScope
    {
        private readonly MensajeriaContextoDB contexto;
        private readonly Action registrarDisposicion;
        private int dispuesto;

        public UnitOfWorkScopePrueba(
            IUnitOfWork unitOfWork,
            MensajeriaContextoDB contexto,
            Action registrarDisposicion)
        {
            UnitOfWork = unitOfWork;
            this.contexto = contexto;
            this.registrarDisposicion = registrarDisposicion;
        }

        public IUnitOfWork UnitOfWork { get; }

        public async ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref dispuesto, 1) != 0)
            {
                return;
            }

            try
            {
                await contexto.DisposeAsync();
            }
            finally
            {
                registrarDisposicion();
            }
        }
    }
}
