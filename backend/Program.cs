using Serilog;
using backend.Services;
using backend.Hubs;
using Microsoft.EntityFrameworkCore;
using Microsoft.SemanticKernel;
using Microsoft.Extensions.AI;
using OpenAI;

var builder = WebApplication.CreateBuilder(args);

// --- Serilog to file + console ---
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File("logs/app.log", rollingInterval: RollingInterval.Day)
    .CreateLogger();

builder.Host.UseSerilog();

// --- Database Configuration ---
builder.Services.AddDbContext<DocumentDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddSignalR();
builder.Services.AddHttpClient(); // used by our services
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// --- AI Services Configuration ---
var openAiApiKey = builder.Configuration["OpenAI:ApiKey"];
var embeddingModel = builder.Configuration["OpenAI:EmbeddingModel"] ?? "text-embedding-3-small";

// Register Semantic Kernel
builder.Services.AddSingleton<Kernel>(sp =>
{
    var kernelBuilder = Kernel.CreateBuilder();
    kernelBuilder.AddOpenAIChatCompletion(
        modelId: builder.Configuration["OpenAI:ChatModel"] ?? "gpt-4",
        apiKey: openAiApiKey!);
    return kernelBuilder.Build();
});

// Register IEmbeddingGenerator using OpenAI
#pragma warning disable SKEXP0010 // Type is for evaluation purposes only and is subject to change or removal in future updates
builder.Services.AddOpenAIEmbeddingGenerator(embeddingModel, openAiApiKey!);
#pragma warning restore SKEXP0010

builder.Services.AddScoped<IVectorDatabaseService, VectorDatabaseService>();
builder.Services.AddScoped<IPythonExecutorService, PythonExecutorService>();
builder.Services.AddScoped<IWebCrawlerService, WebCrawlerService>();
builder.Services.AddScoped<IRAGService, RAGService>();

builder.Services.AddControllers();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins("http://localhost:3000", "http://localhost:5014")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

var app = builder.Build();

app.UseSerilogRequestLogging();
app.UseCors("AllowFrontend");
app.MapControllers();
app.MapHub<CrawlerHub>("Hubs/CrawlerHub");

app.Run();