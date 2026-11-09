using System.Diagnostics;
using System.Text.Json;
using LmsNoSql;


//   dotnet run -- 1000     -> 1000 курсів 
//   dotnet run -- redis    -> базові команди Redis

var sql = new SqlRepository("Host=localhost;Port=55432;Database=lms_lab2;Username=postgres;Password=postgres");
var mongo = new MongoRepository("mongodb://localhost:57017");

if (args.Length > 0 && args[0] == "redis")
{
    RedisDemo.Run("localhost:56379");
    return;
}

int coursesCount = args.Length > 0 ? int.Parse(args[0]) : 1000;

Console.WriteLine($"Заповнюю бази: {coursesCount} курсів...");
sql.Clear();
mongo.Clear();
DataGenerator.Reset(42);

var timer = Stopwatch.StartNew();
var batch = new List<Course>();
for (int i = 1; i <= coursesCount; i++)
{
    var course = DataGenerator.MakeCourse(i);
    sql.AddCourse(course);  
    batch.Add(course);
    if (batch.Count == 100)
    {
        mongo.AddCourses(batch);
        batch.Clear();
        Console.Write($"\r  {i}/{coursesCount}");
    }
}
if (batch.Count > 0) mongo.AddCourses(batch);
sql.Analyze();
Console.WriteLine($"\nГотово за {timer.Elapsed.TotalSeconds:F0} c");

Console.WriteLine($"\nMongoDB: {mongo.Count()} документів, {mongo.SizeInfo()}");
long totalRows = 0;
Console.WriteLine("PostgreSQL:");
foreach (var (table, rows) in sql.RowCounts())
{
    Console.WriteLine($"  {table,-15} {rows,8}");
    totalRows += rows;
}
Console.WriteLine($"  {"разом",-15} {totalRows,8} рядків, {sql.SizeInfo()}");

var rnd = new Random(1);
int same = 0;
for (int i = 0; i < 20; i++)
{
    int id = rnd.Next(1, coursesCount + 1);
    string a = JsonSerializer.Serialize(mongo.GetCourse(id));
    string b = JsonSerializer.Serialize(sql.GetCourse(id));
    if (a == b) same++;
}
Console.WriteLine($"\nПеревірка: {same} з 20 випадкових курсів однакові в MongoDB і PostgreSQL");

Console.WriteLine("\nТести (середній час одного запиту, мс):");
Console.WriteLine($"{"Тест",-42} {"MongoDB",10} {"PostgreSQL",12}");

// однакові випадкові значення для обох баз
int[] courseIds = Enumerable.Range(0, 500).Select(_ => rnd.Next(1, coursesCount + 1)).ToArray();
string[] tags = Enumerable.Range(0, 300).Select(_ => "tag" + rnd.Next(DataGenerator.TagCount)).ToArray();
var lessonIds = sql.AllLessonIds();
int[] lessonsToRename = Enumerable.Range(0, 500).Select(_ => lessonIds[rnd.Next(lessonIds.Count)]).ToArray();

PrintRow("1. Отримати курс повністю за id",
    Measure(courseIds.Length, i => mongo.GetCourse(courseIds[i])),
    Measure(courseIds.Length, i => sql.GetCourse(courseIds[i])));

PrintRow("2. Пошук курсів за тегом уроку",
    Measure(tags.Length, i => mongo.FindCoursesByTag(tags[i])),
    Measure(tags.Length, i => sql.FindCoursesByTag(tags[i])));

DataGenerator.Reset(777);
var newCourses = Enumerable.Range(1, 100).Select(i => DataGenerator.MakeCourse(100000 + i)).ToList();
double sqlInsert = Measure(newCourses.Count, i => sql.AddCourse(newCourses[i]), warmup: 0);
double mongoInsert = Measure(newCourses.Count, i => mongo.AddCourse(newCourses[i]), warmup: 0);
foreach (var c in newCourses) { sql.DeleteCourse(c.Id); mongo.DeleteCourse(c.Id); }
PrintRow("3. Додати новий курс", mongoInsert, sqlInsert);

PrintRow("4. Змінити назву уроку",
    Measure(lessonsToRename.Length, i => mongo.RenameLesson(lessonsToRename[i], "Нова назва " + i)),
    Measure(lessonsToRename.Length, i => sql.RenameLesson(lessonsToRename[i], "Нова назва " + i)));

PrintRow("5. Статистика відео по категоріях",
    Measure(10, i => mongo.VideoStats(), warmup: 1),
    Measure(10, i => sql.VideoStats(), warmup: 1));

Console.WriteLine("\nРезультат тесту 5 (однаковий в обох базах):");
var mongoStats = mongo.VideoStats();
foreach (var (category, count, totalSec) in sql.VideoStats())
{
    var m = mongoStats.First(d => d["_id"].AsString == category);
    Console.WriteLine($"  {category,-15} відео: {count,6} / {m["count"],6}   хвилин: {totalSec / 60,7} / {m["totalSec"].ToInt64() / 60,7}");
}


static double Measure(int count, Action<int> action, int warmup = 5)
{
    for (int i = 0; i < warmup; i++) action(i);

    var sw = Stopwatch.StartNew();
    for (int i = 0; i < count; i++) action(i);
    sw.Stop();
    return sw.Elapsed.TotalMilliseconds / count;
}

static void PrintRow(string name, double mongoMs, double sqlMs)
{
    string faster = mongoMs < sqlMs ? $"MongoDB у {sqlMs / mongoMs:F1} р." : $"PostgreSQL у {mongoMs / sqlMs:F1} р.";
}
