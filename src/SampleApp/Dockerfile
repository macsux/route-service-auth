FROM mcr.microsoft.com/dotnet/core/sdk:3.1 AS build
WORKDIR src
# copy nuget.config files at solution and project levels
# copy any local nuget sources that are subfolders of the solution
COPY ["SampleApp/SampleApp.csproj", "SampleApp/SampleApp.csproj"]
RUN dotnet restore "SampleApp\SampleApp.csproj"
COPY . .
RUN dotnet msbuild /p:RestorePackages=false /t:PublishLayer /p:PublishDir=/layer/ /p:DockerLayer=All "SampleApp/SampleApp.csproj"
FROM mcr.microsoft.com/dotnet/core/aspnet:3.1 AS run
WORKDIR /app
COPY --from=build /layer/package ./
COPY --from=build /layer/earlypackage ./
COPY --from=build /layer/project ./
COPY --from=build /layer/app ./
ENTRYPOINT ["dotnet", "SampleApp.dll"]
