# SystemTeams.Api

API ASP.NET Core minimal (.NET 8) com endpoints HTTP simples e imagem Docker opcional.

## Requisitos

- [.NET SDK 8.0](https://dotnet.microsoft.com/download/dotnet/8.0)

## Executar localmente

Na pasta deste projeto:

```bash
dotnet restore
dotnet run --project SystemTeams.Api.csproj
```

Por omissão o perfil **http** em `Properties/launchSettings.json` usa **http://localhost:5080**.

## Endpoints

| Método | Caminho | Descrição |
|--------|---------|-------------|
| GET | `/health` | Estado da aplicação e timestamp UTC. |
| GET | `/info` | Informação da aplicação, runtime, host e dados de infraestrutura (memória, CPU, disco, cgroup quando disponível). |
| GET | `/*` (outros caminhos) | Resposta JSON placeholder. |

## Docker

Construir a imagem **a partir desta pasta** (`backend`):

```bash
docker build -t systemteams-api .
```

Se estiver na raiz do repositório **SystemTeams** (pasta pai de `backend`):

```bash
docker build -t systemteams-api -f backend/Dockerfile backend
```

Executar:

```bash
docker run --rm -p 8080:8080 systemteams-api
```

A aplicação escuta na porta **8080** no contentor (`ASPNETCORE_URLS=http://+:8080`). Exemplos: `http://localhost:8080/health`, `http://localhost:8080/info`.

## Estrutura relevante

- `App.cs` — definição da aplicação e rotas minimal APIs.
- `SystemTeams.Api.csproj` — projeto Web SDK.
- `Dockerfile` — build multi-stage e runtime `aspnet:8.0`.
- `Properties/launchSettings.json` — URL e ambiente para desenvolvimento local.
