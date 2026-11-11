# Makefile for Prisma AI Extractors Testing

.PHONY: help install test test-unit test-integration test-e2e test-performance \
        test-quick test-all test-slow test-real coverage lint format security \
        clean fixtures docker-test benchmark profile report docs

# Default target
help:
	@echo "Available targets:"
	@echo "  install          - Install dependencies"
	@echo "  test             - Run all tests (unit + mocked integration/e2e)"
	@echo "  test-unit        - Run unit tests only"
	@echo "  test-integration - Run integration tests (mocked)"
	@echo "  test-e2e         - Run end-to-end tests (mocked)"
	@echo "  test-performance - Run performance tests"
	@echo "  test-quick       - Run quick tests (unit, no slow tests)"
	@echo "  test-all         - Run all test types"
	@echo "  test-slow        - Run slow/performance tests"
	@echo "  test-real        - Run tests with real models (not mocked)"
	@echo "  coverage         - Generate coverage report"
	@echo "  lint             - Run code linting"
	@echo "  format           - Format code"
	@echo "  security         - Run security checks"
	@echo "  clean            - Clean up test artifacts"
	@echo "  fixtures         - Generate test fixtures"
	@echo "  docker-test      - Test in Docker container"
	@echo "  benchmark        - Run benchmarks"
	@echo "  profile          - Profile performance"
	@echo "  report           - Generate comprehensive test report"
	@echo "  docs             - Generate documentation"

# Installation
install:
	pip install -r requirements.txt
	pip install -r requirements-test.txt

install-dev: install
	pip install -e .
	pre-commit install

# Testing
test: fixtures
	pytest tests/ \
		--cov=src \
		--cov-report=term-missing \
		--cov-report=html \
		-v \
		-m "unit or (integration and mock) or (e2e and mock)" \
		--tb=short

test-unit:
	pytest tests/unit/ \
		--cov=src \
		--cov-report=term-missing \
		-v \
		-m "unit and not slow" \
		--tb=short

test-integration: fixtures
	pytest tests/integration/ \
		--cov=src \
		--cov-append \
		--cov-report=term-missing \
		-v \
		-m "integration and mock" \
		--tb=short

test-e2e: fixtures
	pytest tests/e2e/ \
		--cov=src \
		--cov-append \
		--cov-report=term-missing \
		-v \
		-m "e2e and mock" \
		--tb=short

test-performance: fixtures
	pytest tests/performance/ \
		--benchmark-json=benchmark.json \
		-v \
		-m "performance and not slow" \
		--tb=short

test-quick:
	pytest tests/unit/ \
		-v \
		-m "unit and not slow" \
		--tb=short \
		--maxfail=10

test-all: fixtures
	pytest tests/ \
		--cov=src \
		--cov-report=html \
		--cov-report=term \
		-v \
		--tb=short

test-slow: fixtures
	pytest tests/ \
		-v \
		-m "slow or performance" \
		--tb=line \
		--durations=10

test-real: fixtures
	@echo "Running tests with real models (this may take a while)..."
	pytest tests/integration/ tests/e2e/ \
		--cov=src \
		--cov-report=term \
		-v \
		-m "not mock" \
		--tb=short \
		--maxfail=5 \
		--durations=10

# Coverage
coverage: fixtures
	pytest tests/ \
		--cov=src \
		--cov-report=html \
		--cov-report=xml \
		--cov-report=term \
		--cov-fail-under=75 \
		-m "unit or (integration and mock) or (e2e and mock)"
	@echo "Coverage report generated in htmlcov/index.html"

coverage-xml: fixtures
	pytest tests/ \
		--cov=src \
		--cov-report=xml \
		-m "unit or (integration and mock) or (e2e and mock)"

# Code Quality
lint:
	black --check --diff src/ tests/
	isort --check-only --diff src/ tests/
	flake8 src/ tests/ --count --statistics
	mypy src/ --ignore-missing-imports --strict-optional

format:
	black src/ tests/
	isort src/ tests/
	@echo "Code formatted successfully"

security:
	bandit -r src/ -ll
	safety check --json
	@echo "Security checks completed"

# Cleanup
clean:
	rm -rf .pytest_cache/
	rm -rf htmlcov/
	rm -rf .coverage*
	rm -rf benchmark.json
	rm -rf test-report.html
	rm -rf test-results.xml
	find . -type d -name __pycache__ -exec rm -rf {} + 2>/dev/null || true
	find . -type f -name "*.pyc" -delete
	@echo "Cleanup completed"

clean-fixtures:
	rm -rf tests/data/fixtures/images/
	rm -rf tests/data/fixtures/ground_truth/
	rm -f tests/data/fixtures/dataset_index.json
	@echo "Test fixtures cleaned"

# Fixtures and Data
fixtures:
	@echo "Generating test fixtures..."
	cd tests/data/fixtures && python generate_test_fixtures.py
	@echo "Test fixtures generated successfully"

# Docker Testing
docker-test:
	docker build -t prisma-extractors-test .
	docker run --rm \
		-v $(PWD):/app \
		prisma-extractors-test \
		make test-quick

# Benchmarking and Profiling
benchmark: fixtures
	pytest tests/performance/ \
		--benchmark-json=benchmark.json \
		--benchmark-histogram \
		-v \
		-m "performance"
	@echo "Benchmark results saved to benchmark.json"

profile: fixtures
	python -m cProfile -o profile.stats -m pytest tests/unit/ -v -q
	@echo "Profile saved to profile.stats"
	@echo "View with: python -c 'import pstats; pstats.Stats(\"profile.stats\").sort_stats(\"cumulative\").print_stats(20)'"

profile-memory: fixtures
	python -m memory_profiler tests/performance/test_performance.py
	@echo "Memory profiling completed"

# Reporting
report: fixtures
	@echo "Generating comprehensive test report..."
	pytest tests/ \
		--html=test-report.html \
		--self-contained-html \
		--junitxml=test-results.xml \
		--cov=src \
		--cov-report=html \
		--cov-report=term \
		--tb=short \
		-m "unit or (integration and mock) or (e2e and mock)" \
		|| true
	@echo "Test report generated: test-report.html"
	@echo "Coverage report: htmlcov/index.html"
	@echo "JUnit XML: test-results.xml"

# Documentation
docs:
	@echo "Generating documentation..."
	sphinx-build -W -b html docs/source docs/build/html
	@echo "Documentation generated in docs/build/html/index.html"

# Tox environments
tox-test:
	tox -e py311-unit,py311-integration,py311-e2e

tox-lint:
	tox -e lint

tox-coverage:
	tox -e coverage

tox-all:
	tox

# Development helpers
dev-setup: install-dev fixtures
	@echo "Development environment setup complete!"
	@echo "Run 'make test-quick' to verify setup"

watch-tests:
	@echo "Watching for changes... (Ctrl+C to stop)"
	watchexec --exts py --ignore tests/data/ "make test-quick"

# Performance monitoring
monitor-memory:
	@echo "Monitoring memory usage during test execution..."
	/usr/bin/time -v make test-quick 2>&1 | grep -E "(Maximum resident set size|User time|System time)"

monitor-performance: fixtures
	@echo "Running performance monitoring..."
	pytest tests/performance/ \
		--durations=0 \
		--tb=short \
		-v \
		-m "performance" \
		--benchmark-sort=mean

# Validation
validate: lint security coverage
	@echo "All validation checks passed!"

# CI/CD helpers  
ci-setup:
	sudo apt-get update
	sudo apt-get install -y libgl1-mesa-glx libglib2.0-0 libsm6 libxext6 libxrender-dev libgomp1
	pip install --upgrade pip
	make install

ci-test: fixtures test coverage
	@echo "CI test suite completed"

# Parallel testing
test-parallel:
	pytest tests/ \
		--cov=src \
		--cov-report=term \
		-n auto \
		-v \
		-m "unit or (integration and mock)" \
		--tb=short

# Database of commands for quick reference
commands:
	@echo "Quick command reference:"
	@echo "  make test-quick      # Fast unit tests"
	@echo "  make test            # Full test suite (mocked)"
	@echo "  make test-real       # Tests with real models"  
	@echo "  make coverage        # Generate coverage report"
	@echo "  make lint           # Check code quality"
	@echo "  make format         # Format code"
	@echo "  make fixtures       # Generate test data"
	@echo "  make clean          # Clean up artifacts"
	@echo "  make report         # Comprehensive test report"