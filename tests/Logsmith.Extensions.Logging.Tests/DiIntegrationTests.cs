using Logsmith.Extensions.Logging;
using Logsmith.Sinks;
using Microsoft.Extensions.DependencyInjection;

namespace Logsmith.Extensions.Logging.Tests;

[TestFixture]
public class DiIntegrationTests
{
    [SetUp]
    public void SetUp() => LogManager.Reset();

    [TearDown]
    public void TearDown() => LogManager.Reset();

    [Test]
    public void AddLogsmith_RegistersILogger()
    {
        var sink = new RecordingSink();
        var services = new ServiceCollection();
        services.AddLogsmith(c =>
        {
            c.MinimumLevel = Logsmith.LogLevel.Debug;
            c.AddSink(sink);
        });

        var sp = services.BuildServiceProvider();
        var logger = sp.GetService<Logsmith.ILogger>();

        Assert.That(logger, Is.Not.Null);
    }

    [Test]
    public void AddLogsmith_ILogger_CanDispatch()
    {
        var sink = new RecordingSink();
        var services = new ServiceCollection();
        services.AddLogsmith(c =>
        {
            c.MinimumLevel = Logsmith.LogLevel.Debug;
            c.AddSink(sink);
        });

        var sp = services.BuildServiceProvider();
        var logger = sp.GetRequiredService<Logsmith.ILogger>();
        logger.Debug("DI dispatched message");

        Assert.That(sink.Entries, Has.Count.EqualTo(1));
        Assert.That(sink.Entries[0].Message, Does.Contain("DI dispatched message"));
    }

    [Test]
    public void AddLogsmith_RegistersILoggerOfT()
    {
        var sink = new RecordingSink();
        var services = new ServiceCollection();
        services.AddLogsmith(c =>
        {
            c.MinimumLevel = Logsmith.LogLevel.Debug;
            c.AddSink(sink);
        });

        var sp = services.BuildServiceProvider();
        var logger = sp.GetService<Logsmith.ILogger<DiIntegrationTests>>();

        Assert.That(logger, Is.Not.Null);
    }

    [Test]
    public void AddLogsmith_ILoggerOfT_UsesCategoryFromTypeName()
    {
        var sink = new RecordingSink();
        var services = new ServiceCollection();
        services.AddLogsmith(c =>
        {
            c.MinimumLevel = Logsmith.LogLevel.Debug;
            c.AddSink(sink);
        });

        var sp = services.BuildServiceProvider();
        var logger = sp.GetRequiredService<Logsmith.ILogger<DiIntegrationTests>>();
        logger.Information("typed message");

        Assert.That(sink.Entries, Has.Count.EqualTo(1));
        Assert.That(sink.Entries[0].Category, Is.EqualTo("DiIntegrationTests"));
    }

    [Test]
    public void AddLogsmith_DifferentTypesGetDifferentCategories()
    {
        var sink = new RecordingSink();
        var services = new ServiceCollection();
        services.AddLogsmith(c =>
        {
            c.MinimumLevel = Logsmith.LogLevel.Debug;
            c.AddSink(sink);
        });

        var sp = services.BuildServiceProvider();
        var logger1 = sp.GetRequiredService<Logsmith.ILogger<DiIntegrationTests>>();
        var logger2 = sp.GetRequiredService<Logsmith.ILogger<LogsmithLoggerTests>>();

        logger1.Debug("from type1");
        logger2.Debug("from type2");

        Assert.That(sink.Entries, Has.Count.EqualTo(2));
        Assert.That(sink.Entries[0].Category, Is.EqualTo("DiIntegrationTests"));
        Assert.That(sink.Entries[1].Category, Is.EqualTo("LogsmithLoggerTests"));
    }
}
