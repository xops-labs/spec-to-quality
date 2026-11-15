# Multi-stage build: frontend -> backend publish -> runtime. Context: this directory (reference-app/).
FROM node:24 AS web
WORKDIR /src/frontend
COPY frontend/package*.json ./
RUN npm ci
COPY frontend/ ./
# vite writes to ../backend/src/IncidentApp.Api/wwwroot
COPY backend/src/IncidentApp.Api/ /src/backend/src/IncidentApp.Api/
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS api
WORKDIR /src
COPY backend/ backend/
COPY --from=web /src/backend/src/IncidentApp.Api/wwwroot backend/src/IncidentApp.Api/wwwroot
RUN dotnet publish backend/src/IncidentApp.Api -c Release -o /out

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=api /out .
ENV ASPNETCORE_URLS=http://0.0.0.0:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "IncidentApp.Api.dll"]
