FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["src/Faturas.Infrastructure.Migrations/Faturas.Infrastructure.Migrations.csproj", "src/Faturas.Infrastructure.Migrations/"]
RUN dotnet restore "src/Faturas.Infrastructure.Migrations/Faturas.Infrastructure.Migrations.csproj"

COPY . .
RUN dotnet publish "src/Faturas.Infrastructure.Migrations/Faturas.Infrastructure.Migrations.csproj" \
    -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/runtime:8.0
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Faturas.Infrastructure.Migrations.dll"]
