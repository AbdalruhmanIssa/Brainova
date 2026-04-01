public class FeedbackResponse
{
    public Guid Id { get; set; }
    public Guid ReportId { get; set; }

    public string SupervisorId { get; set; } = default!;
    public string SupervisorName { get; set; } = string.Empty;

    public string StudentId { get; set; } = default!;
    public string StudentName { get; set; } = string.Empty;

    public string Comment { get; set; } = default!;
    public DateTime CreatedAt { get; set; }
}