# RAG Documentation System

A complete Retrieval-Augmented Generation (RAG) system for crawling and querying technical documentation from websites.

## 🏗️ Architecture

### Backend (ASP.NET Core + C#)
- **REST API** for chat interactions
- **Python script execution** for web crawling
- **Microsoft Semantic Kernel** for AI/LLM integration
- **SignalR** for real-time crawl status updates
- **Dual database architecture**:
  - Relational DB (SQL Server/PostgreSQL) for metadata
  - Vector DB (Azure AI Search/Qdrant/Pinecone) for embeddings

### Frontend (React.js)
- Modern chat interface
- Real-time status updates via SignalR
- Command parsing for `CRAWL:` operations
- Source citations display

### Python Crawling Module
- BeautifulSoup4 for HTML parsing
- Configurable depth and page limits
- Respectful rate limiting
- Structured JSON output

## 📋 Prerequisites

- **.NET 8.0 SDK** or later
- **Python 3.8+** with pip
- **Node.js 18+** and npm
- **SQL Server** or **PostgreSQL**
- **Azure OpenAI** or **OpenAI API** access
- **Vector Database** (choose one):
  - Azure AI Search
  - Qdrant
  - Pinecone
  - pgvector

## 🚀 Setup Instructions

### 1. Backend Setup

#### Install .NET dependencies:
```bash
cd Backend
dotnet restore
```

#### Configure appsettings.json:
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Your-SQL-Connection-String"
  },
  "AzureOpenAI": {
    "Endpoint": "https://your-resource.openai.azure.com",
    "ApiKey": "your-api-key",
    "DeploymentName": "gpt-4",
    "EmbeddingDeployment": "text-embedding-ada-002"
  },
  "VectorDatabase": {
    "Type": "AzureAISearch",
    "Endpoint": "your-vector-db-endpoint",
    "ApiKey": "your-api-key"
  }
}
```

#### Create database migrations:
```bash
dotnet ef migrations add InitialCreate
dotnet ef database update
```

#### Run the backend:
```bash
dotnet run
```

The API will be available at `http://localhost:5000`

### 2. Python Setup

#### Install Python dependencies:
```bash
cd Python
pip install -r requirements.txt
```

#### Test the crawler:
```bash
python web_crawler.py --url https://docs.python.org --output test_output.json --max-depth 2 --max-pages 50
```

### 3. Frontend Setup

#### Install npm dependencies:
```bash
cd Frontend
npm install
```

#### Update API endpoints in App.jsx if needed:
```javascript
const API_BASE_URL = 'http://localhost:5000/api';
const HUB_URL = 'http://localhost:5000/crawlerHub';
```

#### Run the development server:
```bash
npm start
```

The frontend will be available at `http://localhost:3000`

## 📖 Usage

### Starting a Crawl

Type in the chat interface:
```
CRAWL: https://docs.microsoft.com/en-us/dotnet
```

The system will:
1. Execute the Python crawler
2. Extract content and structure
3. Generate embeddings for each document chunk
4. Store in vector database
5. Update you with real-time progress

### Asking Questions

After crawling documentation, simply ask questions:
```
How do I implement dependency injection in ASP.NET Core?
```

The system will:
1. Generate embedding for your question
2. Search vector database for relevant chunks
3. Provide context to LLM
4. Return answer with source citations

## 🔧 Configuration Options

### Crawler Settings

Edit `Python/web_crawler.py` or pass arguments:

- `--max-depth`: Maximum crawl depth (default: 3)
- `--max-pages`: Maximum pages to crawl (default: 100)
- `--url`: Starting URL

### Chunking Strategy

Edit `Backend/Services/DocumentProcessingService.cs`:

```csharp
private const int ChunkSize = 1000; // Characters per chunk
private const int ChunkOverlap = 200; // Overlap between chunks
```

### Vector Search

Adjust in `Backend/Services/RAGService.cs`:

```csharp
private const int TopKResults = 5; // Number of documents to retrieve
```

## 🗄️ Database Schema

### Relational Database (Conversations)

```sql
CREATE TABLE Conversations (
    Id INT PRIMARY KEY IDENTITY,
    ConversationId NVARCHAR(100) NOT NULL,
    UserMessage NVARCHAR(MAX) NOT NULL,
    AssistantMessage NVARCHAR(MAX) NOT NULL,
    Timestamp DATETIME2 NOT NULL,
    Sources NVARCHAR(MAX)
);

CREATE TABLE Documents (
    Id INT PRIMARY KEY IDENTITY,
    Url NVARCHAR(2000) NOT NULL UNIQUE,
    Title NVARCHAR(500) NOT NULL,
    CrawledAt DATETIME2 NOT NULL,
    ContentLength INT NOT NULL,
    Metadata NVARCHAR(MAX)
);
```

### Vector Database Schema

Each document chunk stored with:
- **id**: Unique identifier
- **content**: Text chunk
- **embedding**: Vector representation (1536 dimensions for Ada-002)
- **metadata**: Title, URL, chunk index, etc.

## 🔐 Security Considerations

1. **API Keys**: Never commit API keys to version control
2. **CORS**: Update CORS policy in `Program.cs` for production
3. **Rate Limiting**: Implement rate limiting for API endpoints
4. **Input Validation**: The system validates URLs before crawling
5. **Content Filtering**: Be mindful of what documentation you crawl

## 🐛 Troubleshooting

### Python Script Not Executing

- Check Python path in `appsettings.json`
- Verify Python dependencies are installed
- Check file permissions on the script

### Vector Search Returns No Results

- Ensure documents are properly indexed
- Check embedding generation is working
- Verify vector database connection

### SignalR Connection Fails

- Check CORS configuration
- Verify both frontend and backend URLs
- Check firewall settings

## 📚 Vector Database Options

### Azure AI Search
```bash
dotnet add package Azure.Search.Documents
```

### Qdrant
```bash
dotnet add package Qdrant.Client
```

### Pinecone
```bash
dotnet add package Pinecone
```

## 🎯 Future Enhancements

- [ ] Support for PDF documentation
- [ ] Multi-language support
- [ ] Document versioning
- [ ] Advanced filtering by documentation source
- [ ] Export chat history
- [ ] Admin dashboard for monitoring
- [ ] Automatic re-crawling on schedule
- [ ] Support for authentication-required sites

## 📄 License

This is a template project for building RAG systems.

## 🤝 Contributing

Feel free to customize and extend this system for your specific needs!

## 📞 Support

For issues with:
- **Semantic Kernel**: https://github.com/microsoft/semantic-kernel
- **Azure OpenAI**: https://learn.microsoft.com/azure/ai-services/openai/
- **Vector Databases**: Refer to specific provider documentation