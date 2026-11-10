public interface IUnitOfWork
{
    Task<IUnitOfWorkTransaction> BeginTransactionAsync();

    Task SaveChangesAsync();
}
