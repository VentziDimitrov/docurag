"""
Data models for the web crawler package.
"""

from dataclasses import dataclass, field
from typing import List
from datetime import datetime


@dataclass
class CrawlerConfig:
    """Configuration for the documentation crawler."""

    base_url: str
    max_depth: int = 3
    max_pages: int = 200
    timeout: int = 10
    rate_limit_delay: float = 0.5
    user_agent: str = 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36'

    def __post_init__(self):
        """Validate configuration after initialization."""
        if self.max_depth < 0:
            raise ValueError("max_depth must be non-negative")
        if self.max_pages < 1:
            raise ValueError("max_pages must be at least 1")
        if not self.base_url.startswith(('http://', 'https://')):
            raise ValueError("base_url must start with http:// or https://")
        if self.timeout <= 0:
            raise ValueError("timeout must be positive")
        if self.rate_limit_delay < 0:
            raise ValueError("rate_limit_delay must be non-negative")


@dataclass
class CrawledDocument:
    """Represents a single crawled document."""

    url: str
    title: str
    content: str
    code_blocks: List[str]
    depth: int
    crawled_at: str

    def to_dict(self) -> dict:
        """Convert document to dictionary format."""
        return {
            'url': self.url,
            'title': self.title,
            'content': self.content,
            'code_blocks': self.code_blocks,
            'metadata': {
                'code_blocks': self.code_blocks,
                'depth': self.depth,
                'crawled_at': self.crawled_at
            }
        }
