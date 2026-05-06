FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["src/Faturas.Web/Faturas.Web.csproj", "src/Faturas.Web/"]
RUN dotnet restore "src/Faturas.Web/Faturas.Web.csproj"

COPY . .
RUN dotnet publish "src/Faturas.Web/Faturas.Web.csproj" \
    -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
ENTRYPOINT ["dotnet", "Faturas.Web.dll"]
