namespace Jobuler.Application.Feedback;

/// <summary>
/// Configuration options for the feedback/bug-report feature.
/// Bound from the "Feedback" section in appsettings.json.
/// </summary>
public class FeedbackOptions
{
    /// <summary>
    /// The email address that receives all bug reports and feedback submissions.
    /// </summary>
    public string DeveloperEmail { get; set; } = string.Empty;

    /// <summary>
    /// Maximum number of successful submissions allowed per user within a sliding 60-minute window.
    /// </summary>
    public int MaxSubmissionsPerHour { get; set; } = 5;
}
