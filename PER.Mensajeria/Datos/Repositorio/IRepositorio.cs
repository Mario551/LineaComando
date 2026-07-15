namespace PER.Mensajeria.Datos.Repositorio;

public interface IRepositorio<T> where T : class
{
    IQueryable<T> Get();
    IQueryable<T> GetNoTracking();
    Task<T> AgregarAsync(T entidad, CancellationToken cancellationToken);
    void Actualizar(T entidad);
    void LiberarRastreo(T entidad);
}
