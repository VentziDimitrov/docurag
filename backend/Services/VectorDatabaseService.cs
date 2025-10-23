using Pinecone;
using backend.Models;
using System.Text.RegularExpressions;
using System.Linq;
using Microsoft.Extensions.AI;

namespace backend.Services;

public interface IVectorDatabaseService
{
    Task<List<RetrievedDocument>> SearchAsync(string indexName, ReadOnlyMemory<float> vector, uint topK);
    Task StoreDocumentAsync(string indexName, string documentId, string content, Embedding<float> embedding, Dictionary<string, object> metadata);
    Task<bool> DeleteDocumentAsync(string indexName, string documentId);
    Task CreateIndexIfNotExists(string indexName);  
    EmbeddingGenerationOptions GetEmbeddingOptions();  
}

public class PineconeOptions
{
    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string Region { get; set; } = string.Empty;
    public int Dimension { get; set; } = 1536;
    public string Namespace { get; set; } = "__default__";
}

/// <summary>
/// Vector database service 
/// - Pinecone
/// </summary>
public class VectorDatabaseService : IVectorDatabaseService
{
    private readonly ILogger<VectorDatabaseService> _logger;
    private readonly PineconeClient _pineconeClient;
    private readonly PineconeOptions options;

    public VectorDatabaseService(
        ILogger<VectorDatabaseService> logger,
        DocumentDbContext dbContext,
        IConfiguration configuration)
    {
        _logger = logger;
        

        var apiKey = configuration["Pinecone:ApiKey"] ?? string.Empty;
        var region = configuration["Pinecone:Region"] ?? "us-east-1";
        var model = configuration["Pinecone:Model"] ?? "text-embedding-3-small";
        var dimension = int.Parse(configuration["Pinecone:Dimension"] ?? "1536");
        var namespacee = configuration["Pinecone:NNamespace"] ?? "__default__";

        options = new PineconeOptions
        {
            ApiKey = apiKey,
            Region = region,
            Dimension = dimension,
            Model = model,
            Namespace = namespacee
        };

        _pineconeClient = new PineconeClient(apiKey);
    }

    public async Task<List<RetrievedDocument>> SearchAsync(string indexName, ReadOnlyMemory<float> vector, uint topK)
    {
        try
        {
            var index = _pineconeClient.Index(indexName);

            var queryRequest = new QueryRequest
            {
                Vector = vector,
                TopK = topK,
                IncludeMetadata = true,
                IncludeValues = false
            };

            QueryResponse response = (await index.QueryAsync(queryRequest)) ?? new QueryResponse { Matches = Array.Empty<ScoredVector>() };

            var documents = new List<RetrievedDocument>();
            if (response.Matches == null)
                return documents;

            foreach (var match in response.Matches)
            {
                documents.Add(new RetrievedDocument
                {
                    Id = match.Id,
                    Title = match.Metadata?["title"]?.ToString() ?? "",
                    Url = match.Metadata?["url"]?.ToString() ?? "",
                    Content = match.Metadata?["content"]?.ToString() ?? "",
                    Score = match.Score ?? 0f
                });
            }

            return documents;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error performing Pinecone vector search");
            throw;
        }
    }

    public async Task StoreDocumentAsync(string indexName, string documentId, string content, Embedding<float> embedding, Dictionary<string, object> metadata)
    {
        try
        {
            IndexClient? index = _pineconeClient.Index(indexName);
            if (index == null)
            {
                _logger.LogInformation("No index found with name {DocumentId} in Pinecone", documentId);
            }

            // Add content to metadata
            var enrichedMetadata = new Dictionary<string, MetadataValue?>();
            foreach (var kvp in metadata)
            {
                enrichedMetadata[kvp.Key] = ConvertToMetadataValue(kvp.Value);
            }
            enrichedMetadata["content"] = content;

            var vector = new Vector
            {
                Id = documentId,
                Values = embedding.Vector,
                Metadata = new Metadata(enrichedMetadata)
            };
            await index.UpsertAsync(new UpsertRequest { Vectors = new List<Vector> { vector }});

            _logger.LogInformation("Document {DocumentId} stored in Pinecone", documentId);     
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error storing document in Pinecone");
            return;
        }
    }

    public async Task<bool> DeleteDocumentAsync(string indexName, string documentId)
    {
        try
        {
            var index = _pineconeClient.Index(indexName);
            await index.DeleteAsync(new DeleteRequest { Ids = new List<string> { documentId } });

            _logger.LogInformation("Document {DocumentId} deleted from Pinecone", documentId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting document from Pinecone");
            return false;
        }
    }

    public async Task CreateIndexIfNotExists(string indexName)
    {
        try
        {
            IndexList indexes = await _pineconeClient.ListIndexesAsync();
            if (indexes == null)
            {
                _logger.LogError("Failed to retrieve index list from Pinecone.");
                return;
            }

#pragma warning disable CS8604 // Possible null reference argument.
            if (!indexes.Indexes.Any(i => i.Name == indexName))
            {
                var createIndexRequest = await _pineconeClient.CreateIndexAsync(new CreateIndexRequest
                {
                    Name = indexName,
                    VectorType = VectorType.Dense,
                    Dimension = options.Dimension,
                    Metric = MetricType.Cosine,
                    Spec = new ServerlessIndexSpec
                    {
                        Serverless = new ServerlessSpec
                        {
                            Cloud = ServerlessSpecCloud.Aws,
                            Region = options.Region
                        }
                    },
                    // TODO: Rework on production
                    DeletionProtection = DeletionProtection.Disabled
                });
                if (createIndexRequest == null)
                {
                    _logger.LogError("Failed to create Pinecone index: {IndexName}", indexName);
                    return;
                }
                _logger.LogInformation("Created Pinecone index: {IndexName}", indexName);
            }
#pragma warning restore CS8604 // Possible null reference argument.
            else
            {

                _logger.LogInformation("Pinecone index {IndexName} already exists", indexName);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating Pinecone index");
        }
    }

    public EmbeddingGenerationOptions GetEmbeddingOptions()
    {
        return new EmbeddingGenerationOptions
        {
            ModelId = options.Model,
            Dimensions = options.Dimension
        };
    }

    private static MetadataValue? ConvertToMetadataValue(object value)
    {
        return value switch
        {
            string s => s,
            int i => i,
            long l => l,
            float f => f,
            double d => d,
            bool b => b,
            _ => value?.ToString() ?? string.Empty
        };
    }

    
}

