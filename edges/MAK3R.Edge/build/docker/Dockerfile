# MAK3R.Edge Dockerfile
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ./src/Mak3r.Edge ./Mak3r.Edge
RUN dotnet publish Mak3r.Edge/Mak3r.Edge.csproj -c Release -o /out

FROM mcr.microsoft.com/dotnet/runtime:9.0
WORKDIR /app
COPY --from=build /out .
ENV DOTNET_gcServer=1
ENV DOTNET_ThreadPool_ForceMinWorkerThreads=8
VOLUME ["/var/lib/mak3r"]
EXPOSE 5080
ENTRYPOINT ["dotnet","Mak3r.Edge.dll"]
