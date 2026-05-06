using System.Runtime.CompilerServices;
using FleetReports.Models;
using LiteDB;

internal static class TestInitializer
{
    [ModuleInitializer]
    internal static void Initialize()
    {
        // Pre-warm BsonMapper.Global so type registration happens single-threaded
        // before xUnit starts running test classes in parallel.
        using var db = new LiteDatabase(":memory:");
        db.GetCollection<KillmailDocument>("killmails");
        db.GetCollection<ReportDocument>("reports");
    }
}
