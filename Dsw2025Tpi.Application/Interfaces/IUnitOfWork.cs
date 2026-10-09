namespace Dsw2025Tpi.Application.Interfaces;

public interface IUnitOfWork
{
    // Ejecuta una operación que involucra Identity (AuthenticateContext) y el contexto de la aplicación
    Task ExecuteAsync(Func<Task> action, CancellationToken ct = default);

    // Ejecuta la operación dentro de una transacción del contexto de la aplicación.
    // Los cambios registrados por los repositorios se envían con un único SaveChanges y luego se hace Commit.
    // Si cualquier paso falla, se hace Rollback y se descartan los cambios pendientes.
    Task<T> ExecuteInTransactionAsync<T>(Func<Task<T>> action, CancellationToken ct = default);

    // Persiste los cambios registrados por los repositorios
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
