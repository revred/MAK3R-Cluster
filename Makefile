# MAK3R DigitalTwin2 - Development Makefile

.PHONY: help run-api run-pwa run-rig build test clean docker-build docker-up dev-seed

# Default target
help:
	@echo "MAK3R DigitalTwin2 - Available targets:"
	@echo ""
	@echo "  run-api          Start the MAK3R.Api service"
	@echo "  run-pwa          Start the MAK3R.PWA application"
	@echo "  run-rig          Run the test rig with scenarios"
	@echo "  build            Build all projects"
	@echo "  test             Run all tests"
	@echo "  clean            Clean build artifacts"
	@echo "  docker-build     Build Docker images"
	@echo "  docker-up        Start services with Docker Compose"
	@echo "  dev-seed         Reset DB and seed with demo data"
	@echo ""

# Development targets
run-api:
	@echo "Starting MAK3R.Api service..."
	cd services/MAK3R.Api && dotnet run

run-pwa:
	@echo "Starting MAK3R.PWA application..."
	cd apps/MAK3R.PWA && dotnet run

run-rig:
	@echo "Running MAK3R TestRig..."
	cd tests/MAK3R.TestRig && dotnet run

# Build targets
build:
	@echo "Building MAK3R DigitalTwin2 solution..."
	dotnet build MAK3R-Cluster.sln

test:
	@echo "Running all tests..."
	dotnet test MAK3R-Cluster.sln --configuration Release --logger trx --results-directory test-results

clean:
	@echo "Cleaning build artifacts..."
	dotnet clean MAK3R-Cluster.sln
	rm -rf test-results/
	rm -rf */bin */obj

# Docker targets
docker-build:
	@echo "Building Docker images..."
	docker build -t mak3r-api -f services/MAK3R.Api/Dockerfile .
	docker build -t mak3r-pwa -f apps/MAK3R.PWA/Dockerfile .
	docker build -t mak3r-edge -f edges/MAK3R.Edge/Dockerfile .

docker-up:
	@echo "Starting services with Docker Compose..."
	docker-compose -f edges/docker-compose.yml up -d
	docker-compose -f docker-compose.yml up -d

# Development helpers
dev-seed:
	@echo "Resetting database and seeding demo data..."
	cd services/MAK3R.Api && dotnet ef database drop --force
	cd services/MAK3R.Api && dotnet ef database update
	cd scripts && powershell -ExecutionPolicy Bypass -File dev-seed.ps1

# DigitalTwin2 specific targets
dt2-build:
	@echo "Building DigitalTwin2 components..."
	dotnet build libs/MAK3R.KG/MAK3R.KG.csproj
	dotnet build libs/MAK3R.Validation/MAK3R.Validation.csproj
	dotnet build libs/MAK3R.OEE/MAK3R.OEE.csproj
	dotnet build libs/MAK3R.SPOF/MAK3R.SPOF.csproj
	dotnet build services/MAK3R.QnA/MAK3R.QnA.csproj

dt2-test:
	@echo "Running DigitalTwin2 specific tests..."
	dotnet test tests/MAK3R.TestRig/MAK3R.TestRig.csproj
	dotnet test tests/MAK3R.UnitTests/MAK3R.UnitTests.csproj --filter Category=DigitalTwin

dt2-scenarios:
	@echo "Running all DigitalTwin2 test scenarios..."
	cd tests/MAK3R.TestRig && dotnet run -- --scenario A_hidden_bottleneck_lathe --report work/reports/A.pdf
	cd tests/MAK3R.TestRig && dotnet run -- --scenario B_phantom_supplier_switch --report work/reports/B.pdf
	cd tests/MAK3R.TestRig && dotnet run -- --all --report work/reports/weekly.pdf