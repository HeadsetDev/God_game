FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY ["GameAuthAPI.csproj", "."]
RUN dotnet restore "./GameAuthAPI.csproj"

COPY . .
RUN dotnet build "GameAuthAPI.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "GameAuthAPI.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
EXPOSE 80
EXPOSE 443

COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "GameAuthAPI.dll"]