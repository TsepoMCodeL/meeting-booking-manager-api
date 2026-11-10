using Microsoft.EntityFrameworkCore.Storage;

// Saves EF changes and manages the database transaction boundary.
public sealed class EfUnitOfWork(MeetingRoomDbContext db) : IUnitOfWork
{
    public async Task<IUnitOfWorkTransaction> BeginTransactionAsync() =>
        new EfUnitOfWorkTransaction(await db.Database.BeginTransactionAsync());

    public async Task SaveChangesAsync() => await db.SaveChangesAsync();

    // Adapts the EF transaction to the application transaction contract.
    private sealed class EfUnitOfWorkTransaction(IDbContextTransaction transaction) : IUnitOfWorkTransaction
    {
        public Task CommitAsync() => transaction.CommitAsync();

        public ValueTask DisposeAsync() => transaction.DisposeAsync();
    }
}
