WORKDIR /src

COPY SystemTeams.Api.csproj .
RUN dotnet restore SystemTeams.Api.csproj

COPY . .
RUN dotnet publish SystemTeams.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

COPY --from=build /app/publish .

EXPOSE 8080

ENTRYPOINT ["dotnet", "SystemTeams.Api.dll"]
