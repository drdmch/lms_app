using LmsApp.Repositories;
using Npgsql;

namespace LmsApp;

public interface IUnitOfWork : IAsyncDisposable
{
    IUserRepository        Users        { get; }
    ICourseRepository      Courses      { get; }
    IEnrollmentRepository  Enrollments  { get; }
    ISubmissionRepository  Submissions  { get; }
    IQuizRepository        Quizzes      { get; }

    Task CommitAsync();
    Task RollbackAsync();
}

public interface IUnitOfWorkFactory
{
    Task<IUnitOfWork> BeginAsync();
}

public sealed class UnitOfWorkFactory(string connectionString) : IUnitOfWorkFactory
{
    public async Task<IUnitOfWork> BeginAsync()
    {
        var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync();
        var tx = await conn.BeginTransactionAsync();
        return new UnitOfWork(conn, tx);
    }
}

public sealed class UnitOfWork : IUnitOfWork
{
    private readonly NpgsqlConnection _conn;
    private readonly NpgsqlTransaction _tx;
    private bool _completed;

    // репозиторії створюються ліниво й ділять _conn та _tx
    private IUserRepository?       _users;
    private ICourseRepository?     _courses;
    private IEnrollmentRepository? _enrollments;
    private ISubmissionRepository? _submissions;
    private IQuizRepository?       _quizzes;

    internal UnitOfWork(NpgsqlConnection conn, NpgsqlTransaction tx)
    {
        _conn = conn;
        _tx = tx;
    }

    public IUserRepository       Users       => _users       ??= new UserRepository(_conn, _tx);
    public ICourseRepository     Courses     => _courses     ??= new CourseRepository(_conn, _tx);
    public IEnrollmentRepository Enrollments => _enrollments ??= new EnrollmentRepository(_conn, _tx);
    public ISubmissionRepository Submissions => _submissions ??= new SubmissionRepository(_conn, _tx);
    public IQuizRepository       Quizzes     => _quizzes     ??= new QuizRepository(_conn, _tx);

    public async Task CommitAsync()
    {
        EnsureActive();
        await _tx.CommitAsync();
        _completed = true;
    }

    public async Task RollbackAsync()
    {
        EnsureActive();
        await _tx.RollbackAsync();
        _completed = true;
    }

    public async ValueTask DisposeAsync()
    {
        if (!_completed)
            await _tx.RollbackAsync();      // безпечний відкат, якщо Commit не викликали
        await _tx.DisposeAsync();
        await _conn.DisposeAsync();
    }

    private void EnsureActive()
    {
        if (_completed)
            throw new InvalidOperationException("Транзакція вже завершена.");
    }
}
