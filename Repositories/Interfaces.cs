using LmsApp.Models;

namespace LmsApp.Repositories;


public interface IUserRepository
{
    Task<int> CreateAsync(string role, string email, string passwordHash, string fullName, int? actorId);
    Task UpdateAsync(int id, string fullName, string role, int? actorId);
    Task SoftDeleteAsync(int id, int? actorId);
    Task RestoreAsync(int id, int? actorId);
    Task<UserDetails?> GetByIdAsync(int id);
    Task<IReadOnlyList<UserDetails>> GetAllAsync();
}
public interface ICourseRepository
{
    Task<int> CreateCategoryAsync(string name);

    Task<int> CreateAsync(int categoryId, string title, int? actorId);
    Task UpdateAsync(int id, string title, int categoryId, int? actorId);
    Task SoftDeleteAsync(int id, int? actorId);   // каскадно: модулі + уроки
    Task RestoreAsync(int id, int? actorId);
    Task AssignInstructorAsync(int courseId, int instructorId, int? actorId);

    Task<int> CreateModuleAsync(int courseId, string title, int? actorId);
    Task SoftDeleteModuleAsync(int moduleId, int? actorId);

    Task<int> CreateLessonAsync(int moduleId, string title, int? actorId);
    Task SoftDeleteLessonAsync(int lessonId, int? actorId);

    Task<CourseCatalogItem?> GetByIdAsync(int id);
    Task<IReadOnlyList<CourseCatalogItem>> GetCatalogAsync();
    Task<IReadOnlyList<CourseCatalogItem>> SearchAsync(string text);
    Task<IReadOnlyList<CourseStructureRow>> GetStructureAsync(int courseId);
}

public interface IEnrollmentRepository
{
    Task<int> EnrollAsync(int studentId, int courseId);
    Task CompleteAsync(int enrollmentId);  
    Task<IReadOnlyList<EnrollmentProgress>> GetByStudentAsync(int studentId);
}

public interface ISubmissionRepository
{
    Task<int> CreateAssignmentAsync(int lessonId, string title, int maxScore, int? actorId);
    Task<int> SubmitAsync(int assignmentId, int enrollmentId, string answerText, int? actorId);
    Task GradeAsync(int submissionId, int score, int teacherId);
    Task<IReadOnlyList<SubmissionDetails>> GetByAssignmentAsync(int assignmentId);
    Task<IReadOnlyList<SubmissionDetails>> GetByEnrollmentAsync(int enrollmentId);
}

public interface IQuizRepository
{
    Task<int> CreateQuizAsync(int lessonId, string title, int passScore);
    Task<int> AddQuestionAsync(int quizId, string text);
    Task<int> AddOptionAsync(int questionId, string text, bool isCorrect);
    Task<QuizAttemptOutcome> SubmitAttemptAsync(int quizId, int enrollmentId, int[] selectedOptionIds);
    Task<IReadOnlyList<QuizResult>> GetResultsAsync(int enrollmentId);
}
