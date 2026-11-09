namespace EmployeeSync.Infrastructure.Events;

public sealed class EventsOptions
{
    public const string SectionName = "Events";

    /// <summary>"InMemory" (default) or "Nats".</summary>
    public string Publisher { get; set; } = "InMemory";

    public NatsSettings Nats { get; set; } = new();

    public sealed class NatsSettings
    {
        public string Url { get; set; } = "nats://localhost:4222";

        public string SubjectPrefix { get; set; } = "tracktik.employees";
    }
}
