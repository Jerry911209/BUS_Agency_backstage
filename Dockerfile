# 1. 使用 .NET SDK 進行編譯
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build-env
WORKDIR /app

# 複製專案檔並還原 NuGet 套件
COPY *.csproj ./
RUN dotnet restore

# 複製所有檔案並編譯成 Release 版本
COPY . ./
RUN dotnet publish -c Release -o out

# 2. 使用 Runtime 運行環境（縮小映像檔體積）
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build-env /app/out .

# 設定 ASP.NET Core 監聽的連接埠（雲端平台通常預設會給 8080 或 80）
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "BUS_Agency_backstage.dll"]