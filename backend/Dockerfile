FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Directory.Build.props ./
COPY SaEkg.sln ./
COPY SaEkg.Api/SaEkg.Api.csproj SaEkg.Api/
COPY SaEkg.Application/SaEkg.Application.csproj SaEkg.Application/
COPY SaEkg.Domain/SaEkg.Domain.csproj SaEkg.Domain/
COPY SaEkg.Infrastructure/SaEkg.Infrastructure.csproj SaEkg.Infrastructure/
COPY SaEkg.Tests.Unit/SaEkg.Tests.Unit.csproj SaEkg.Tests.Unit/
COPY SaEkg.Tests.Integration/SaEkg.Tests.Integration.csproj SaEkg.Tests.Integration/
RUN dotnet restore SaEkg.Api/SaEkg.Api.csproj
COPY . .
RUN dotnet publish SaEkg.Api/SaEkg.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
RUN apt-get -o Acquire::Check-Valid-Until=false -o Acquire::Check-Date=false update \
    && apt-get -o Acquire::Check-Valid-Until=false -o Acquire::Check-Date=false install -y --no-install-recommends curl fonts-dejavu-core \
    && rm -rf /var/lib/apt/lists/*
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "SaEkg.Api.dll"]
