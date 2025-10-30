namespace BugStore.Api.Controllers.Models;

public record ReportQuery(
    DateTime? StartDate = null,
    DateTime? EndDate = null,
    string? GroupBy = "day");
