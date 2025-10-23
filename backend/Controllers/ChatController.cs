using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using backend.Models.Requests;
using backend.Models.Responses;
using backend.Models.DTOs;
using backend.Services;
using backend.Hubs;
using backend.Common;

namespace backend.Controllers;

/// <summary>
/// API controller for chat and web crawling operations
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class ChatController : ControllerBase
{
    private readonly IRAGService _ragService;
    private readonly IWebCrawlerService _crawlerService;
    private readonly IHubContext<CrawlerHub> _hubContext;
    private readonly ILogger<ChatController> _logger;

    public ChatController(
        IRAGService ragService,
        IWebCrawlerService crawlerService,
        IHubContext<CrawlerHub> hubContext,
        ILogger<ChatController> logger)
    {
        _ragService = ragService;
        _crawlerService = crawlerService;
        _hubContext = hubContext;
        _logger = logger;
    }

    [HttpPost("crawl")]
    public async Task<ActionResult<ChatResponse>> OnCrawlPage([FromBody] CrawlRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            _logger.LogInformation("Starting crawl for {Url} to index {IndexName}",
                request.Url, request.IndexName);

            var result = await HandleCrawlCommand(request.IndexName, request.Url, request.ConnectionId);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error crawling the page {Url}", request.Url);
            return StatusCode(500, new { error = $"An error occurred while crawling {request.Url}" });
        }
    }

    [HttpPost("message")]
    public async Task<ActionResult<ChatResponse>> OnMessage([FromBody] ChatRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            _logger.LogInformation("Processing message for conversation {ConversationId}",
                request.ConversationId);

            // TODO: Extract index name from request or conversation context
            const string indexName = "beautifulsoup"; // Temporary hardcoded value

            var response = await _ragService.GenerateResponseAsync(indexName, request.Message, request.ConversationId);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing message");
            return StatusCode(500, new { error = "An error occurred processing your message" });
        }
    }

    private async Task<ActionResult<ChatResponse>> HandleCrawlCommand(string indexName, string url, string connectionId)
    {
        try
        {
            // Send initial status update
            await _hubContext.Clients.Client(connectionId).SendAsync(
                HubMethods.CrawlStatusUpdate,
                new { status = "starting", message = $"Starting to crawl {url}..." }
            );

            // Execute web crawling
            var crawlResult = await _crawlerService.CrawlWebsiteAsync(url, async (CrawlStatus status) =>
            {
                // Progress callback
                await _hubContext.Clients.Client(connectionId).SendAsync(
                    HubMethods.CrawlStatusUpdate,
                    new { status = status.Status, message = status.Message, progress = status.Progress }
                );
            });

            if (!crawlResult.Success)
            {
                _logger.LogError("Crawl failed for {Url}: {Error}", url, crawlResult.Error);
                await _hubContext.Clients.Client(connectionId).SendAsync(
                    HubMethods.CrawlStatusUpdate,
                    new { status = "failed", message = crawlResult.Error ?? "Crawling failed for unknown reasons" }
                );

                return BadRequest(new { error = crawlResult.Error ?? "Crawling failed" });
            }

            var processed = await _ragService.ProcessDocumentsAsync(indexName, crawlResult.Documents);

            // At this point, crawl was successful
            await _hubContext.Clients.Client(connectionId).SendAsync(
                HubMethods.CrawlStatusUpdate,
                new
                {
                    status = "completed",
                    message = $"Successfully crawled and indexed {processed} documents",
                    documentsProcessed = processed
                }
            );

            _logger.LogInformation("Successfully processed {Count} documents from {Url} to index {IndexName}",
                processed, url, indexName);

            return Ok(new ChatResponse
            {
                Message = $"Successfully crawled and indexed {crawlResult.DocumentsProcessed} documents from {url}",
                Sources = crawlResult.ProcessedUrls.Select(processedUrl => new DocumentSource
                {
                    Url = processedUrl
                }).ToList(),
                Success = true
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during crawl operation for {Url}", url);
            await _hubContext.Clients.Client(connectionId).SendAsync(
                HubMethods.CrawlStatusUpdate,
                new { status = "error", message = "An error occurred during crawling" }
            );

            return StatusCode(500, new { error = $"Crawling failed: {ex.Message}" });
        }
    }
}