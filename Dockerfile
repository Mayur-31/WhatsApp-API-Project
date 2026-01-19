# ---------- Frontend build ----------
FROM node:20-alpine AS frontend
WORKDIR /app

# Copy package files
COPY ./DriverConnectApp.Frontend/package*.json ./
RUN npm ci

# Copy frontend source
COPY ./DriverConnectApp.Frontend ./

# Build frontend
RUN npm run build

# ---------- Backend build ----------
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project files
COPY ./DriverConnectApp.API/DriverConnectApp.API.csproj ./DriverConnectApp.API/
COPY ./DriverConnectApp.Infrastructure/DriverConnectApp.Infrastructure.csproj ./DriverConnectApp.Infrastructure/
COPY ./DriverConnectApp.Domain/DriverConnectApp.Domain.csproj ./DriverConnectApp.Domain/

# Restore dependencies
RUN dotnet restore "./DriverConnectApp.API/DriverConnectApp.API.csproj"

# Copy all source code
COPY . .

# Publish backend
WORKDIR "/src/DriverConnectApp.API"
RUN dotnet publish -c Release -o /app/publish

# ---------- Runtime ----------
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
EXPOSE 5001
ENV ASPNETCORE_URLS=http://+:5001
ENV ASPNETCORE_ENVIRONMENT=Production

# Copy backend
COPY --from=build /app/publish .

# Copy frontend (from frontend build stage)
COPY --from=frontend /DriverConnectApp.API/wwwroot ./wwwroot

# Create directories
RUN mkdir -p /app/data /app/uploads

HEALTHCHECK --interval=10s --timeout=5s --retries=5 \
  CMD curl -fs http://localhost:5001/api/health || exit 1


ENTRYPOINT ["dotnet", "DriverConnectApp.API.dll"]