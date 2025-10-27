namespace backend.Common;

/// <summary>
/// Application-wide constants
/// </summary>
public static class RAGConstants
{
    /// <summary>
    /// Default size of text chunks in characters
    /// </summary>
    public const int DefaultChunkSize = 1000;

    /// <summary>
    /// Default overlap between chunks in characters
    /// </summary>
    public const int DefaultChunkOverlap = 200;

    /// <summary>
    /// Default number of documents to retrieve from vector search
    /// </summary>
    public const uint DefaultTopK = 3;

    /// <summary>
    /// Default length of document snippets in characters
    /// </summary>
    public const int DefaultSnippetLength = 200;

    /// <summary>
    /// Multiplier for initial document retrieval before reranking
    /// </summary>
    public const uint ReRankMultiplier = 3;

    /// <summary>
    /// Minimum confidence score to consider a response reliable
    /// </summary>
    public const float MinConfidenceThreshold = 0.7f;

    /// <summary>
    /// Enable reranking of documents (can be disabled for performance)
    /// </summary>
    public const bool EnableReRanking = true;

    /// <summary>
    /// Enable confidence assessment (can be disabled for performance)
    /// </summary>
    public const bool EnableConfidenceAssessment = true;
}

/// <summary>
/// SignalR hub routes
/// </summary>
public static class HubRoutes
{
    public const string CrawlerHub = "Hubs/CrawlerHub";
}

/// <summary>
/// SignalR method names
/// </summary>
public static class HubMethods
{
    public const string CrawlStatusUpdate = "CrawlStatusUpdate";
}

/// <summary>
/// CORS policy names
/// </summary>
public static class CorsPolicy
{
    public const string AllowFrontend = "AllowFrontend";
}
