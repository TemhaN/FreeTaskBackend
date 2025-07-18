# Dockerfile для backend
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app
COPY . .
RUN dotnet restore
RUN dotnet add package System.Net.Http
RUN dotnet build
RUN dotnet publish -c Release -o out
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/out .
EXPOSE 7125
ENTRYPOINT ["dotnet", "FreeTaskBackend.dll"]