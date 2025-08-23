# MAK3R.Edge — C# Starter Stack (SQLite NetDiag + LoadGen)

This repo is a **production-ready starter** for the MAK3R Edge runtime:
- .NET 9 Worker with hosted services
- **SQLite NetDiag** (network phases, batch acks, queue depth, error taxonomy)
- **SignalR** uplink with batching, acks, replay spool
- **LoadGen** (up to 1,000 virtual machines) driving the real pipeline
- **Admin API** for health, metrics, NetDiag queries, LoadGen control

## Run (local)
```bash
dotnet build src/Mak3r.Edge/Mak3r.Edge.csproj -c Release
dotnet run --project src/Mak3r.Edge/Mak3r.Edge.csproj
```

Then visit `http://localhost:5080/health` and `http://localhost:5080/metrics`.

## Config
See `appsettings.json`. Override via environment variables (e.g., `Edge__LoadGen__Enabled=true`).

## Systemd
A unit file is provided in `build/systemd/mak3r-edge.service`.

## Docker
Use the `build/docker/Dockerfile` to build an image.
```bash
docker build -t mak3r/edge:dev -f build/docker/Dockerfile .
docker run --net=host -v $PWD/varlib:/var/lib/mak3r mak3r/edge:dev
```
