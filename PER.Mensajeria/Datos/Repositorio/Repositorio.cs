using PER.Mensajeria.Datos.Contexto;
using Microsoft.EntityFrameworkCore;

namespace PER.Mensajeria.Datos.Repositorio;

public class Repositorio<T> : IRepositorio<T> where T : class
{
    protected readonly MensajeriaContextoDB Contexto;
    protected readonly DbSet<T> DbSet;

    public Repositorio(MensajeriaContextoDB contexto)
    {
        Contexto = contexto;
        DbSet = Contexto.Set<T>();
    }

    public IQueryable<T> Get()
    {
        return DbSet;
    }

    public IQueryable<T> GetNoTracking()
    {
        return DbSet.AsNoTracking();
    }

    public async Task<T> AgregarAsync(T entidad, CancellationToken cancellationToken)
    {
        await DbSet.AddAsync(entidad, cancellationToken);
        return entidad;
    }

    public void Actualizar(T entidad)
    {
        DbSet.Update(entidad);
    }
}
