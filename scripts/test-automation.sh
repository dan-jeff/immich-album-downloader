#!/bin/bash

# Test Automation Framework for Immich Album Downloader
# Provides automated testing, building, and validation workflows

set -e

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

PROJECT_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
BACKEND_DIR="$PROJECT_ROOT/backend"

echo -e "${GREEN}🚀 Immich Album Downloader - Test Automation Framework${NC}"
echo "Project Root: $PROJECT_ROOT"
echo

# Function to run tests with proper error handling
run_tests() {
    echo -e "${YELLOW}Running unit tests...${NC}"
    cd "$BACKEND_DIR"
    
    if dotnet test --verbosity normal; then
        echo -e "${GREEN}✓ Tests passed${NC}"
        return 0
    else
        echo -e "${RED}✗ Tests failed${NC}"
        return 1
    fi
}

# Function to build the project
build_project() {
    echo -e "${YELLOW}Building project...${NC}"
    cd "$BACKEND_DIR"
    
    if dotnet build --configuration Release; then
        echo -e "${GREEN}✓ Build successful${NC}"
        return 0
    else
        echo -e "${RED}✗ Build failed${NC}"
        return 1
    fi
}

# Function to run security tests specifically
run_security_tests() {
    echo -e "${YELLOW}Running security tests...${NC}"
    cd "$BACKEND_DIR"
    
    if dotnet test --filter "Category=Security" --verbosity normal; then
        echo -e "${GREEN}✓ Security tests passed${NC}"
        return 0
    else
        echo -e "${RED}✗ Security tests failed${NC}"
        return 1
    fi
}

# Function to run component tests specifically
run_component_tests() {
    echo -e "${YELLOW}Running component tests...${NC}"
    cd "$BACKEND_DIR"
    
    if dotnet test --filter "Category=Component" --verbosity normal; then
        echo -e "${GREEN}✓ Component tests passed${NC}"
        return 0
    else
        echo -e "${RED}✗ Component tests failed${NC}"
        return 1
    fi
}

# Function to validate code quality
validate_code_quality() {
    echo -e "${YELLOW}Validating code quality...${NC}"
    cd "$BACKEND_DIR"
    
    # Check for compilation warnings
    local warnings
    warnings=$(dotnet build 2>&1 | grep -c "warning" || true)
    
    if [ "$warnings" -gt 0 ]; then
        echo -e "${YELLOW}⚠ Found $warnings build warnings${NC}"
    else
        echo -e "${GREEN}✓ No build warnings${NC}"
    fi
    
    echo -e "${GREEN}✓ Code quality validation completed${NC}"
}

# Function to run full CI pipeline
run_ci_pipeline() {
    echo -e "${YELLOW}Running full CI pipeline...${NC}"
    
    local failed=0
    
    # Build first
    if ! build_project; then
        failed=1
    fi
    
    # Run tests
    if ! run_tests; then
        failed=1
    fi
    
    # Validate code quality
    validate_code_quality
    
    if [ $failed -eq 0 ]; then
        echo -e "${GREEN}🎉 CI pipeline completed successfully!${NC}"
        return 0
    else
        echo -e "${RED}❌ CI pipeline failed${NC}"
        return 1
    fi
}

# Function to show usage
show_usage() {
    echo "Usage: $0 [OPTION]"
    echo "Test automation framework for Immich Album Downloader"
    echo
    echo "Options:"
    echo "  test          Run all unit tests"
    echo "  build         Build the project"
    echo "  security      Run security tests only"
    echo "  component     Run component tests only"
    echo "  quality       Validate code quality"
    echo "  ci            Run full CI pipeline"
    echo "  help          Show this help message"
    echo
    echo "Examples:"
    echo "  $0 test       # Run all tests"
    echo "  $0 ci         # Run full CI pipeline"
    echo "  $0 security   # Run security tests only"
}

# Main execution logic
case "${1:-}" in
    "test")
        run_tests
        ;;
    "build")
        build_project
        ;;
    "security")
        run_security_tests
        ;;
    "component")
        run_component_tests
        ;;
    "quality")
        validate_code_quality
        ;;
    "ci")
        run_ci_pipeline
        ;;
    "help"|"--help"|"-h")
        show_usage
        ;;
    "")
        echo -e "${YELLOW}No command specified. Running full CI pipeline...${NC}"
        run_ci_pipeline
        ;;
    *)
        echo -e "${RED}Error: Unknown command '$1'${NC}"
        echo
        show_usage
        exit 1
        ;;
esac