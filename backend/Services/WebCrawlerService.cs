using System.Text.Json;
using backend.Models.DTOs;
using backend.Models.Responses;
using backend.Configuration;
using Microsoft.Extensions.Options;

namespace backend.Services;

public interface IWebCrawlerService
{
    Task<CrawlResult> CrawlWebsiteAsync(string url, Func<CrawlStatus, Task> progressCallback);
}

public class WebCrawlerService : IWebCrawlerService
{
    private readonly IPythonExecutorService _pythonExecutor;
    private readonly ILogger<WebCrawlerService> _logger;
    private readonly PythonSettings _pythonSettings;
    private readonly string _crawlerScriptPath;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public WebCrawlerService(
        IPythonExecutorService pythonExecutor,
        IOptions<PythonSettings> pythonSettings,
        ILogger<WebCrawlerService> logger)
    {
        _pythonExecutor = pythonExecutor;
        _logger = logger;
        _pythonSettings = pythonSettings.Value;

        // Use configured path or fallback to relative path
        _crawlerScriptPath = !string.IsNullOrWhiteSpace(_pythonSettings.CrawlerScriptPath)
            ? _pythonSettings.CrawlerScriptPath
            : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "python", "crawl.py");
    }

    public async Task<CrawlResult> CrawlWebsiteAsync(string url, Func<CrawlStatus, Task> progressCallback)
    {
        string? tempFilePath = null;

        try
        {
            await progressCallback(new CrawlStatus
            {
                Status = "initializing",
                Message = "Preparing to crawl website...",
                Progress = 0
            });

            // Create temp file path
            tempFilePath = Path.Combine(Path.GetTempPath(), $"crawl_{Guid.NewGuid()}.json");
            _logger.LogInformation("Starting crawl of {Url}, output to {TempFile}", url, tempFilePath);

            // Execute Python crawler script
            var arguments = new Dictionary<string, string>
            {
                { "url", url },
                { "output", tempFilePath }
            };

            await progressCallback(new CrawlStatus
            {
                Status = "crawling",
                Message = "Crawling website and extracting content...",
                Progress = 25
            });

            var result = await _pythonExecutor.ExecuteScriptAsync(_crawlerScriptPath, arguments);

            if (!result.Success)
            {
                _logger.LogError("Crawler execution failed with exit code {ExitCode}: {Error}",
                    result.ExitCode, result.Error);

                return new CrawlResult
                {
                    Success = false,
                    Error = $"Crawler failed: {result.Error}"
                };
            }

            await progressCallback(new CrawlStatus
            {
                Status = "processing",
                Message = "Processing and indexing documents...",
                Progress = 50
            });

            // Parse crawler output
            var crawledData = ParseCrawlerOutput(tempFilePath);

            if (crawledData.Count == 0)
            {
                _logger.LogWarning("No documents were crawled from {Url}", url);
                return new CrawlResult
                {
                    Success = false,
                    Error = "No documents were found to crawl"
                };
            }

            await progressCallback(new CrawlStatus
            {
                Status = "indexing",
                Message = "Creating embeddings and storing in vector database...",
                Progress = 75
            });

            await progressCallback(new CrawlStatus
            {
                Status = "completed",
                Message = "Crawling completed successfully!",
                Progress = 100
            });

            _logger.LogInformation("Successfully crawled {Count} documents from {Url}",
                crawledData.Count, url);

            return new CrawlResult
            {
                Success = true,
                DocumentsProcessed = crawledData.Count,
                ProcessedUrls = crawledData.Select(d => d.Url).ToList(),
                Documents = crawledData
            };
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse crawler output JSON");
            return new CrawlResult
            {
                Success = false,
                Error = "Invalid crawler output format"
            };
        }
        catch (IOException ex)
        {
            _logger.LogError(ex, "File I/O error during crawling");
            return new CrawlResult
            {
                Success = false,
                Error = "File operation failed during crawling"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during web crawling of {Url}", url);
            return new CrawlResult
            {
                Success = false,
                Error = $"Crawling failed: {ex.Message}"
            };
        }
        finally
        {
            // Cleanup temporary file
            if (!string.IsNullOrEmpty(tempFilePath) && File.Exists(tempFilePath))
            {
                try
                {
                    File.Delete(tempFilePath);
                    _logger.LogDebug("Deleted temporary file {TempFile}", tempFilePath);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to delete temporary file {TempFile}", tempFilePath);
                }
            }
        }
    }

    private List<CrawledDocument> ParseCrawlerOutput(string outputPath)
    {
        if (!File.Exists(outputPath))
        {
            _logger.LogError("Crawler output file not found: {Path}", outputPath);
            throw new FileNotFoundException($"Crawler output file not found: {outputPath}");
        }

        var json = File.ReadAllText(outputPath);

        if (string.IsNullOrWhiteSpace(json))
        {
            _logger.LogError("Crawler output file is empty: {Path}", outputPath);
            return [];
        }

        var crawlerOutput = JsonSerializer.Deserialize<CrawlerOutput>(json, JsonOptions);

        if (crawlerOutput?.Documents == null)
        {
            _logger.LogWarning("No documents found in crawler output");
            return [];
        }

        _logger.LogInformation("Parsed {Count} documents from crawler output", crawlerOutput.Documents.Count);
        return crawlerOutput.Documents;
    }
}