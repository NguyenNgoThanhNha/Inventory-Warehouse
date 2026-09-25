FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Directory.Build.props Inventory.sln ./
COPY src/Inventory.Domain/Inventory.Domain.csproj src/Inventory.Domain/
COPY src/Inventory.Persistence/Inventory.Persistence.csproj src/Inventory.Persistence/
COPY src/Inventory.Application/Inventory.Application.csproj src/Inventory.Application/
COPY src/Inventory.Infrastructure/Inventory.Infrastructure.csproj src/Inventory.Infrastructure/
COPY src/Inventory.Api/Inventory.Api.csproj src/Inventory.Api/
RUN dotnet restore src/Inventory.Api/Inventory.Api.csproj
COPY src/ src/
RUN dotnet publish src/Inventory.Api/Inventory.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "Inventory.Api.dll"]
