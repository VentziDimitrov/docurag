# Web Crawler for Technical Documentation

A Python-based web crawler designed to extract content from technical documentation websites.

## Features

- 🔍 Recursive crawling with configurable depth
- 📝 Extracts text content structured by headings
- 💻 Extracts code blocks (handles syntax-highlighted HTML)
- ⚡ Rate limiting to be polite to servers
- 🔧 Configurable via command-line arguments
- 📊 Detailed logging with verbose mode
- ✅ Input validation with clear error messages
- 🎯 Type-safe with dataclasses and type hints

## Installation

### Requirements

- Python 3.10+
- Dependencies:
  ```bash
  pip install requests beautifulsoup4
  ```

### Optional (for better IntelliSense)

```bash
pip install types-beautifulsoup4 types-requests
```

## Usage

### Basic Usage

```bash
python3 crawl.py --url https://docs.example.com --output output.json
```

### Advanced Usage

```bash
python3 crawl.py \
  --url https://docs.example.com \
  --output output.json \
  --max-depth 5 \
  --max-pages 200 \
  --timeout 15 \
  --rate-limit 1.0 \
  --verbose
```

### Command-Line Arguments

| Argument | Description | Default |
|----------|-------------|---------|
| `--url` | Base URL to start crawling (required) | - |
| `--output` | Output JSON file path (required) | - |
| `--max-depth` | Maximum crawl depth | 3 |
| `--max-pages` | Maximum pages to crawl | 100 |
| `--timeout` | Request timeout in seconds | 10 |
| `--rate-limit` | Delay between requests in seconds | 0.5 |
| `--verbose, -v` | Enable verbose logging | False |

## Architecture

### Project Structure

```
python/
├── __init__.py           # Package initialization
├── models.py             # Data models (CrawlerConfig, CrawledDocument)
├── text_extractor.py     # Text extraction utilities
├── docu_crawler.py       # Main crawler logic
├── crawl.py             # Standalone CLI entry point
└── web_crawler.py       # Package-based CLI entry point
```

### Key Components

#### 1. **Models** (`models.py`)

- `CrawlerConfig`: Configuration dataclass with validation
- `CrawledDocument`: Represents a crawled page

#### 2. **TextExtractor** (`text_extractor.py`)

- `split_text_on_words()`: Splits long text into chunks
- `extract_text_content()`: Extracts structured text content
- `extract_code_blocks()`: Extracts code from `<pre>` and `<code>` tags

#### 3. **DocumentationCrawler** (`docu_crawler.py`)

- Main crawler class
- Uses `requests.Session` for better performance
- Implements rate limiting
- Comprehensive logging

## Best Practices Implemented

### 1. ✅ Type Safety
- Uses dataclasses for structured data
- Type hints throughout the codebase
- Better IDE support and error detection

### 2. ✅ Separation of Concerns
- Text extraction logic in separate helper class
- Configuration in dedicated dataclass
- Clear module responsibilities

### 3. ✅ Logging Instead of Print
- Structured logging with levels (DEBUG, INFO, ERROR)
- Verbose mode for debugging
- Exception tracking with `exc_info=True`

### 4. ✅ Constants Management
- Module-level constants (e.g., `SKIP_EXTENSIONS`)
- Easy to modify and maintain

### 5. ✅ Input Validation
- Configuration validation in `__post_init__`
- Clear error messages
- Fails fast with helpful feedback

### 6. ✅ Resource Management
- Uses `requests.Session` for connection pooling
- `pathlib.Path` for file operations
- Context managers for file handling

### 7. ✅ Documentation
- PEP 257 compliant docstrings
- Type hints in function signatures
- Module-level documentation

### 8. ✅ Error Handling
- Specific exception types
- Graceful degradation
- Detailed error logging

## Output Format

```json
{
  "docs": [
    {
      "url": "https://example.com/page",
      "title": "Page Title",
      "content": "Extracted text content...",
      "code_blocks": ["code example 1", "code example 2"],
      "metadata": {
        "code_blocks": ["code example 1", "code example 2"],
        "depth": 0,
        "crawled_at": "2025-10-24 12:00:00"
      }
    }
  ],
  "status": "success"
}
```

## Examples

### Basic Crawl

```bash
python3 crawl.py \
  --url https://docs.python.org/3/ \
  --output python_docs.json \
  --max-depth 2 \
  --max-pages 50
```

### Verbose Debugging

```bash
python3 crawl.py \
  --url https://docs.python.org/3/ \
  --output python_docs.json \
  --verbose
```

### Slow, Polite Crawl

```bash
python3 crawl.py \
  --url https://docs.example.com \
  --output docs.json \
  --rate-limit 2.0 \
  --timeout 20
```

## Development

### Running Tests

```bash
# Test configuration validation
python3 -c "from models import CrawlerConfig; config = CrawlerConfig(base_url='http://example.com')"

# Test import structure
python3 -c "from docu_crawler import DocumentationCrawler; from text_extractor import TextExtractor"
```

### Package Usage

```python
from models import CrawlerConfig
from docu_crawler import DocumentationCrawler

# Create config
config = CrawlerConfig(
    base_url='https://docs.example.com',
    max_depth=3,
    max_pages=100
)

# Create crawler
crawler = DocumentationCrawler(config)

# Start crawling
documents = crawler.crawl()

# Process documents
for doc in documents:
    print(f"Title: {doc.title}")
    print(f"URL: {doc.url}")
    print(f"Code blocks: {len(doc.code_blocks)}")
```

## License

This project is part of the DocuRAG system.

## Contributing

When contributing, please follow these guidelines:
- Use type hints
- Add docstrings (PEP 257 format)
- Use logging instead of print statements
- Validate inputs
- Write descriptive commit messages
