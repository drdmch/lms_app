using LmsApp;

var cs = Environment.GetEnvironmentVariable("LMS_CONNECTION")
         ?? "Host=localhost;Port=5432;Database=lms_db;Username=dariademchik;Password=postgres;Search Path=lms";

IUnitOfWorkFactory factory = new UnitOfWorkFactory(cs);
var sfx = Guid.NewGuid().ToString("N")[..8];  
int adminId, instructorId, studentId, courseId, assignmentId, quizId;
int[] correctOptions;

Console.WriteLine("== 1. Створення курсу ==");
await using (var uow = await factory.BeginAsync())
{
    adminId      = await uow.Users.CreateAsync("admin",      $"admin_{sfx}@lms.test",   "hash", "Адміністратор", null);
    instructorId = await uow.Users.CreateAsync("instructor", $"teacher_{sfx}@lms.test", "hash", "Іван Викладач", adminId);
    studentId    = await uow.Users.CreateAsync("student",    $"student_{sfx}@lms.test", "hash", "Олена Студентка", adminId);

    var categoryId = await uow.Courses.CreateCategoryAsync($"Програмування {sfx}");
    courseId       = await uow.Courses.CreateAsync(categoryId, $"PostgreSQL для початківців {sfx}", adminId);
    await uow.Courses.AssignInstructorAsync(courseId, instructorId, adminId);

    var moduleId = await uow.Courses.CreateModuleAsync(courseId, "Основи SQL", instructorId);
    var lessonId = await uow.Courses.CreateLessonAsync(moduleId, "Команда SELECT", instructorId);

    assignmentId = await uow.Submissions.CreateAssignmentAsync(lessonId, "Напишіть запит", 100, instructorId);

    quizId = await uow.Quizzes.CreateQuizAsync(lessonId, "Тест з SQL", 50);
    var q1 = await uow.Quizzes.AddQuestionAsync(quizId, "Яка команда читає дані?");
    var o1 = await uow.Quizzes.AddOptionAsync(q1, "SELECT", true);
    await uow.Quizzes.AddOptionAsync(q1, "DROP", false);
    var q2 = await uow.Quizzes.AddQuestionAsync(quizId, "Що об'єднує таблиці?");
    var o3 = await uow.Quizzes.AddOptionAsync(q2, "JOIN", true);
    await uow.Quizzes.AddOptionAsync(q2, "EXPLAIN", false);
    correctOptions = [o1, o3];

    await uow.CommitAsync();
}

Console.WriteLine("Структура курсу:");
await using (var uow = await factory.BeginAsync())
{
    foreach (var row in await uow.Courses.GetStructureAsync(courseId))
        Console.WriteLine($"  {row.ModulePosition}. {row.ModuleTitle} -> {row.LessonPosition}. {row.LessonTitle}");
    Console.WriteLine("Пошук 'postgre':");
    foreach (var c in await uow.Courses.SearchAsync("postgre"))
        Console.WriteLine($"  {c.Title} | викладачі: {c.Instructors} | студентів: {c.StudentsCount}");
}


Console.WriteLine("\n== 2. Навчання студента ==");
await using (var uow = await factory.BeginAsync())
{
    var enrollmentId = await uow.Enrollments.EnrollAsync(studentId, courseId);

    var submissionId = await uow.Submissions.SubmitAsync(assignmentId, enrollmentId, "SELECT * FROM courses;", studentId);
    await uow.Submissions.GradeAsync(submissionId, 90, instructorId);

    var outcome = await uow.Quizzes.SubmitAttemptAsync(quizId, enrollmentId, correctOptions);
    Console.WriteLine($"Тест: спроба {outcome.AttemptId}, бал {outcome.Score}");

    var before = (await uow.Enrollments.GetByStudentAsync(studentId))[0];
    Console.WriteLine($"Прогрес до завершення: {before.ProgressPercent}% (сертифікат: {before.HasCertificate})");

    await uow.Enrollments.CompleteAsync(enrollmentId);

    var after = (await uow.Enrollments.GetByStudentAsync(studentId))[0];
    Console.WriteLine($"Статус: {after.Status}, сертифікат: {after.HasCertificate}");

    foreach (var s in await uow.Submissions.GetByEnrollmentAsync(enrollmentId))
        Console.WriteLine($"Робота: {s.Assignment}, оцінка {s.Score}/{s.MaxScore}, змінив: {s.ChangedBy}");

    await uow.CommitAsync();
}


Console.WriteLine("\n== 3. Відкат транзакції ==");
try
{
    await using var uow = await factory.BeginAsync();
    await uow.Courses.SoftDeleteAsync(courseId, adminId);     
    Console.WriteLine("Курс видалено в межах транзакції, імітуємо збій...");
    throw new InvalidOperationException("Імітація помилки");
}
catch (InvalidOperationException ex)
{
    Console.WriteLine($"Помилка: {ex.Message}");
}

await using (var uow = await factory.BeginAsync())
{
    var course = await uow.Courses.GetByIdAsync(courseId);
    Console.WriteLine($"Після відкату курс {(course is null ? "ВІДСУТНІЙ" : "на місці")}");
}

Console.WriteLine("\n== 4. Soft delete та відновлення ==");
await using (var uow = await factory.BeginAsync())
{
    await uow.Courses.SoftDeleteAsync(courseId, adminId);
    await uow.CommitAsync();
}
await using (var uow = await factory.BeginAsync())
{
    Console.WriteLine($"Після видалення в каталозі: {(await uow.Courses.GetByIdAsync(courseId) is null ? "немає" : "є")}");
    await uow.Courses.RestoreAsync(courseId, adminId);
    await uow.CommitAsync();
}
await using (var uow = await factory.BeginAsync())
{
    Console.WriteLine($"Після відновлення в каталозі: {(await uow.Courses.GetByIdAsync(courseId) is null ? "немає" : "є")}");
    Console.WriteLine($"Рядків у структурі курсу: {(await uow.Courses.GetStructureAsync(courseId)).Count}");
}

Console.WriteLine("\n== 5. Перевірка правил БД ==");
try
{
    await using var uow = await factory.BeginAsync();
    await uow.Enrollments.EnrollAsync(instructorId, courseId);   
    await uow.CommitAsync();
}
catch (Npgsql.PostgresException ex)
{
    Console.WriteLine($"БД відхилила операцію: {ex.MessageText}");
}
