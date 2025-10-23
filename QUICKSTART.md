# Quick Start Guide

## 5-Minute Setup

### 1. Clone and Install (2 minutes)

```bash
# Backend
cd Backend
dotnet restore

# Python
cd ../Python
pip install -r requirements.txt

# Frontend
cd ../Frontend
npm install
```

### 2. Configure (2 minutes)

Edit `Backend/appsettings.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=RAGDocs;Integrated Security=true;"
  },
  "AzureOpenAI": {
    "Endpoint": "YOUR_ENDPOINT",
    "ApiKey": "YOUR_KEY",
    "DeploymentName": "gpt-4",
    "EmbeddingDeployment": "text-embedding-ada-002"
  }
}
```

### 3. Run (1 minute)

**Terminal 1 - Backend:**
```bash
cd Backend
dotnet run
```

**Terminal 2 - Frontend:**
```bash
cd Frontend
npm start
```

### 4. Test

1. Open http://localhost:3000
2. Type: `CRAWL: https://docs.python.org/3/tutorial/`
3. Wait for crawling to complete
4. Ask: "How do I create a virtual environment in Python?"

## Essential Commands

### Crawl Documentation
```
CRAWL: https://your-documentation-site.com
```

### Ask Questions
```
How do I configure authentication?
Show me examples of error handling
What are the best practices for...?
```

## Common Issues

### Issue: "Python not found"
**Solution:** Update Python path in `appsettings.json`:
```json
"Python": {
  "ExecutablePath": "python3"  // or "python" on Windows
}
```

### Issue: "Vector database connection failed"
**Solution:** 
1. Check your vector DB credentials in `appsettings.json`
2. Ensure the service is running
3. Verify network connectivity

### Issue: "No documents found"
**Solution:** 
- Crawl documentation first using `CRAWL:` command
- Check that crawling completed successfully
- Verify documents are in the vector database

## Next Steps

1. **Customize Chunking**: Edit `DocumentProcessingService.cs`
2. **Adjust Crawl Limits**: Modify crawler arguments in `WebCrawlerService.cs`
3. **Improve Prompts**: Update system prompts in `RAGService.cs`
4. **Add Authentication**: Implement user authentication in `Program.cs`
5. **Deploy**: Follow deployment guide for production setup

## Architecture Overview

```
┌─────────────┐
│   Frontend  │  React + SignalR
│  (Port 3000)│
└──────┬──────┘
       │
       │ HTTP/WebSocket
       │
┌──────▼──────────────────────────────┐
│        Backend (Port 5000)          │
│  ┌──────────────────────────────┐  │
│  │   ASP.NET Core API           │  │
│  │   - ChatController           │  │
│  │   - SignalR Hub              │  │
│  └────┬────────────┬─────────────┘  │
│       │            │                │
│  ┌────▼─────┐  ┌──▼──────────────┐ │
│  │ Python   │  │ Semantic Kernel │ │
│  │ Executor │  │ - Embeddings    │ │
│  └────┬─────┘  │ - Chat          │ │
│       │        └──┬──────────────┘ │
└───────┼───────────┼─────────────────┘
        │           │
   ┌────▼─────┐ ┌──▼──────────┐
   │  Python  │ │Vector DB    │
   │ Crawler  │ │(Embeddings) │
   │          │ │             │
   └──────────┘ └─────────────┘
                     ┌──────────────┐
                     │ Relational DB│
                     │ (Metadata)   │
                     └──────────────┘
```

## API Endpoints

### POST /api/chat/message
Send a message or crawl command

**Request:**
```json
{
  "message": "Your question or CRAWL: URL",
  "conversationId": "unique-id",
  "connectionId": "signalr-connection-id"
}
```

**Response:**
```json
{
  "message": "Answer text",
  "sources": [
    {
      "title": "Document Title",
      "url": "https://...",
      "snippet": "Relevant excerpt..."
    }
  ],
  "success": true
}
```

### GET /api/chat/history/{conversationId}
Retrieve conversation history

## Environment Variables

Alternative to `appsettings.json`:

```bash
export AZURE_OPENAI_ENDPOINT="your-endpoint"
export AZURE_OPENAI_KEY="your-key"
export VECTOR_DB_ENDPOINT="your-vector-db"
export VECTOR_DB_KEY="your-key"
```

## Pro Tips

1. **Start Small**: Crawl 10-20 pages initially to test
2. **Monitor Costs**: Azure OpenAI charges per token
3. **Cache Embeddings**: Avoid re-embedding same content
4. **Optimize Chunks**: Experiment with chunk size for better retrieval
5. **Use Filters**: Filter search by document source or date

Happy coding! 🚀