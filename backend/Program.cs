using Serilog;
using backend.Services;
using backend.Hubs;
using backend.Configuration;
using backend.Common;
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
builder.Services.AddHttpClient();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// --- Configure Settings with Options Pattern ---
builder.Services.Configure<OpenAISettings>(
    builder.Configuration.GetSection(OpenAISettings.SectionName))
    .AddOptions<OpenAISettings>()
    .Validate(settings =>
    {
        try
        {
            settings.Validate();
            return true;
        }
        catch
        {
            return false;
        }
    }, "OpenAI settings validation failed")
    .ValidateOnStart();

builder.Services.Configure<PineconeSettings>(
    builder.Configuration.GetSection(PineconeSettings.SectionName))
    .AddOptions<PineconeSettings>()
    .Validate(settings =>
    {
        try
        {
            settings.Validate();
            return true;
        }
        catch
        {
            return false;
        }
    }, "Pinecone settings validation failed")
    .ValidateOnStart();

builder.Services.Configure<PythonSettings>(
    builder.Configuration.GetSection(PythonSettings.SectionName))
    .AddOptions<PythonSettings>()
    .Validate(settings =>
    {
        try
        {
            settings.Validate();
            return true;
        }
        catch
        {
            return false;
        }
    }, "Python settings validation failed")
    .ValidateOnStart();

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
#pragma warning disable SKEXP0010
builder.Services.AddOpenAIEmbeddingGenerator(embeddingModel, openAiApiKey!);
#pragma warning restore SKEXP0010

builder.Services.AddScoped<IVectorDatabaseService, VectorDatabaseService>();
builder.Services.AddScoped<IPythonExecutorService, PythonExecutorService>();
builder.Services.AddScoped<IWebCrawlerService, WebCrawlerService>();
builder.Services.AddScoped<IRAGService, RAGService>();

builder.Services.AddControllers();

builder.Services.AddCors(options =>
{
    options.AddPolicy(CorsPolicy.AllowFrontend, policy =>
    {
        policy.WithOrigins("http://localhost:3000", "http://localhost:5014")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

var app = builder.Build();

app.UseSerilogRequestLogging();
app.UseCors(CorsPolicy.AllowFrontend);
app.MapControllers();
app.MapHub<CrawlerHub>(HubRoutes.CrawlerHub);

app.Run();