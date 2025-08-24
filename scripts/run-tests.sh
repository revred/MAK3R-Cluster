#!/bin/bash

# MAK3R Test Runner Script for Linux/macOS
# Runs unit tests, integration tests, and generates coverage reports

set -e

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
CYAN='\033[0;36m'
NC='\033[0m' # No Color

# Default values
TEST_PROJECT="all"
COVERAGE=false
WATCH=false
FILTER=""
VERBOSE=false

# Function to print colored output
print_colored() {
    local color=$1
    local message=$2
    echo -e "${color}${message}${NC}"
}

# Parse command line arguments
while [[ $# -gt 0 ]]; do
    case $1 in
        -p|--project)
            TEST_PROJECT="$2"
            shift 2
            ;;
        -c|--coverage)
            COVERAGE=true
            shift
            ;;
        -w|--watch)
            WATCH=true
            shift
            ;;
        -f|--filter)
            FILTER="$2"
            shift 2
            ;;
        -v|--verbose)
            VERBOSE=true
            shift
            ;;
        -h|--help)
            echo "Usage: $0 [OPTIONS]"
            echo "Options:"
            echo "  -p, --project    Test project to run (unit|integration|all)"
            echo "  -c, --coverage   Generate coverage report"
            echo "  -w, --watch      Run tests in watch mode"
            echo "  -f, --filter     Filter tests by name pattern"
            echo "  -v, --verbose    Verbose output"
            echo "  -h, --help       Show this help message"
            exit 0
            ;;
        *)
            echo "Unknown option $1"
            exit 1
            ;;
    esac
done

check_prerequisites() {
    print_colored $CYAN "🔍 Checking prerequisites..."
    
    # Check if .NET is installed
    if ! command -v dotnet &> /dev/null; then
        print_colored $RED "❌ .NET SDK not found. Please install .NET 8.0 SDK."
        exit 1
    fi
    
    local dotnet_version=$(dotnet --version)
    print_colored $GREEN "✅ .NET SDK: $dotnet_version"
    
    # Check if we're in the correct directory
    if [[ ! -f "MAK3R-Cluster.sln" ]]; then
        print_colored $RED "❌ Must run from MAK3R-Cluster root directory"
        exit 1
    fi
    
    print_colored $GREEN "✅ Prerequisites check passed"
    echo
}

restore_packages() {
    print_colored $CYAN "📦 Restoring NuGet packages..."
    
    if dotnet restore --verbosity quiet; then
        print_colored $GREEN "✅ Package restoration completed"
    else
        print_colored $RED "❌ Package restoration failed"
        exit 1
    fi
    echo
}

build_solution() {
    print_colored $CYAN "🔨 Building solution..."
    
    if [[ "$VERBOSE" == "true" ]]; then
        verbosity="normal"
    else
        verbosity="quiet"
    fi
    
    if dotnet build --no-restore --verbosity $verbosity; then
        print_colored $GREEN "✅ Build completed successfully"
    else
        print_colored $RED "❌ Build failed"
        exit 1
    fi
    echo
}

run_unit_tests() {
    print_colored $CYAN "🧪 Running Unit Tests..."
    
    local args=(
        "test"
        "tests/MAK3R.UnitTests/MAK3R.UnitTests.csproj"
        "--no-build"
        "--verbosity"
        "normal"
        "--logger"
        "console;verbosity=detailed"
    )
    
    if [[ -n "$FILTER" ]]; then
        args+=("--filter" "$FILTER")
    fi
    
    if [[ "$COVERAGE" == "true" ]]; then
        args+=(
            "--collect"
            "XPlat Code Coverage"
            "--results-directory"
            "./TestResults/UnitTests"
        )
    fi
    
    if [[ "$WATCH" == "true" ]]; then
        args+=("--watch")
    fi
    
    if dotnet "${args[@]}"; then
        print_colored $GREEN "✅ Unit tests completed"
        return 0
    else
        print_colored $RED "❌ Unit tests failed"
        return 1
    fi
}

run_integration_tests() {
    print_colored $CYAN "🔗 Running Integration Tests..."
    
    local args=(
        "test"
        "tests/MAK3R.IntegrationTests/MAK3R.IntegrationTests.csproj"
        "--no-build"
        "--verbosity"
        "normal"
        "--logger"
        "console;verbosity=detailed"
    )
    
    if [[ -n "$FILTER" ]]; then
        args+=("--filter" "$FILTER")
    fi
    
    if [[ "$COVERAGE" == "true" ]]; then
        args+=(
            "--collect"
            "XPlat Code Coverage"
            "--results-directory"
            "./TestResults/IntegrationTests"
        )
    fi
    
    if dotnet "${args[@]}"; then
        print_colored $GREEN "✅ Integration tests completed"
        return 0
    else
        print_colored $RED "❌ Integration tests failed"
        return 1
    fi
}

generate_coverage_report() {
    if [[ "$COVERAGE" != "true" ]]; then
        return
    fi
    
    print_colored $CYAN "📊 Generating coverage report..."
    
    # Check if reportgenerator is installed
    if ! dotnet tool list -g | grep -q "dotnet-reportgenerator-globaltool"; then
        print_colored $YELLOW "Installing ReportGenerator..."
        dotnet tool install -g dotnet-reportgenerator-globaltool
    fi
    
    # Generate HTML report
    local coverage_files=$(find ./TestResults -name "coverage.cobertura.xml" -type f)
    
    if [[ -n "$coverage_files" ]]; then
        local coverage_input=$(echo "$coverage_files" | tr '\n' ';')
        coverage_input=${coverage_input%;}  # Remove trailing semicolon
        
        if reportgenerator "-reports:$coverage_input" "-targetdir:./TestResults/Coverage" "-reporttypes:Html;Badges"; then
            print_colored $GREEN "✅ Coverage report generated at ./TestResults/Coverage/index.html"
        else
            print_colored $RED "❌ Coverage report generation failed"
        fi
    else
        print_colored $YELLOW "⚠️ No coverage files found"
    fi
}

show_test_summary() {
    local unit_tests_passed=$1
    local integration_tests_passed=$2
    
    echo
    print_colored $CYAN "📋 Test Summary"
    print_colored $CYAN "═══════════════"
    
    if [[ "$TEST_PROJECT" == "all" ]] || [[ "$TEST_PROJECT" == "unit" ]]; then
        if [[ $unit_tests_passed -eq 0 ]]; then
            print_colored $GREEN "Unit Tests:        ✅ PASSED"
        else
            print_colored $RED "Unit Tests:        ❌ FAILED"
        fi
    fi
    
    if [[ "$TEST_PROJECT" == "all" ]] || [[ "$TEST_PROJECT" == "integration" ]]; then
        if [[ $integration_tests_passed -eq 0 ]]; then
            print_colored $GREEN "Integration Tests: ✅ PASSED"
        else
            print_colored $RED "Integration Tests: ❌ FAILED"
        fi
    fi
    
    if [[ "$COVERAGE" == "true" ]]; then
        print_colored $CYAN "Coverage Report:   ./TestResults/Coverage/index.html"
    fi
    
    echo
}

# Main execution
print_colored $GREEN "🚀 MAK3R Test Runner"
print_colored $GREEN "═══════════════════"
echo

check_prerequisites
restore_packages
build_solution

unit_tests_exit_code=0
integration_tests_exit_code=0

# Run tests based on the selected project
case ${TEST_PROJECT,,} in
    unit)
        run_unit_tests || unit_tests_exit_code=$?
        ;;
    integration)
        run_integration_tests || integration_tests_exit_code=$?
        ;;
    all)
        run_unit_tests || unit_tests_exit_code=$?
        echo
        run_integration_tests || integration_tests_exit_code=$?
        ;;
    *)
        print_colored $RED "❌ Invalid test project: $TEST_PROJECT. Use 'unit', 'integration', or 'all'"
        exit 1
        ;;
esac

generate_coverage_report
show_test_summary $unit_tests_exit_code $integration_tests_exit_code

# Exit with error code if any tests failed
if [[ $unit_tests_exit_code -ne 0 ]] || [[ $integration_tests_exit_code -ne 0 ]]; then
    exit 1
fi

print_colored $GREEN "🎉 All tests passed!"