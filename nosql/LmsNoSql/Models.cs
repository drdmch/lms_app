using MongoDB.Bson.Serialization.Attributes;

namespace LmsNoSql;


public class Course
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public string Category { get; set; } = "";
    public List<Module> Modules { get; set; } = new();
}

public class Module
{
    public string Title { get; set; } = "";
    public int Position { get; set; }
    public List<Lesson> Lessons { get; set; } = new();
}

public class Lesson
{
    public int LessonId { get; set; }
    public string Title { get; set; } = "";
    public int Position { get; set; }
    public List<string> Tags { get; set; } = new();
    public List<Material> Materials { get; set; } = new();
    public List<Quiz> Quizzes { get; set; } = new();
}


public class Material
{
    public string Type { get; set; } = "";   // video, pdf, link, article
    public string Url { get; set; } = "";

    [BsonIgnoreIfNull]
    public int? DurationSec { get; set; }

    [BsonIgnoreIfNull]
    public string? Body { get; set; }
}

public class Quiz
{
    public string Title { get; set; } = "";
    public int PassScore { get; set; }
    public List<Question> Questions { get; set; } = new();
}

public class Question
{
    public string Text { get; set; } = "";
    public List<AnswerOption> Options { get; set; } = new();
}

public class AnswerOption
{
    public string Text { get; set; } = "";
    public bool IsCorrect { get; set; }
}
