#!/usr/bin/env python3
"""
Standalone entry point for the web crawler.

This script can be run directly from the command line.
"""

import argparse
import json
import logging
import sys
from pathlib import Path

from docu_crawler import DocumentationCrawler
from models import CrawlerConfig


def setup_logging(verbose: bool = False) -> None:
    """
    Configure logging for the application.

    Args:
        verbose: If True, set log level to DEBUG, otherwise INFO
    """
    level = logging.DEBUG if verbose else logging.INFO
    logging.basicConfig(
        level=level,
        format='%(asctime)s - %(name)s - %(levelname)s - %(message)s',
        datefmt='%Y-%m-%d %H:%M:%S'
    )


def main():
    """Main entry point for the web crawler CLI."""
    parser = argparse.ArgumentParser(
        description='Crawl technical documentation websites',
        formatter_class=argparse.ArgumentDefaultsHelpFormatter
    )
    parser.add_argument('--url', required=True, help='Base URL to start crawling')
    parser.add_argument('--output', required=True, help='Output JSON file path')
    parser.add_argument('--max-depth', type=int, default=3, help='Maximum crawl depth')
    parser.add_argument('--max-pages', type=int, default=100, help='Maximum pages to crawl')
    parser.add_argument('--timeout', type=int, default=10, help='Request timeout in seconds')
    parser.add_argument('--rate-limit', type=float, default=0.5, help='Delay between requests in seconds')
    parser.add_argument('--verbose', '-v', action='store_true', help='Enable verbose logging')

    args = parser.parse_args()

    # Setup logging
    setup_logging(args.verbose)
    logger = logging.getLogger(__name__)

    try:
        # Create configuration
        config = CrawlerConfig(
            base_url=args.url,
            max_depth=args.max_depth,
            max_pages=args.max_pages,
            timeout=args.timeout,
            rate_limit_delay=args.rate_limit
        )

        # Create crawler and start crawling
        crawler = DocumentationCrawler(config)
        documents = crawler.crawl()

        # Convert documents to dict format for JSON serialization
        documents_dict = [doc.to_dict() for doc in documents]

        # Write output using pathlib
        output_path = Path(args.output)
        output_path.parent.mkdir(parents=True, exist_ok=True)

        with output_path.open('w', encoding='utf-8') as f:
            json.dump(documents_dict, f, ensure_ascii=False, indent=2)

        # Print result
        result = {
            "docs": documents_dict,
            "status": "success"
        }

        logger.info(f"Success! Crawled {len(documents)} documents")
        print(json.dumps(result, ensure_ascii=False, indent=2), file=sys.stdout)
        sys.exit(0)

    except ValueError as e:
        logger.error(f"Configuration error: {e}")
        sys.exit(1)
    except Exception as e:
        logger.error(f"Fatal error: {e}", exc_info=True)
        sys.exit(1)


if __name__ == '__main__':
    main()
