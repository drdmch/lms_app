using MongoDB.Bson;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Driver;

namespace LmsNoSql;

public class MongoRepository
{
    private readonly IMongoDatabase db;
    private IMongoCollection<Course> courses;

    public MongoRepository(string connectionString)
    {
        ConventionRegistry.Register("camelCase", new ConventionPack { new CamelCaseElementNameConvention() }, t => true);

        var client = new MongoClient(connectionString);
        db = client.GetDatabase("lms_lab2");
        courses = db.GetCollection<Course>("courses");
    }

    public void Clear()
    {
        db.DropCollection("courses");
        courses = db.GetCollection<Course>("courses");

        courses.Indexes.CreateOne(new CreateIndexModel<Course>(
            Builders<Course>.IndexKeys.Ascending("modules.lessons.tags")));
        courses.Indexes.CreateOne(new CreateIndexModel<Course>(
            Builders<Course>.IndexKeys.Ascending("modules.lessons.lessonId")));
    }

    public void AddCourse(Course course)
    {
        courses.InsertOne(course);
    }

    public void AddCourses(List<Course> list)
    {
        courses.InsertMany(list);
    }

    public Course? GetCourse(int id)
    {
        return courses.Find(c => c.Id == id).FirstOrDefault();
    }

    public List<int> FindCoursesByTag(string tag)
    {
        var filter = Builders<Course>.Filter.Eq("modules.lessons.tags", tag);
        return courses.Find(filter).Project(c => c.Id).ToList();
    }

    public void RenameLesson(int lessonId, string newTitle)
    {
        var filter = Builders<Course>.Filter.Eq("modules.lessons.lessonId", lessonId);
        var update = Builders<Course>.Update.Set("modules.$[].lessons.$[l].title", newTitle);
        var options = new UpdateOptions
        {
            ArrayFilters = new[]
            {
                new BsonDocumentArrayFilterDefinition<BsonDocument>(new BsonDocument("l.lessonId", lessonId))
            }
        };
        courses.UpdateOne(filter, update, options);
    }

    public List<BsonDocument> VideoStats()
    {
        return courses.Aggregate()
            .Unwind("modules")
            .Unwind("modules.lessons")
            .Unwind("modules.lessons.materials")
            .Match(new BsonDocument("modules.lessons.materials.type", "video"))
            .Group(new BsonDocument
            {
                { "_id", "$category" },
                { "count", new BsonDocument("$sum", 1) },
                { "totalSec", new BsonDocument("$sum", "$modules.lessons.materials.durationSec") }
            })
            .Sort(new BsonDocument("_id", 1))
            .ToList();
    }

    public void DeleteCourse(int id)
    {
        courses.DeleteOne(c => c.Id == id);
    }

    public long Count() => courses.CountDocuments(FilterDefinition<Course>.Empty);

    public string SizeInfo()
    {
        var stats = db.RunCommand<BsonDocument>(new BsonDocument("collStats", "courses"));
        double avgKb = stats["avgObjSize"].ToDouble() / 1024;
        double diskMb = stats["storageSize"].ToDouble() / 1024 / 1024;
        return $"середній документ {avgKb:F1} KB, на диску {diskMb:F1} MB";
    }
}
