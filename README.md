# DocuRAG - Documentation RAG System

A Retrieval-Augmented Generation (RAG) system for technical documentation, built with ASP.NET Core, React, and Python.

## 🚀 Features

- **Web Crawling**: Automatically crawl and extract content from documentation websites
- **Vector Search**: Semantic search using Pinecone vector database
- **AI-Powered Chat**: Ask questions about your documentation using OpenAI GPT
- **Real-time Updates**: SignalR for live crawling progress updates
- **Code Extraction**: Specialized extraction of code blocks from documentation

## 📁 Project Structure

```
docurag/
├── backend/           # ASP.NET Core Web API
│   ├── Common/        # Shared utilities and constants
│   ├── Configuration/ # Strongly-typed settings
│   ├── Controllers/   # API endpoints
│   ├── Models/        # Data models (DTOs, Entities, Requests, Responses)
│   ├── Services/      # Business logic
│   └── Hubs/          # SignalR hubs
├── frontend/          # React application
│   ├── src/
│   └── public/
└── python/            # Python web crawler
    ├── crawl.py       # CLI entry point
    ├── docu_crawler.py # Main crawler
    ├── text_extractor.py # Text extraction utilities
    └── models.py      # Data models
```

## 🛠️ Technology Stack

### Backend
- **Framework**: ASP.NET Core 8.0
- **AI Services**:
  - OpenAI GPT-4 (chat completion)
  - OpenAI text-embedding-3-small (embeddings)
  - Semantic Kernel
- **Vector Database**: Pinecone
- **Database**: SQL Server (Entity Framework Core)
- **Logging**: Serilog
- **Real-time**: SignalR

### Frontend
- **Framework**: React
- **UI**: (To be configured)
- **HTTP Client**: Axios
- **Real-time**: SignalR Client

### Python Crawler
- **HTTP**: requests
- **HTML Parsing**: BeautifulSoup4
- **Language**: Python 3.10+

## 📋 Prerequisites

- .NET 8.0 SDK
- Node.js 18+ and npm
- Python 3.10+
- SQL Server (or SQL Server Express)
- OpenAI API key
- Pinecone API key

## 🔧 Installation

### 1. Clone the Repository

```bash
git clone <repository-url>
cd docurag
```

### 2. Backend Setup

```bash
cd backend

# Restore dependencies
dotnet restore

# Update configuration
cp appsettings.json appsettings.Development.json
# Edit appsettings.Development.json with your keys
```

**Configure `appsettings.Development.json`**:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=DocuRAG;Trusted_Connection=True;"
  },
  "OpenAI": {
    "ApiKey": "your-openai-api-key",
    "ChatModel": "gpt-4",
    "EmbeddingModel": "text-embedding-3-small"
  },
  "Pinecone": {
    "ApiKey": "your-pinecone-api-key",
    "Region": "us-east-1",
    "Dimension": 1536
  },
  "Python": {
    "ExecutablePath": "python3",
    "CrawlerScriptPath": "",
    "TimeoutSeconds": 300
  }
}
```

**Run migrations**:

```bash
dotnet ef database update
```

**Run the backend**:

```bash
dotnet run
```

The API will be available at `https://localhost:5001`

### 3. Frontend Setup

```bash
cd frontend

# Install dependencies
npm install

# Start development server
npm start
```

The frontend will be available at `http://localhost:3000`

### 4. Python Setup

```bash
cd python

# Install dependencies
pip install -r requirements.txt

# Or install individually
pip install requests beautifulsoup4

# Test the crawler
python3 crawl.py --help
```

## 🎯 Usage

### Crawling Documentation

**Via API**:

```bash
POST /api/chat/crawl
Content-Type: application/json

{
  "indexName": "python-docs",
  "url": "https://docs.python.org/3/",
  "connectionId": "signalr-connection-id"
}
```

**Via Python CLI**:

```bash
python3 python/crawl.py \
  --url https://docs.python.org/3/ \
  --output output.json \
  --max-depth 3 \
  --max-pages 100 \
  --verbose
```

### Asking Questions

```bash
POST /api/chat/message
Content-Type: application/json

{
  "message": "How do I use list comprehensions in Python?",
  "conversationId": "unique-conversation-id",
  "connectionId": "signalr-connection-id"
}
```

## 📚 API Endpoints

### Chat Controller

- `POST /api/chat/crawl` - Start crawling a documentation website
- `POST /api/chat/message` - Send a chat message and get AI response

### SignalR Hub

- `Hubs/CrawlerHub` - Real-time crawling progress updates
  - `CrawlStatusUpdate` - Receives status updates during crawling

## 🏗️ Architecture

### RAG Pipeline

1. **Document Ingestion**
   - Python crawler extracts content and code blocks
   - Content split into chunks (default 1000 chars with 200 char overlap)

2. **Embedding Generation**
   - Chunks converted to embeddings using OpenAI
   - Stored in Pinecone with metadata

3. **Query Processing**
   - User question converted to embedding
   - Top-K similar documents retrieved (default K=3)

4. **Response Generation**
   - Retrieved context + user question sent to GPT-4
   - AI generates response based on documentation

### Best Practices Implemented

**Backend (C#)**:
- ✅ Options Pattern for configuration
- ✅ Dependency Injection throughout
- ✅ Strongly-typed models with validation
- ✅ Result pattern for error handling
- ✅ Structured logging with Serilog
- ✅ Proper resource management
- ✅ Thread-safe controllers
- ✅ Specific exception handling

**Python**:
- ✅ Type hints throughout
- ✅ Dataclasses for models
- ✅ Configuration validation
- ✅ Logging instead of print
- ✅ Proper error handling
- ✅ Modular architecture

See [backend/IMPROVEMENTS.md](backend/IMPROVEMENTS.md) for detailed backend improvements.

## 🔍 Configuration

### Environment Variables

You can also configure using environment variables:

```bash
export OpenAI__ApiKey="your-key"
export Pinecone__ApiKey="your-key"
export ConnectionStrings__DefaultConnection="your-connection-string"
```

### Python Crawler Options

| Option | Default | Description |
|--------|---------|-------------|
| `--url` | Required | Base URL to crawl |
| `--output` | Required | Output JSON file path |
| `--max-depth` | 3 | Maximum crawl depth |
| `--max-pages` | 100 | Maximum pages to crawl |
| `--timeout` | 10 | Request timeout (seconds) |
| `--rate-limit` | 0.5 | Delay between requests (seconds) |
| `--verbose` | False | Enable verbose logging |

## 🧪 Testing

### Backend

```bash
cd backend
dotnet test
```

### Frontend

```bash
cd frontend
npm test
```

### Python

```bash
cd python
python -m pytest
```

## 📈 Performance Tuning

### Vector Database

- Adjust `topK` in `RAGConstants` for more/fewer retrieved documents
- Tune chunk size and overlap in `RAGConstants`

### Crawler

- Increase `--rate-limit` for slower, more polite crawling
- Decrease `--max-depth` for faster but shallower crawls
- Adjust `--max-pages` based on documentation size

### LLM

- Adjust `Temperature` in `OpenAISettings` for creativity vs. consistency
- Tune `MaxTokens` for response length

## 🐛 Troubleshooting

### "Configuration validation failed"
- Check that all required API keys are set in `appsettings.Development.json`
- Ensure configuration values meet validation rules

### "Temporary file not cleaned up"
- Check Python script has write permissions to temp directory
- Review logs for cleanup errors

### "No documents found"
- Verify the URL is accessible
- Check crawler logs for errors
- Ensure the website structure is compatible

### "Vector search returns no results"
- Verify Pinecone index exists and has documents
- Check index name matches the crawl request
- Ensure embeddings were generated successfully

## 📝 Development

### Adding a New Model

1. Create file in appropriate folder:
   - DTOs: `backend/Models/DTOs/`
   - Entities: `backend/Models/Entities/`
   - Requests: `backend/Models/Requests/`
   - Responses: `backend/Models/Responses/`

2. Use records for immutability:
```csharp
public record MyModel(string Property1, int Property2);
```

### Adding Configuration

1. Create settings class in `backend/Configuration/`:
```csharp
public class MySettings
{
    public const string SectionName = "MySettings";
    public string Value { get; set; }

    public void Validate()
    {
        if (string.IsNullOrEmpty(Value))
            throw new InvalidOperationException("Value is required");
    }
}
```

2. Register in `Program.cs`:
```csharp
builder.Services.Configure<MySettings>(
    builder.Configuration.GetSection(MySettings.SectionName))
    .AddOptions<MySettings>()
    .Validate(s => { s.Validate(); return true; })
    .ValidateOnStart();
```

## 🤝 Contributing

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

## 📄 License

[Your License Here]

## 🙏 Acknowledgments

- OpenAI for GPT and embeddings
- Pinecone for vector database
- Microsoft for Semantic Kernel
- BeautifulSoup for HTML parsing

## 📧 Contact

[Your contact information]

---

**Note**: This is a RAG system for technical documentation. Ensure you have permission to crawl and index any documentation websites you use.
