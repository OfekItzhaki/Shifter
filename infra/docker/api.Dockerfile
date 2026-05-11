FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY *.sln ./
COPY Jobuler.Api/*.csproj Jobuler.Api/
COPY Jobuler.Domain/*.csproj Jobuler.Domain/
COPY Jobuler.Application/*.csproj Jobuler.Application/
COPY Jobuler.Infrastructure/*.csproj Jobuler.Infrastructure/
COPY Jobuler.Tests/*.csproj Jobuler.Tests/

RUN dotnet restore

COPY . .
RUN dotnet publish Jobuler.Api/Jobuler.Api.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
RUN mkdir -p /app/wwwroot/uploads
EXPOSE 8080
ENTRYPOINT ["dotnet", "Jobuler.Api.dll"]
