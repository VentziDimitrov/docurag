using System.Text.Json;
using backend.Models;
using System.Collections.Generic;

namespace backend.Services;

public interface IWebCrawlerService
{
    Task<CrawlResult> CrawlWebsiteAsync(string url, Func<CrawlStatus, Task> progressCallback);
}

public class WebCrawlerService : IWebCrawlerService
{
    private readonly IPythonExecutorService _pythonExecutor;
    private readonly ILogger<WebCrawlerService> _logger;
    private readonly string _crawlerScriptPath;

    public WebCrawlerService(
        IPythonExecutorService pythonExecutor,
        IConfiguration configuration,
        ILogger<WebCrawlerService> logger)
    {
        _pythonExecutor = pythonExecutor;
        _logger = logger;
        _crawlerScriptPath = configuration["Python:CrawlerScriptPath"] 
            ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "Python", "web_crawler.py");
    }

    public async Task<CrawlResult> CrawlWebsiteAsync(string url, Func<CrawlStatus, Task> progressCallback)
    {
        try
        {
            await progressCallback(new CrawlStatus 
            { 
                Status = "initializing", 
                Message = "Preparing to crawl website...",
                Progress = 0 
            });

            // Execute Python crawler script
            var arguments = new Dictionary<string, string>
            {
                { "url", url },
                { "output", Path.Combine(Path.GetTempPath(), $"crawl_{Guid.NewGuid()}.json") }
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
            var crawledData = ParseCrawlerOutput(arguments["output"]);

            await progressCallback(new CrawlStatus 
            { 
                Status = "indexing", 
                Message = "Creating embeddings and storing in vector database...",
                Progress = 75 
            });

            // Process and store documents
            //var processedCount = await _documentProcessor.ProcessDocumentsAsync(crawledData);

            await progressCallback(new CrawlStatus 
            { 
                Status = "completed", 
                Message = "Crawling completed successfully!",
                Progress = 100 
            });

            // Cleanup temporary file
            if (File.Exists(arguments["output"]))
            {
                //File.Delete(arguments["output"]);
                Console.WriteLine($"Temporary file retained for debugging: {arguments["output"]}");
            }

            return new CrawlResult
            {
                Success = true,
                DocumentsProcessed = crawledData.Count,
                ProcessedUrls = crawledData.Select(d => d.Url).ToList(),
                Documents = crawledData
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during web crawling");
            return new CrawlResult
            {
                Success = false,
                Error = ex.Message
            };
        }
    }

    private List<CrawledDocument> ParseCrawlerOutput(string outputPath)
    {
        try
        {
            var json = File.ReadAllText(outputPath);
            return JsonSerializer.Deserialize<List<CrawledDocument>>(json) ?? new List<CrawledDocument>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing crawler output");
            return new List<CrawledDocument>();
        }
    }
}

public class CrawlResult
{
    public bool Success { get; set; }
    public int DocumentsProcessed { get; set; }
    public List<string> ProcessedUrls { get; set; } = new();
    public List<CrawledDocument> Documents { get; set; } = new List<CrawledDocument>();
    public string? Error { get; set; }
}

public class CrawlStatus
{
    public string Status { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public int Progress { get; set; }
}