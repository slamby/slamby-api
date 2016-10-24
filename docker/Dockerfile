FROM dockerhub.slamby.com/slambybase:1

COPY ./src /app_code

WORKDIR /app_code
RUN ["dnu", "restore","--runtime=ubuntu.14.04-x64"]

WORKDIR /app_code/Slamby.API

RUN ["dnu", "publish", "--out", "/app", "--configuration",  "Release", "--no-source"]

RUN apt-get update
RUN apt-get -y install cron

WORKDIR /app
RUN ls

RUN ["rm", "-r", "/app_code"]

COPY ./scripts /app/scripts

CMD /app/scripts/start_resources.sh && /app/approot/prod-server