using StackExchange.Redis;

namespace LmsNoSql;

// Пункт 6
public static class RedisDemo
{
    public static void Run(string connectionString)
    {
        var redis = ConnectionMultiplexer.Connect(connectionString);
        IDatabase db = redis.GetDatabase();

        Console.WriteLine("\n== 1. STRING: сесія користувача з часом життя (SET / GET / TTL) ==");
        db.StringSet("session:abc123", "user:17", TimeSpan.FromMinutes(30));
        Console.WriteLine($"GET session:abc123  -> {db.StringGet("session:abc123")}");
        Console.WriteLine($"TTL session:abc123  -> {db.KeyTimeToLive("session:abc123")?.TotalSeconds} c");

        Console.WriteLine("\n== 2. HASH: профіль студента (HSET / HGET / HGETALL) ==");
        db.HashSet("user:17", new HashEntry[]
        {
            new("name", "Олена"), new("role", "student"), new("group", "ТК-31")
        });
        Console.WriteLine($"HGET user:17 name   -> {db.HashGet("user:17", "name")}");
        foreach (var e in db.HashGetAll("user:17"))
            Console.WriteLine($"   {e.Name} = {e.Value}");

        Console.WriteLine("\n== 3. INCR: лічильник переглядів курсу ==");
        db.KeyDelete("course:5:views");
        for (int i = 0; i < 10; i++) db.StringIncrement("course:5:views");
        Console.WriteLine($"GET course:5:views  -> {db.StringGet("course:5:views")}");

        Console.WriteLine("\n== 4. LIST: останні переглянуті курси (LPUSH / LTRIM / LRANGE) ==");
        db.KeyDelete("user:17:recent");
        foreach (var courseId in new[] { 3, 8, 1, 12, 5, 7 })
        {
            db.ListLeftPush("user:17:recent", courseId);
            db.ListTrim("user:17:recent", 0, 4);  
        }
        Console.WriteLine($"LRANGE user:17:recent 0 -1 -> {string.Join(", ", db.ListRange("user:17:recent"))}");

        Console.WriteLine("\n== 5. SET: хто зараз онлайн (SADD / SISMEMBER / SCARD / SINTER) ==");
        db.KeyDelete("online"); db.KeyDelete("course:5:students");
        db.SetAdd("online", new RedisValue[] { "17", "21", "30" });
        db.SetAdd("course:5:students", new RedisValue[] { "17", "22", "30", "41" });
        Console.WriteLine($"SCARD online -> {db.SetLength("online")}");
        Console.WriteLine($"SISMEMBER online 22 -> {db.SetContains("online", "22")}");
        var both = db.SetCombine(SetOperation.Intersect, "online", "course:5:students");
        Console.WriteLine($"SINTER online course:5:students (студенти курсу онлайн) -> {string.Join(", ", both)}");

        Console.WriteLine("\n== 6. SORTED SET: рейтинг за тестом (ZADD / ZREVRANGE / ZRANK) ==");
        db.KeyDelete("quiz:1:rating");
        db.SortedSetAdd("quiz:1:rating", new SortedSetEntry[]
        {
            new("Олена", 95), new("Андрій", 80), new("Марта", 88), new("Тарас", 70)
        });
        foreach (var e in db.SortedSetRangeByRankWithScores("quiz:1:rating", 0, 2, Order.Descending))
            Console.WriteLine($"   {e.Element}: {e.Score}");
        Console.WriteLine($"Місце Марти -> {db.SortedSetRank("quiz:1:rating", "Марта", Order.Descending) + 1}");

        Console.WriteLine("\n== 7. EXPIRE + INCR: не більше 3 спроб тесту за годину ==");
        db.KeyDelete("attempts:17:quiz1");
        for (int i = 1; i <= 4; i++)
        {
            long n = db.StringIncrement("attempts:17:quiz1");
            if (n == 1) db.KeyExpire("attempts:17:quiz1", TimeSpan.FromHours(1));
            Console.WriteLine($"спроба {n}: {(n <= 3 ? "дозволено" : "заборонено")}");
        }

        Console.WriteLine("\n== 8. Кеш курсу: SET з EX, потім DEL коли курс змінили ==");
        db.StringSet("cache:course:5", "{\"title\":\"Курс 5\", ...}", TimeSpan.FromMinutes(10));
        Console.WriteLine($"EXISTS cache:course:5 -> {db.KeyExists("cache:course:5")}");
        db.KeyDelete("cache:course:5");
        Console.WriteLine($"після DEL EXISTS -> {db.KeyExists("cache:course:5")}");
    }
}
