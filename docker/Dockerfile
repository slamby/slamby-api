FROM microsoft/aspnetcore:1.1.0-preview1

COPY ./publish /app
WORKDIR /app
CMD ./scripts/start_resources.sh && dotnet Slamby.API.dll