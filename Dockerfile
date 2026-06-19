FROM node:20-alpine AS frontend-build
WORKDIR /src

COPY frontend/package*.json ./frontend/
WORKDIR /src/frontend
RUN npm ci

WORKDIR /src
COPY frontend ./frontend
WORKDIR /src/frontend
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS backend-build
WORKDIR /src

COPY backend/FrpcManager.Api/*.csproj ./backend/FrpcManager.Api/
RUN dotnet restore ./backend/FrpcManager.Api/FrpcManager.Api.csproj

COPY backend ./backend
COPY --from=frontend-build /src/frontend/dist ./backend/FrpcManager.Api/wwwroot
RUN dotnet publish ./backend/FrpcManager.Api/FrpcManager.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

ENV ASPNETCORE_ENVIRONMENT=Production
ENV Frpc__WebServerAddr=host.docker.internal
ENV Frpc__WebServerPort=7400
ENV Jwt__KeyFile=/app/data/jwt.key

COPY --from=backend-build /app/publish ./

EXPOSE 6887
EXPOSE 6888

ENTRYPOINT ["dotnet", "FrpcManager.Api.dll"]
