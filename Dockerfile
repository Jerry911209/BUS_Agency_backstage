# 1. 使用 .NET 10 SDK 進行編譯
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build-env
WORKDIR /app

# 複製專案檔並還原 NuGet 套件
COPY *.csproj ./
RUN dotnet restore

# 複製所有檔案並編譯成 Release 版本
COPY . ./
RUN dotnet publish -c Release -o out

# 2. 使用 .NET 10 Runtime 運行環境
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build-env /app/out .

# 設定 ASP.NET Core 監聽的連接埠
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "BUS_Agency_backstage.dll"]