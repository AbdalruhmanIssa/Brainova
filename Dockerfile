# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project files
COPY ["Brainova.PL/Brainova.PL.csproj", "Brainova.PL/"]
COPY ["Brainova.BLL/Brainova.BLL.csproj", "Brainova.BLL/"]
COPY ["Brainova.DAL/Brainova.DAL.csproj", "Brainova.DAL/"]

# Restore dependencies
RUN dotnet restore "Brainova.PL/Brainova.PL.csproj"

# Copy all source code
COPY . .

# Build the application
RUN dotnet build "Brainova.PL/Brainova.PL.csproj" -c Release -o /app/build

# Publish stage
FROM build AS publish
RUN dotnet publish "Brainova.PL/Brainova.PL.csproj" -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=publish /app/publish .

# Set environment to production
ENV ASPNETCORE_ENVIRONMENT=Production

# Listen on port from environment variable
EXPOSE 8080

# Run the application
ENTRYPOINT ["dotnet", "Brainova.PL.dll"]
