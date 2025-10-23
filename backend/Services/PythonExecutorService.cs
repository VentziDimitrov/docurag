using System.Diagnostics;
using System.Text;
using System.Text.Json;
using backend.Models.DTOs;
using backend.Configuration;
using Microsoft.Extensions.Options;

namespace backend.Services;

public interface IPythonExecutorService
{
    Task<PythonExecutionResult> ExecuteScriptAsync(string scriptPath, Dictionary<string, string> arguments);
}

public class PythonExecutorService : IPythonExecutorService
{
    private readonly ILogger<PythonExecutorService> _logger;
    private readonly PythonSettings _pythonSettings;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public PythonExecutorService(
        ILogger<PythonExecutorService> logger,
        IOptions<PythonSettings> pythonSettings)
    {
        _logger = logger;
        _pythonSettings = pythonSettings.Value;
    }

    public async Task<PythonExecutionResult> ExecuteScriptAsync(
        string scriptPath, 
        Dictionary<string, string> arguments)
    {
        try
        {
            _logger.LogInformation("Executing Python script: {ScriptPath}", scriptPath);

            var processStartInfo = new ProcessStartInfo
            {
                FileName = _pythonSettings.ExecutablePath,
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
            var errorOutput = errorBuilder.ToString();

            _logger.LogDebug("Python stdout: {Output}", output);

            if (!string.IsNullOrWhiteSpace(errorOutput))
            {
                _logger.LogWarning("Python stderr: {Error}", errorOutput);
            }

            var crawlerOutput = string.IsNullOrWhiteSpace(output)
                ? new CrawlerOutput()
                : JsonSerializer.Deserialize<CrawlerOutput>(output, JsonOptions) ?? new CrawlerOutput();

            var result = new PythonExecutionResult
            {
                ExitCode = process.ExitCode,
                Output = crawlerOutput,
                Error = errorOutput,
                Success = process.ExitCode == 0
            };

            if (!result.Success)
            {
                _logger.LogError("Python script failed with exit code {ExitCode}: {Error}",
                    result.ExitCode, result.Error);
            }
            else
            {
                _logger.LogInformation("Python script completed successfully");
            }

            return result;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse Python script output as JSON");
            return new PythonExecutionResult
            {
                Success = false,
                Error = $"Invalid JSON output: {ex.Message}",
                ExitCode = -1
            };
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

        _logger.LogDebug("Python arguments: {Arguments}", args.ToString());
        return args.ToString();
    }
}