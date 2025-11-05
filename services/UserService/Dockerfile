# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project files for restore (service + shared contracts)
COPY services/UserService/UserService.csproj services/UserService/
COPY services/Contracts/Contracts.csproj services/Contracts/

# Restore only the needed projects first for better layer caching
RUN dotnet restore "services/UserService/UserService.csproj"

# Copy the rest of the source
COPY . .

# Publish UserService
RUN dotnet publish "services/UserService/UserService.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "UserService.dll"]