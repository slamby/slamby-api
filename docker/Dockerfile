FROM microsoft/aspnetcore-build:1.1.0-preview1

COPY ./src /app_code

WORKDIR /app_code
RUN ["dotnet", "restore"]

WORKDIR /app_code/Slamby.API

RUN ["dotnet", "publish", "--output", "/app", "--configuration",  "Release"]

WORKDIR /app

RUN ["rm", "-r", "/app_code"]

COPY ./scripts /app/scripts
RUN chmod +x /app/scripts/*.sh

CMD /app/scripts/start_resources.sh && dotnet /app/Slamby.API.dll