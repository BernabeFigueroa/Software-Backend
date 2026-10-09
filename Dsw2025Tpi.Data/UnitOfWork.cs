using System.Transactions;
using Dsw2025Tpi.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Dsw2025Tpi.Data.Persistence;

public class UnitOfWork : IUnitOfWork
{
    private readonly Dsw2025TpiContext _appDb;
    private readonly AuthenticateContext _authDb;

    public UnitOfWork(Dsw2025TpiContext appDb, AuthenticateContext authDb)
    {
        _appDb = appDb;
        _authDb = authDb;
    }

    public async Task ExecuteAsync(Func<Task> action, CancellationToken ct = default)
    {
        var options = new TransactionOptions
        {
            IsolationLevel = IsolationLevel.ReadCommitted,
            Timeout = TransactionManager.DefaultTimeout
        };

        using var scope = new TransactionScope(
            TransactionScopeOption.Required,
            options,
            TransactionScopeAsyncFlowOption.Enabled);

        await action();  // se ejecuta la operación completa

        // Guardamos cambios en ambos contextos
        await _authDb.SaveChangesAsync(ct);
        await _appDb.SaveChangesAsync(ct);

        scope.Complete();
    }

    public async Task<T> ExecuteInTransactionAsync<T>(Func<Task<T>> action, CancellationToken ct = default)
    {
        await using var transaction = await _appDb.Database.BeginTransactionAsync(ct);

        try
        {
            var result = await action();

            // Único SaveChanges: todas las alteraciones se envían juntas
            await _appDb.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            return result;
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);

            // Descartar los cambios en memoria para no persistirlos en un SaveChanges posterior
            _appDb.ChangeTracker.Clear();
            throw;
        }
    }

    public Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        return _appDb.SaveChangesAsync(ct);
    }
}
