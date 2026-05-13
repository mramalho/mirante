# Se o terminal estiver em backend/ (onde está este Dockerfile):
#   docker build -t systemteams-api .
# Não use "backend" como contexto aqui — não existe subpasta backend dentro de backend.
#
# Se o terminal estiver na raiz SystemTeams/ (pasta que contém backend/):
#   docker build -t systemteams-api -f backend/Dockerfile backend
#
# Executar o container (qualquer um dos builds acima):
#   docker run --rm -p 8080:8080 systemteams-api

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
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
