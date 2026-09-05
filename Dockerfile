# Estágio de Build
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copia a solução e os arquivos de projeto para restaurar as dependências corretamente
COPY *.sln ./
COPY src/Vetly.Domain/*.csproj ./src/Vetly.Domain/
COPY src/Vetly.Application/*.csproj ./src/Vetly.Application/
COPY src/Vetly.Infrastructure/*.csproj ./src/Vetly.Infrastructure/
COPY src/Vetly.API/*.csproj ./src/Vetly.API/
COPY tests/Vetly.UnitTests/*.csproj ./tests/Vetly.UnitTests/
COPY tests/Vetly.IntegrationTests/*.csproj ./tests/Vetly.IntegrationTests/

RUN dotnet restore

# Copia todo o restante do código fonte
COPY . .

# Faz o publish do projeto da API
WORKDIR /src/src/Vetly.API
RUN dotnet publish -c Release -o /app/publish

# Estágio de Runtime (Execução)
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

# Define a porta que o Render vai expor e o comando de inicialização
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "Vetly.API.dll"]