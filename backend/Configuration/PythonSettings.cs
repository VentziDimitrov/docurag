namespace backend.Configuration;

/// <summary>
/// Configuration settings for Python script execution
/// </summary>
public class PythonSettings
{
    public const string SectionName = "Python";

    public string ExecutablePath { get; set; } = "python3";
    public string CrawlerScriptPath { get; set; } = string.Empty;
    public int TimeoutSeconds { get; set; } = 300; // 5 minutes default

    /// <summary>
    /// Validates the configuration settings
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when configuration is invalid</exception>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(ExecutablePath))
            throw new InvalidOperationException("Python ExecutablePath is required");

        if (TimeoutSeconds <= 0)
            throw new InvalidOperationException($"TimeoutSeconds must be positive, got {TimeoutSeconds}");
    }
}
