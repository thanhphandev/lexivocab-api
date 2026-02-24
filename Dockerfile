# ──────────────────────────────────────────────────────────────────
# LexiVocab API — Multi-stage Docker Build
# ──────────────────────────────────────────────────────────────────

# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
WORKDIR /app

# Copy project files first for layer caching (restore only reruns when .csproj changes)
COPY LexiVocabAPI.slnx .
COPY src/LexiVocab.Domain/LexiVocab.Domain.csproj src/LexiVocab.Domain/
COPY src/LexiVocab.Application/LexiVocab.Application.csproj src/LexiVocab.Application/
COPY src/LexiVocab.Infrastructure/LexiVocab.Infrastructure.csproj src/LexiVocab.Infrastructure/
COPY src/LexiVocab.API/LexiVocab.API.csproj src/LexiVocab.API/
RUN dotnet restore LexiVocabAPI.slnx

# Copy full source and publish
COPY . .
RUN dotnet publish src/LexiVocab.API/LexiVocab.API.csproj -c Release -o /out --no-restore

# Stage 2: Runtime (minimal image ~80MB)
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine
WORKDIR /app

# Install ICU for globalization support
RUN apk add --no-cache icu-libs
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false

COPY --from=build /out .

EXPOSE 8080
ENTRYPOINT ["dotnet", "LexiVocab.API.dll"]
