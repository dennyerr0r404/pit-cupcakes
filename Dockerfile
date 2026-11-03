# Imagem para publicação em serviços de nuvem (Render, Railway, Fly.io, etc.)
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish src/Cupcakes.Api/Cupcakes.Api.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_ENVIRONMENT=Production
# A plataforma informa a porta na variável PORT (padrão 8080).
# Configure DATABASE_URL (postgres://usuario:senha@host:porta/banco) ou ConnectionStrings__Cupcakes.
CMD ASPNETCORE_URLS=http://+:${PORT:-8080} dotnet Cupcakes.Api.dll
