using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using backend.Models;

namespace backend.Services;

public interface IPythonExecutorService
{
    Task<PythonExecutionResult> ExecuteScriptAsync(string scriptPath, Dictionary<string, string> arguments);
}

public class PythonExecutorService : IPythonExecutorService
{
    private readonly ILogger<PythonExecutorService> _logger;
    private readonly IConfiguration _configuration;
    private readonly string _pythonPath;

    public PythonExecutorService(ILogger<PythonExecutorService> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
        _pythonPath = configuration["Python:ExecutablePath"] ?? "python3";
    }

    public async Task<PythonExecutionResult> ExecuteScriptAsync(
        string scriptPath, 
        Dictionary<string, string> arguments)
    {
        try
        {
            var processStartInfo = new ProcessStartInfo
            {
                FileName = _pythonPath,
                Arguments = BuildArguments(scriptPath, arguments),
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = Path.GetDirectoryName(scriptPath)
            };

            var outputBuilder = new StringBuilder();
            var errorBuilder = new StringBuilder();

            using var process = new Process { StartInfo = processStartInfo };
            
            process.OutputDataReceived += (sender, args) =>
            {
                if (!string.IsNullOrEmpty(args.Data))
                {
                    outputBuilder.AppendLine(args.Data);                    
                }
            };

            process.ErrorDataReceived += (sender, args) =>
            {
                if (!string.IsNullOrEmpty(args.Data))
                {
                    errorBuilder.AppendLine(args.Data);
                    _logger.LogWarning("Python error: {Error}", args.Data);
                }
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            await process.WaitForExitAsync();

            var output = outputBuilder.ToString();
            //_logger.LogInformation("Python output: {Output}", output);

            var result = new PythonExecutionResult
            {
                ExitCode = process.ExitCode,
                Output = JsonSerializer.Deserialize<CrawRawObject>(output ?? "{}") ?? new CrawRawObject(),
                Error = errorBuilder.ToString(),
                Success = process.ExitCode == 0
            };

            if (!result.Success)
            {
                _logger.LogError("Python script failed with exit code {ExitCode}: {Error}", 
                    result.ExitCode, result.Error);
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing Python script");
            return new PythonExecutionResult
            {
                Success = false,
                Error = ex.Message,
                ExitCode = -1
            };
        }
    }

    private string BuildArguments(string scriptPath, Dictionary<string, string> arguments)
    {
        var args = new StringBuilder($"\"{scriptPath}\"");
        
        foreach (var arg in arguments)
        {
            args.Append($" --{arg.Key} \"{arg.Value}\"");
        }

        return args.ToString();
    }
}

public class CrawRawObject
{
    [JsonPropertyName("docs")]
    public List<CrawledDocument> Documents { get; set; } = new();

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;
}

public class PythonExecutionResult
{
    public bool Success { get; set; }
    public int ExitCode { get; set; }
    public object Output { get; set; } = new();
    public string Error { get; set; } = string.Empty;
}