FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

WORKDIR /App
COPY . ./
RUN dotnet restore SampleCam.sln
RUN dotnet publish SampleCam.sln -c Release -o out

#######################################################
FROM mcr.microsoft.com/dotnet/aspnet:8.0

WORKDIR /App
COPY --from=build /App/out .
ENTRYPOINT ["dotnet", "DotNet.FiberX.SampleCam.dll"]
