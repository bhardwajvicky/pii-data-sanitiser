FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 80

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project files
COPY ["Web.csproj", "./"]
COPY ["../contracts/Contracts.csproj", "../contracts/"]

# Restore dependencies
RUN dotnet restore "Web.csproj"
RUN dotnet restore "../contracts/Contracts.csproj"

# Copy source code
COPY . .
COPY ["../contracts/", "../contracts/"]

# Build the application
RUN dotnet build "Web.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Web.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Web.dll"]
