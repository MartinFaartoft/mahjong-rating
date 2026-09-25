# syntax=docker/dockerfile:1.7
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Rating.sln ./
COPY src/Rating.Domain/Rating.Domain.csproj src/Rating.Domain/
COPY src/Rating.Application/Rating.Application.csproj src/Rating.Application/
COPY src/Rating.Infrastructure/Rating.Infrastructure.csproj src/Rating.Infrastructure/
COPY src/Rating.Api/Rating.Api.csproj src/Rating.Api/
RUN dotnet restore Rating.sln
COPY . .
RUN dotnet publish src/Rating.Api/Rating.Api.csproj -c Release -o /app --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
RUN apt-get update \
 && apt-get install -y --no-install-recommends curl \
 && rm -rf /var/lib/apt/lists/*
COPY --from=build /app ./
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "Rating.dll"]
