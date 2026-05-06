FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["src/Faturas.Api/Faturas.Api.csproj",                         "src/Faturas.Api/"]
COPY ["src/Faturas.Application/Faturas.Application.csproj",         "src/Faturas.Application/"]
COPY ["src/Faturas.Domain/Faturas.Domain.csproj",                   "src/Faturas.Domain/"]
COPY ["src/Faturas.Infrastructure/Faturas.Infrastructure.csproj",   "src/Faturas.Infrastructure/"]
RUN dotnet restore "src/Faturas.Api/Faturas.Api.csproj"

COPY . .
RUN dotnet publish "src/Faturas.Api/Faturas.Api.csproj" \
    -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
ENTRYPOINT ["dotnet", "Faturas.Api.dll"]
