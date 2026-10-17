namespace LmsApp.Models;

public record UserDetails(
    int UserId, string Email, string FullName, string Role,
    DateTime UpdatedAt, int? UpdatedBy);

public record CourseCatalogItem(
    int CourseId, string Title, int CategoryId, string Category,
    string Instructors, long StudentsCount, long ModulesCount);

public record CourseStructureRow(
    int CourseId, string CourseTitle,
    int ModuleId, string ModuleTitle, int ModulePosition,
    int? LessonId, string? LessonTitle, int? LessonPosition);

public record EnrollmentProgress(
    int EnrollmentId, int UserId, string Student, int CourseId, string Course,
    string Status, DateTime EnrolledAt, decimal ProgressPercent, bool HasCertificate);

public record SubmissionDetails(
    int SubmissionId, int AssignmentId, string Assignment, int MaxScore, int? Score,
    int EnrollmentId, string Student, string Course, string AnswerText,
    DateTime UpdatedAt, string? ChangedBy);

public record QuizResult(
    int AttemptId, int QuizId, string Quiz, int EnrollmentId, string Student,
    int Score, int PassScore, bool IsPassed, DateTime TakenAt);

public record QuizAttemptOutcome(int AttemptId, int Score);
