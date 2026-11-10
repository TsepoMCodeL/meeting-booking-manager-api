public interface IUnitOfWorkTransaction : IAsyncDisposable
{
    Task CommitAsync();
}
