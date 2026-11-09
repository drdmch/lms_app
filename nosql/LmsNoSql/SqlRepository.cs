using Npgsql;

namespace LmsNoSql;

public class SqlRepository
{
    private readonly string connectionString;

    public SqlRepository(string connectionString)
    {
        this.connectionString = connectionString;
    }

    private NpgsqlConnection Open()
    {
        var conn = new NpgsqlConnection(connectionString);
        conn.Open();
        return conn;
    }

    public void Clear()
    {
        using var conn = Open();
        using var cmd = new NpgsqlCommand("TRUNCATE courses RESTART IDENTITY CASCADE", conn);
        cmd.ExecuteNonQuery();
        // лічильники id інших таблиць теж скидаємо
        foreach (var t in new[] { "modules", "lessons", "materials", "quizzes", "questions", "answer_options" })
        {
            using var reset = new NpgsqlCommand($"ALTER SEQUENCE {t}_id_seq RESTART WITH 1", conn);
            reset.ExecuteNonQuery();
        }
    }


    public void AddCourse(Course course)
    {
        using var conn = Open();
        using var tx = conn.BeginTransaction();

        int categoryId = Array.IndexOf(DataGenerator.Categories, course.Category) + 1;
        course.Id = InsertReturningId(conn, tx,
            "INSERT INTO courses(category_id, title) VALUES (@p0, @p1) RETURNING id", categoryId, course.Title);

        foreach (var module in course.Modules)
        {
            int moduleId = InsertReturningId(conn, tx,
                "INSERT INTO modules(course_id, title, position) VALUES (@p0, @p1, @p2) RETURNING id",
                course.Id, module.Title, module.Position);

            foreach (var lesson in module.Lessons)
            {
                lesson.LessonId = InsertReturningId(conn, tx,
                    "INSERT INTO lessons(module_id, title, position) VALUES (@p0, @p1, @p2) RETURNING id",
                    moduleId, lesson.Title, lesson.Position);

                foreach (var tag in lesson.Tags)
                    Execute(conn, tx, "INSERT INTO lesson_tags(lesson_id, tag) VALUES (@p0, @p1)", lesson.LessonId, tag);

                foreach (var m in lesson.Materials)
                    Execute(conn, tx,
                        "INSERT INTO materials(lesson_id, type, url, duration_sec, body) VALUES (@p0, @p1, @p2, @p3, @p4)",
                        lesson.LessonId, m.Type, m.Url, m.DurationSec, m.Body);

                foreach (var quiz in lesson.Quizzes)
                {
                    int quizId = InsertReturningId(conn, tx,
                        "INSERT INTO quizzes(lesson_id, title, pass_score) VALUES (@p0, @p1, @p2) RETURNING id",
                        lesson.LessonId, quiz.Title, quiz.PassScore);

                    foreach (var q in quiz.Questions)
                    {
                        int questionId = InsertReturningId(conn, tx,
                            "INSERT INTO questions(quiz_id, text) VALUES (@p0, @p1) RETURNING id", quizId, q.Text);

                        foreach (var o in q.Options)
                            Execute(conn, tx,
                                "INSERT INTO answer_options(question_id, text, is_correct) VALUES (@p0, @p1, @p2)",
                                questionId, o.Text, o.IsCorrect);
                    }
                }
            }
        }
        tx.Commit();
    }

    public Course? GetCourse(int id)
    {
        using var conn = Open();
        Course? course = null;

        using (var cmd = Command(conn,
            "SELECT c.id, c.title, cat.name FROM courses c JOIN categories cat ON cat.id = c.category_id WHERE c.id = @p0", id))
        using (var r = cmd.ExecuteReader())
        {
            if (r.Read())
                course = new Course { Id = r.GetInt32(0), Title = r.GetString(1), Category = r.GetString(2) };
        }
        if (course == null) return null;

        // модулі і уроки
        var lessons = new Dictionary<int, Lesson>();
        using (var cmd = Command(conn, @"
            SELECT m.id, m.title, m.position, l.id, l.title, l.position
            FROM modules m JOIN lessons l ON l.module_id = m.id
            WHERE m.course_id = @p0
            ORDER BY m.position, l.position", id))
        using (var r = cmd.ExecuteReader())
        {
            int lastModuleId = -1;
            Module? module = null;
            while (r.Read())
            {
                if (r.GetInt32(0) != lastModuleId)
                {
                    lastModuleId = r.GetInt32(0);
                    module = new Module { Title = r.GetString(1), Position = r.GetInt32(2) };
                    course.Modules.Add(module);
                }
                var lesson = new Lesson { LessonId = r.GetInt32(3), Title = r.GetString(4), Position = r.GetInt32(5) };
                module!.Lessons.Add(lesson);
                lessons[lesson.LessonId] = lesson;
            }
        }

        using (var cmd = Command(conn, @"
            SELECT lt.lesson_id, lt.tag
            FROM lesson_tags lt JOIN lessons l ON l.id = lt.lesson_id JOIN modules m ON m.id = l.module_id
            WHERE m.course_id = @p0
            ORDER BY lt.lesson_id, lt.tag COLLATE ""C""", id))
        using (var r = cmd.ExecuteReader())
        {
            while (r.Read())
                lessons[r.GetInt32(0)].Tags.Add(r.GetString(1));
        }

        using (var cmd = Command(conn, @"
            SELECT mt.lesson_id, mt.type, mt.url, mt.duration_sec, mt.body
            FROM materials mt JOIN lessons l ON l.id = mt.lesson_id JOIN modules m ON m.id = l.module_id
            WHERE m.course_id = @p0
            ORDER BY mt.id", id))
        using (var r = cmd.ExecuteReader())
        {
            while (r.Read())
            {
                lessons[r.GetInt32(0)].Materials.Add(new Material
                {
                    Type = r.GetString(1),
                    Url = r.GetString(2),
                    DurationSec = r.IsDBNull(3) ? null : r.GetInt32(3),
                    Body = r.IsDBNull(4) ? null : r.GetString(4)
                });
            }
        }

        using (var cmd = Command(conn, @"
            SELECT q.lesson_id, q.id, q.title, q.pass_score, qq.id, qq.text, o.text, o.is_correct
            FROM quizzes q
            JOIN questions qq ON qq.quiz_id = q.id
            JOIN answer_options o ON o.question_id = qq.id
            JOIN lessons l ON l.id = q.lesson_id
            JOIN modules m ON m.id = l.module_id
            WHERE m.course_id = @p0
            ORDER BY q.id, qq.id, o.id", id))
        using (var r = cmd.ExecuteReader())
        {
            int lastQuizId = -1, lastQuestionId = -1;
            Quiz? quiz = null;
            Question? question = null;
            while (r.Read())
            {
                if (r.GetInt32(1) != lastQuizId)
                {
                    lastQuizId = r.GetInt32(1);
                    quiz = new Quiz { Title = r.GetString(2), PassScore = r.GetInt32(3) };
                    lessons[r.GetInt32(0)].Quizzes.Add(quiz);
                }
                if (r.GetInt32(4) != lastQuestionId)
                {
                    lastQuestionId = r.GetInt32(4);
                    question = new Question { Text = r.GetString(5) };
                    quiz!.Questions.Add(question);
                }
                question!.Options.Add(new AnswerOption { Text = r.GetString(6), IsCorrect = r.GetBoolean(7) });
            }
        }

        return course;
    }

    public List<int> FindCoursesByTag(string tag)
    {
        using var conn = Open();
        using var cmd = Command(conn, @"
            SELECT DISTINCT m.course_id
            FROM lesson_tags lt
            JOIN lessons l ON l.id = lt.lesson_id
            JOIN modules m ON m.id = l.module_id
            WHERE lt.tag = @p0", tag);
        var result = new List<int>();
        using var r = cmd.ExecuteReader();
        while (r.Read()) result.Add(r.GetInt32(0));
        return result;
    }

    public void RenameLesson(int lessonId, string newTitle)
    {
        using var conn = Open();
        using var cmd = Command(conn, "UPDATE lessons SET title = @p0 WHERE id = @p1", newTitle, lessonId);
        cmd.ExecuteNonQuery();
    }

    public List<(string category, long count, long totalSec)> VideoStats()
    {
        using var conn = Open();
        using var cmd = Command(conn, @"
            SELECT cat.name, COUNT(*), SUM(mt.duration_sec)
            FROM materials mt
            JOIN lessons l ON l.id = mt.lesson_id
            JOIN modules m ON m.id = l.module_id
            JOIN courses c ON c.id = m.course_id
            JOIN categories cat ON cat.id = c.category_id
            WHERE mt.type = 'video'
            GROUP BY cat.name
            ORDER BY cat.name");
        var result = new List<(string, long, long)>();
        using var r = cmd.ExecuteReader();
        while (r.Read()) result.Add((r.GetString(0), r.GetInt64(1), r.GetInt64(2)));
        return result;
    }

    public void DeleteCourse(int id)
    {
        using var conn = Open();
        using var cmd = Command(conn, "DELETE FROM courses WHERE id = @p0", id);
        cmd.ExecuteNonQuery();
    }

    public List<int> AllLessonIds()
    {
        using var conn = Open();
        using var cmd = Command(conn, "SELECT id FROM lessons ORDER BY id");
        var result = new List<int>();
        using var r = cmd.ExecuteReader();
        while (r.Read()) result.Add(r.GetInt32(0));
        return result;
    }

    public List<(string table, long rows)> RowCounts()
    {
        var tables = new[] { "courses", "modules", "lessons", "lesson_tags", "materials", "quizzes", "questions", "answer_options" };
        using var conn = Open();
        var result = new List<(string, long)>();
        foreach (var t in tables)
        {
            using var cmd = Command(conn, $"SELECT COUNT(*) FROM {t}");
            result.Add((t, (long)cmd.ExecuteScalar()!));
        }
        return result;
    }

    public string SizeInfo()
    {
        using var conn = Open();
        using var cmd = Command(conn, "SELECT pg_database_size(current_database()) / 1024.0 / 1024.0");
        return $"розмір бази {Convert.ToDouble(cmd.ExecuteScalar()):F1} MB";
    }

    public void Analyze()
    {
        using var conn = Open();
        using var cmd = Command(conn, "ANALYZE");
        cmd.ExecuteNonQuery();
    }

    // ---- допоміжні методи ----
    private static NpgsqlCommand Command(NpgsqlConnection conn, string sql, params object?[] args)
    {
        var cmd = new NpgsqlCommand(sql, conn);
        for (int i = 0; i < args.Length; i++)
            cmd.Parameters.AddWithValue("p" + i, args[i] ?? DBNull.Value);
        return cmd;
    }

    private static int InsertReturningId(NpgsqlConnection conn, NpgsqlTransaction tx, string sql, params object?[] args)
    {
        using var cmd = Command(conn, sql, args);
        cmd.Transaction = tx;
        return (int)cmd.ExecuteScalar()!;
    }

    private static void Execute(NpgsqlConnection conn, NpgsqlTransaction tx, string sql, params object?[] args)
    {
        using var cmd = Command(conn, sql, args);
        cmd.Transaction = tx;
        cmd.ExecuteNonQuery();
    }
}
