FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["BanRepPriceCapture.ServiceLayer/BanRepPriceCapture.ServiceLayer.csproj", "BanRepPriceCapture.ServiceLayer/"]
COPY ["BanRepPriceCapture.ApplicationLayer/BanRepPriceCapture.ApplicationLayer.csproj", "BanRepPriceCapture.ApplicationLayer/"]
COPY ["BanRepPriceCapture.InfrastructureLayer/BanRepPriceCapture.InfrastructureLayer.csproj", "BanRepPriceCapture.InfrastructureLayer/"]
COPY ["BanRepPriceCapture.DomainLayer/BanRepPriceCapture.DomainLayer.csproj", "BanRepPriceCapture.DomainLayer/"]
RUN dotnet restore "BanRepPriceCapture.ServiceLayer/BanRepPriceCapture.ServiceLayer.csproj"
COPY . .
WORKDIR "/src/BanRepPriceCapture.ServiceLayer"
RUN dotnet build "./BanRepPriceCapture.ServiceLayer.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./BanRepPriceCapture.ServiceLayer.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "BanRepPriceCapture.ServiceLayer.dll"]
