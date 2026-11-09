namespace LmsNoSql;

public static class DataGenerator
{
    public static readonly string[] Categories =
    {
        "Програмування", "Бази даних", "Дизайн", "Математика", "Мови",
        "Маркетинг", "DevOps", "Фізика", "Менеджмент", "Безпека"
    };

    static readonly string[] Words =
    {
        "дані", "запит", "індекс", "транзакція", "модель", "функція", "масив", "граф", "кеш", "схема",
        "алгоритм", "структура", "система", "сервіс", "мережа", "протокол", "ключ", "вузол", "таблиця", "звіт"
    };

    static readonly string[] MaterialTypes = { "video", "video", "article", "article", "pdf", "link" };

    public const int TagCount = 500;   

    static Random rnd = new Random(42);

    public static void Reset(int seed) => rnd = new Random(seed);

    static string Text(int words)
    {
        var list = new List<string>();
        for (int i = 0; i < words; i++) list.Add(Words[rnd.Next(Words.Length)]);
        return string.Join(" ", list);
    }

    public static Course MakeCourse(int number)
    {
        var course = new Course
        {
            Title = $"Курс {number}: {Text(3)}",
            Category = Categories[rnd.Next(Categories.Length)]
        };

        int modulesCount = rnd.Next(3, 7);
        for (int m = 1; m <= modulesCount; m++)
        {
            var module = new Module { Title = $"Модуль {m}. {Text(2)}", Position = m };

            int lessonsCount = rnd.Next(3, 6);
            for (int l = 1; l <= lessonsCount; l++)
            {
                var lesson = new Lesson { Title = $"Урок {l}. {Text(3)}", Position = l };

                int tags = rnd.Next(1, 3);
                for (int t = 0; t < tags; t++)
                {
                    string tag = "tag" + rnd.Next(TagCount);
                    if (!lesson.Tags.Contains(tag)) lesson.Tags.Add(tag);
                }
                lesson.Tags.Sort(StringComparer.Ordinal);

                int materialsCount = rnd.Next(2, 5);
                for (int i = 0; i < materialsCount; i++)
                    lesson.Materials.Add(MakeMaterial());

                if (rnd.Next(100) < 30)   // тест є приблизно в третині уроків
                    lesson.Quizzes.Add(MakeQuiz());

                module.Lessons.Add(lesson);
            }
            course.Modules.Add(module);
        }
        return course;
    }

    static Material MakeMaterial()
    {
        string type = MaterialTypes[rnd.Next(MaterialTypes.Length)];
        var material = new Material { Type = type, Url = $"https://lms.test/files/{rnd.Next(1_000_000)}.{type}" };

        if (type == "video") material.DurationSec = rnd.Next(60, 1800);
        if (type == "article") material.Body = Text(rnd.Next(40, 100));

        return material;
    }

    static Quiz MakeQuiz()
    {
        var quiz = new Quiz { Title = "Тест: " + Text(2), PassScore = rnd.Next(50, 90) };
        int questions = rnd.Next(3, 6);
        for (int q = 0; q < questions; q++)
        {
            var question = new Question { Text = Text(6) + "?" };
            int correct = rnd.Next(4);
            for (int o = 0; o < 4; o++)
                question.Options.Add(new AnswerOption { Text = Text(2), IsCorrect = o == correct });
            quiz.Questions.Add(question);
        }
        return quiz;
    }
}
