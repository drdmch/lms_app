using LmsApp.Models;
using Npgsql;

namespace LmsApp.Repositories;
public sealed class UserRepository(NpgsqlConnection conn, NpgsqlTransaction tx)
    : RepositoryBase(conn, tx), IUserRepository
{
    private const string Select =
        "SELECT user_id, email, full_name, role, updated_at, updated_by FROM v_user_details";

    private static UserDetails Map(NpgsqlDataReader r) =>
        new(r.GetInt32(0), r.GetString(1), r.GetString(2), r.GetString(3), r.GetDateTime(4), NInt(r, 5));

    public Task<int> CreateAsync(string role, string email, string passwordHash, string fullName, int? actorId) =>
        CallForIdAsync("CALL sp_create_user(@role, @email, @hash, @name, @actor, NULL)",
            P("role", role), P("email", email), P("hash", passwordHash),
            P("name", fullName), P("actor", actorId));

    public Task UpdateAsync(int id, string fullName, string role, int? actorId) =>
        CallAsync("CALL sp_update_user(@id, @name, @role, @actor)",
            P("id", id), P("name", fullName), P("role", role), P("actor", actorId));

    public Task SoftDeleteAsync(int id, int? actorId) =>
        CallAsync("CALL sp_soft_delete_user(@id, @actor)", P("id", id), P("actor", actorId));

    public Task RestoreAsync(int id, int? actorId) =>
        CallAsync("CALL sp_restore_user(@id, @actor)", P("id", id), P("actor", actorId));

    public Task<UserDetails?> GetByIdAsync(int id) =>
        QuerySingleAsync($"{Select} WHERE user_id = @id", Map, P("id", id));

    public Task<IReadOnlyList<UserDetails>> GetAllAsync() =>
        QueryAsync($"{Select} ORDER BY user_id", Map);
}

public sealed class CourseRepository(NpgsqlConnection conn, NpgsqlTransaction tx)
    : RepositoryBase(conn, tx), ICourseRepository
{
    private const string CatalogCols =
        "course_id, title, category_id, category, instructors, students_count, modules_count";

    private static CourseCatalogItem MapCatalog(NpgsqlDataReader r) =>
        new(r.GetInt32(0), r.GetString(1), r.GetInt32(2), r.GetString(3),
            r.GetString(4), r.GetInt64(5), r.GetInt64(6));

    private static CourseStructureRow MapStructure(NpgsqlDataReader r) =>
        new(r.GetInt32(0), r.GetString(1), r.GetInt32(2), r.GetString(3), r.GetInt32(4),
            NInt(r, 5), NStr(r, 6), NInt(r, 7));

    public Task<int> CreateCategoryAsync(string name) =>
        CallForIdAsync("CALL sp_create_category(@name, NULL)", P("name", name));

    public Task<int> CreateAsync(int categoryId, string title, int? actorId) =>
        CallForIdAsync("CALL sp_create_course(@cat, @title, @actor, NULL)",
            P("cat", categoryId), P("title", title), P("actor", actorId));

    public Task UpdateAsync(int id, string title, int categoryId, int? actorId) =>
        CallAsync("CALL sp_update_course(@id, @title, @cat, @actor)",
            P("id", id), P("title", title), P("cat", categoryId), P("actor", actorId));

    public Task SoftDeleteAsync(int id, int? actorId) =>
        CallAsync("CALL sp_soft_delete_course(@id, @actor)", P("id", id), P("actor", actorId));

    public Task RestoreAsync(int id, int? actorId) =>
        CallAsync("CALL sp_restore_course(@id, @actor)", P("id", id), P("actor", actorId));

    public Task AssignInstructorAsync(int courseId, int instructorId, int? actorId) =>
        CallAsync("CALL sp_assign_instructor(@course, @user, @actor)",
            P("course", courseId), P("user", instructorId), P("actor", actorId));

    public Task<int> CreateModuleAsync(int courseId, string title, int? actorId) =>
        CallForIdAsync("CALL sp_create_module(@course, @title, @actor, NULL)",
            P("course", courseId), P("title", title), P("actor", actorId));

    public Task SoftDeleteModuleAsync(int moduleId, int? actorId) =>
        CallAsync("CALL sp_soft_delete_module(@id, @actor)", P("id", moduleId), P("actor", actorId));

    public Task<int> CreateLessonAsync(int moduleId, string title, int? actorId) =>
        CallForIdAsync("CALL sp_create_lesson(@module, @title, @actor, NULL)",
            P("module", moduleId), P("title", title), P("actor", actorId));

    public Task SoftDeleteLessonAsync(int lessonId, int? actorId) =>
        CallAsync("CALL sp_soft_delete_lesson(@id, @actor)", P("id", lessonId), P("actor", actorId));

    public Task<CourseCatalogItem?> GetByIdAsync(int id) =>
        QuerySingleAsync($"SELECT {CatalogCols} FROM v_course_catalog WHERE course_id = @id",
            MapCatalog, P("id", id));

    public Task<IReadOnlyList<CourseCatalogItem>> GetCatalogAsync() =>
        QueryAsync($"SELECT {CatalogCols} FROM v_course_catalog ORDER BY title", MapCatalog);

    public Task<IReadOnlyList<CourseCatalogItem>> SearchAsync(string text) =>
        QueryAsync($"SELECT {CatalogCols} FROM fn_search_courses(@q) ORDER BY title",
            MapCatalog, P("q", text));

    public Task<IReadOnlyList<CourseStructureRow>> GetStructureAsync(int courseId) =>
        QueryAsync(
            @"SELECT course_id, course_title, module_id, module_title, module_position,
                     lesson_id, lesson_title, lesson_position
              FROM v_course_structure WHERE course_id = @id
              ORDER BY module_position, lesson_position",
            MapStructure, P("id", courseId));
}

public sealed class EnrollmentRepository(NpgsqlConnection conn, NpgsqlTransaction tx)
    : RepositoryBase(conn, tx), IEnrollmentRepository
{
    private static EnrollmentProgress Map(NpgsqlDataReader r) =>
        new(r.GetInt32(0), r.GetInt32(1), r.GetString(2), r.GetInt32(3), r.GetString(4),
            r.GetString(5), r.GetDateTime(6), r.GetDecimal(7), r.GetBoolean(8));

    public Task<int> EnrollAsync(int studentId, int courseId) =>
        CallForIdAsync("CALL sp_enroll_student(@user, @course, NULL)",
            P("user", studentId), P("course", courseId));

    public Task CompleteAsync(int enrollmentId) =>
        CallAsync("CALL sp_complete_enrollment(@id)", P("id", enrollmentId));

    public Task<IReadOnlyList<EnrollmentProgress>> GetByStudentAsync(int studentId) =>
        QueryAsync(
            @"SELECT enrollment_id, user_id, student, course_id, course, status,
                     enrolled_at, progress_percent, has_certificate
              FROM v_enrollment_progress WHERE user_id = @id ORDER BY enrollment_id",
            Map, P("id", studentId));
}

public sealed class SubmissionRepository(NpgsqlConnection conn, NpgsqlTransaction tx)
    : RepositoryBase(conn, tx), ISubmissionRepository
{
    private const string Select =
        @"SELECT submission_id, assignment_id, assignment, max_score, score, enrollment_id,
                 student, course, answer_text, updated_at, changed_by
          FROM v_submission_details";

    private static SubmissionDetails Map(NpgsqlDataReader r) =>
        new(r.GetInt32(0), r.GetInt32(1), r.GetString(2), r.GetInt32(3), NInt(r, 4), r.GetInt32(5),
            r.GetString(6), r.GetString(7), r.GetString(8), r.GetDateTime(9), NStr(r, 10));

    public Task<int> CreateAssignmentAsync(int lessonId, string title, int maxScore, int? actorId) =>
        CallForIdAsync("CALL sp_create_assignment(@lesson, @title, @max, @actor, NULL)",
            P("lesson", lessonId), P("title", title), P("max", maxScore), P("actor", actorId));

    public Task<int> SubmitAsync(int assignmentId, int enrollmentId, string answerText, int? actorId) =>
        CallForIdAsync("CALL sp_submit_assignment(@assignment, @enrollment, @answer, @actor, NULL)",
            P("assignment", assignmentId), P("enrollment", enrollmentId),
            P("answer", answerText), P("actor", actorId));

    public Task GradeAsync(int submissionId, int score, int teacherId) =>
        CallAsync("CALL sp_grade_submission(@id, @score, @teacher)",
            P("id", submissionId), P("score", score), P("teacher", teacherId));

    public Task<IReadOnlyList<SubmissionDetails>> GetByAssignmentAsync(int assignmentId) =>
        QueryAsync($"{Select} WHERE assignment_id = @id ORDER BY submission_id", Map, P("id", assignmentId));

    public Task<IReadOnlyList<SubmissionDetails>> GetByEnrollmentAsync(int enrollmentId) =>
        QueryAsync($"{Select} WHERE enrollment_id = @id ORDER BY submission_id", Map, P("id", enrollmentId));
}

public sealed class QuizRepository(NpgsqlConnection conn, NpgsqlTransaction tx)
    : RepositoryBase(conn, tx), IQuizRepository
{
    private static QuizResult Map(NpgsqlDataReader r) =>
        new(r.GetInt32(0), r.GetInt32(1), r.GetString(2), r.GetInt32(3), r.GetString(4),
            r.GetInt32(5), r.GetInt32(6), r.GetBoolean(7), r.GetDateTime(8));

    public Task<int> CreateQuizAsync(int lessonId, string title, int passScore) =>
        CallForIdAsync("CALL sp_create_quiz(@lesson, @title, @pass, NULL)",
            P("lesson", lessonId), P("title", title), P("pass", passScore));

    public Task<int> AddQuestionAsync(int quizId, string text) =>
        CallForIdAsync("CALL sp_add_question(@quiz, @text, NULL)", P("quiz", quizId), P("text", text));

    public Task<int> AddOptionAsync(int questionId, string text, bool isCorrect) =>
        CallForIdAsync("CALL sp_add_answer_option(@question, @text, @correct, NULL)",
            P("question", questionId), P("text", text), P("correct", isCorrect));

    public Task<QuizAttemptOutcome> SubmitAttemptAsync(int quizId, int enrollmentId, int[] selectedOptionIds) =>
        CallForRowAsync("CALL sp_submit_quiz_attempt(@quiz, @enrollment, @options, NULL, NULL)",
            r => new QuizAttemptOutcome(r.GetInt32(0), r.GetInt32(1)),
            P("quiz", quizId), P("enrollment", enrollmentId), P("options", selectedOptionIds));

    public Task<IReadOnlyList<QuizResult>> GetResultsAsync(int enrollmentId) =>
        QueryAsync(
            @"SELECT attempt_id, quiz_id, quiz, enrollment_id, student, score, pass_score, is_passed, taken_at
              FROM v_quiz_results WHERE enrollment_id = @id ORDER BY attempt_id",
            Map, P("id", enrollmentId));
}
