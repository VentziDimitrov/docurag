using Pinecone;
using backend.Models.DTOs;
using backend.Configuration;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;

namespace backend.Services;

public interface IVectorDatabaseService
{
    Task<List<RetrievedDocument>> SearchAsync(string indexName, ReadOnlyMemory<float> vector, uint topK);
    Task StoreDocumentAsync(string indexName, string documentId, string content, Embedding<float> embedding, Dictionary<string, object> metadata);
    Task<bool> DeleteDocumentAsync(string indexName, string documentId);
    Task CreateIndexIfNotExists(string indexName);  
    EmbeddingGenerationOptions GetEmbeddingOptions();  
}

/// <summary>
/// Vector database service using Pinecone
/// </summary>
public class VectorDatabaseService : IVectorDatabaseService
{
    private readonly ILogger<VectorDatabaseService> _logger;
    private readonly PineconeClient _pineconeClient;
    private readonly PineconeSettings _settings;

    public VectorDatabaseService(
        ILogger<VectorDatabaseService> logger,
        IOptions<PineconeSettings> pineconeSettings)
    {
        _logger = logger;
        _settings = pineconeSettings.Value;

        _pineconeClient = new PineconeClient(_settings.ApiKey);
        _logger.LogInformation("Initialized Pinecone client for region {Region}", _settings.Region);
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
                _logger.LogError("No index found with name {DocumentId} in Pinecone", documentId);
                return;
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
            _logger.LogError(ex, "Error storing document in Pinecone: ");
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
            if (indexes == null || indexes.Indexes == null)
            {
                _logger.LogError("Failed to retrieve index list from Pinecone.");
                return;
            }
            if (!indexes.Indexes.Any(i => i.Name == indexName))
            {
                var createIndexRequest = await _pineconeClient.CreateIndexAsync(new CreateIndexRequest
                {
                    Name = indexName,
                    VectorType = VectorType.Dense,
                    Dimension = _settings.Dimension,
                    Metric = MetricType.Cosine,
                    Spec = new ServerlessIndexSpec
                    {
                        Serverless = new ServerlessSpec
                        {
                            Cloud = ServerlessSpecCloud.Aws,
                            Region = _settings.Region
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
            ModelId = _settings.Model,
            Dimensions = _settings.Dimension
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

