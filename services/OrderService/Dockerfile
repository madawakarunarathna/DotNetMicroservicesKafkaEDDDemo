# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project files for restore
COPY services/OrderService/OrderService.csproj services/OrderService/
COPY services/Contracts/Contracts.csproj services/Contracts/

RUN dotnet restore "services/OrderService/OrderService.csproj"

# Copy full source
COPY . .

# Publish OrderService
RUN dotnet publish "services/OrderService/OrderService.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
EXPOSE 8081
ENV ASPNETCORE_URLS=http://+:8081
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "OrderService.dll"]