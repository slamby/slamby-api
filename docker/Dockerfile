FROM microsoft/aspnetcore:1.1.0-preview1

COPY ./publish /app

CMD /app/scripts/start_resources.sh && dotnet /app/Slamby.API.dll