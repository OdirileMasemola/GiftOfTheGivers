using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace GiftOfTheGivers.IntegrationTests.Infrastructure;

/// <summary>
/// Sends the app's log messages to the xUnit test output, so each test result
/// (in Test Explorer and in Azure DevOps) shows what the server did.
/// </summary>
public sealed class TestOutputLoggerProvider : ILoggerProvider
{
    private readonly Func<ITestOutputHelper?> _output;

    public TestOutputLoggerProvider(Func<ITestOutputHelper?> output) => _output = output;

    public ILogger CreateLogger(string categoryName) => new TestOutputLogger(categoryName, _output);

    public void Dispose()
    {
    }

    private sealed class TestOutputLogger : ILogger
    {
        private readonly string _category;
        private readonly Func<ITestOutputHelper?> _output;

        public TestOutputLogger(string category, Func<ITestOutputHelper?> output)
        {
            _category = category.Split('.').Last();
            _output = output;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            var output = _output();
            if (output is null)
            {
                return;
            }

            var line = $"[{logLevel}] {_category}: {formatter(state, exception)}";
            if (exception is not null)
            {
                line += $" ({exception.GetType().Name}: {exception.Message})";
            }

            try
            {
                output.WriteLine(line);
            }
            catch (InvalidOperationException)
            {
                // The test that owned this output has already finished.
            }
        }
    }
}
