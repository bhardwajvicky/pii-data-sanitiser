FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 80

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project files
COPY ["API.csproj", "./"]
COPY ["../contracts/Contracts.csproj", "../contracts/"]
COPY ["../dal/DAL.csproj", "../dal/"]

# Restore dependencies
RUN dotnet restore "API.csproj"
RUN dotnet restore "../contracts/Contracts.csproj"
RUN dotnet restore "../dal/DAL.csproj"

# Copy source code
COPY . .
COPY ["../contracts/", "../contracts/"]
COPY ["../dal/", "../dal/"]

# Build the application
RUN dotnet build "API.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "API.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "API.dll"]
