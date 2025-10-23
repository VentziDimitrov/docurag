using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using backend.Models;
using backend.Services;
using backend.Hubs;
using System.Text.RegularExpressions;

namespace backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ChatController : ControllerBase
{
    private readonly IRAGService _ragService;
    private readonly IWebCrawlerService _crawlerService;
    private readonly IHubContext<CrawlerHub> _hubContext;
    private readonly ILogger<ChatController> _logger;
    public string indexName { get; set; } = "beautifulsoup";

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
        try
        {
            indexName = request.IndexName;
            var result = await HandleCrawlCommand(request.Url, request.ConnectionId);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error crawling the page");
            return StatusCode(500, new { error = "An error occurred on crawling " + request.Url });
        }

    }

    [HttpPost("message")]
    public async Task<ActionResult<ChatResponse>> OnMessage([FromBody] ChatRequest request)
    {
        try
        {
            // Regular RAG query
            var response =  await _ragService.GenerateResponseAsync(indexName, request.Message, request.ConversationId);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing message");
            return StatusCode(500, new { error = "An error occurred processing your message" });
        }
    }
    


    private async Task<ActionResult<ChatResponse>> HandleCrawlCommand(string url, string connectionId)
    {
        try
        {
            // Send initial status update
            await _hubContext.Clients.Client(connectionId).SendAsync(
                "CrawlStatusUpdate", 
                new { status = "starting", message = $"Starting to crawl {url}..." }
            );

            // Execute web crawling
            var crawlResult = await _crawlerService.CrawlWebsiteAsync(url, async (CrawlStatus status) =>
            {
                // Progress callback
                await _hubContext.Clients.Client(connectionId).SendAsync(
                    "CrawlStatusUpdate",
                    new { status = status.Status, message = status.Message, progress = status.Progress }
                );
            });

            var processed = await _ragService.ProcessDocumentsAsync(indexName, crawlResult.Documents);

            if (crawlResult.Success)
            {
                await _hubContext.Clients.Client(connectionId).SendAsync(
                    "CrawlStatusUpdate",
                    new { 
                        status = "completed", 
                        message = $"Successfully crawled {processed} documents",
                        documentsProcessed = processed
                    }
                );

                return Ok(new ChatResponse
                {
                    Message = $"Successfully crawled and indexed {crawlResult.DocumentsProcessed} documents from {url}",
                    Sources = crawlResult.ProcessedUrls.Select(url => new DocumentSource
                    {
                        Url = url,
                    }).ToList(),
                    Success = true
                });
            }
            else if(!string.IsNullOrEmpty(crawlResult.Error))
            {
                await _hubContext.Clients.Client(connectionId).SendAsync(
                    "CrawlStatusUpdate",
                    new { status = "failed", message = crawlResult.Error }
                );

                return BadRequest(new { error = crawlResult.Error });
            } else
            {
                await _hubContext.Clients.Client(connectionId).SendAsync(
                    "CrawlStatusUpdate",
                    new { status = "failed", message = "Crawling failed for unknown reasons" }
                );

                return BadRequest(new { error = "Crawling failed for unknown reasons" });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during crawl operation");
            await _hubContext.Clients.Client(connectionId).SendAsync(
                "CrawlStatusUpdate",
                new { status = "error", message = "An error occurred during crawling" }
            );
            
            return StatusCode(500, new { error = "Crawling failed" });
        }
    }
/* 
    [HttpGet("history/{conversationId}")]
    public async Task<ActionResult<List<ChatMessage>>> GetConversationHistory(string conversationId)
    {
        try
        {
            var history = await _ragService.GetConversationHistoryAsync(conversationId);
            return Ok(history);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving conversation history");
            return StatusCode(500, new { error = "Failed to retrieve history" });
        }
    } */
}