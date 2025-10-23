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
