# Slamby API

Slamby introduces Slamby Server (API). Build powerful data management service, store and analyze your data.

## Product Documentation

Check out our [API documentation](https://developers.slamby.com/docs/API).

## Installation with Docker

Slamby API can find on the [Docker Hub](https://hub.docker.com/r/slamby/slamby.api).

### With Docker Compose (recommended)
Because Slamby API has dependencies: Elasticsearch, Redis and Nginx (recommended), the easiest way to use Slamby API is with Docker-Compose.

We made a prepared Docker Compose file for the easy installation.

#### Steps

1. Install Docker on your machine: [Official Docker installation guide](https://docs.docker.com/engine/installation)

2. Install Docker Compose on your machine (minimum 1.9.0 required): [Official Docker Compose installation guide](https://docs.docker.com/compose/install)

3. Download our Docker Compose file
  ```
  $ curl -L "https://github.com/slamby/slamby-api/releases/download/v1.6.0/docker-compose.yml" > docker-compose.yml
  ``` 

4. Compose the containers (run next to the compose file)
  ```
  $ docker-compose -p slamby up -d
  ```

5. Your server is installed. Check that if it's working correctly
  ```
  $ curl localhost

  {
    "Name": "Slamby.API",
    "Version": "1.6.0",
    "InstanceId": "817021ac-cc23-4473-b203-5083c3e7e00e",
    "Information": "https://developers.slamby.com"
  }

  ```

6. Open the setup page in a browser (http://localhost/setup) and follow the orders
    
    During the setup you need to:
    - Request a Slamby License,
    - Copy your Slamby API License,
    - Set the secret (password) for your Slamby API

### With Docker (advanced)

You can use Slamby API server without composing. But Slamby API has prerequisites.
You have to give the settings to the Slamby API server via environment variables (these are like: `SlambyApi__...`). Note that if you run it in a container you have to set the environment variables to the container not to the host.
if you use an operating system in which you can use `:` in the environment variable names than you have to use `:` instead of `__`.


#### Prerequisites

##### Elasticsearch

Slamby API is using Elasticsearch as data storage system. You can use an own instance or cluster. The recommended version is 2.3. It has to be empty (no indices) and it is recommended to install [mapper-attachments plugin](https://github.com/elastic/elasticsearch-mapper-attachments).
Set the elasticsearch url to the `SlambyApi__ElasticSearch__Uris__0` (e.g.: http://elasticsearchserver:9200/).
Or if you have a cluster with multiple endpoints set all the endpoints to the `SlambyApi__ElasticSearch__Uris__0`, `SlambyApi__ElasticSearch__Uris__1`, `SlambyApi__ElasticSearch__Uris__2` etc. environment variables).

##### Redis

Slamby API using Redis for preindexing and for saving some metrics. Set the Redis connection string in the `SlambyApi__Redis__Configuration`.  
You can even disable the usage of Redis if you want, set the are set `SlambyApi__Redis__Enabled` to `false`. (note that in that case, you can't use some features like PRC preindexing).

##### Nginx

Slamby API using dotnet core and Kestrel under the hood. It is recommended to use an nginx of the top of it. We have a preconfigured nginx image in the [dockerhub](https://hub.docker.com/r/slamby/nginx). It is recommended to use this but you can use your own nginx server. 

##### Slamby directory

Create a directory on the host computer for the persistent Slamby API files

#### Installation

Pull the image from docker hub
```
docker pull slamby/slamby.api:1.6.0
```

Run the container with settings
```
docker run -d \
  --name slamby_api \
  -p 5000:5000 \
  -v /yourDataDirectory:/Slamby \
  slamby/slamby.api:1.6.0
```

The Slamby API is using the port 5000 by default, but you can bind it to whatever port you want on your Docker host. 

## Settings

You can override the settings by environment variables.
Please note that if you use an operating system in which you can use `:` in the environment variable names than you have to use `:` instead of `__`.

Here is a list of the most important settings. You can find all the setting in the appsettings.json file.

### `SlambyApi__ApiSecret`

Default value: `s3cr3t` 

This is the secret for your API. You have to use this to authenticate your requests.

### `SlambyApi__BaseUrlPrefix`

It's empty by default. 

If you are using the API behind a reverse proxy, than you have to use this value. Because in that case, the hostname won't be accurate. 
The API will put the http host of the request after it. 

### `ElasticSearch__Uris__NUMBER`

Note that this is an array configuration value. So you have to put 0, 1, 2... instead of the NUMBER. 

There is a default one `ElasticSearch__Uris__0`, with default value: `'http://elasticsearch:9200/'`

### `SlambyApi__Serilog__Output`

Default value: `/Slamby/Logs` 

The output directory of the log files.

### `SlambyApi__Serilog__MinimumLevel`

Default value: `Information` 

The minimum log level.

### `SlambyApi__Redis__Configuration`

Default value: `redis,abortConnect=false,ssl=false,syncTimeout=30000`

The connection string for the Redis server.

### `SlambyApi__Parallel__ConcurrentTasksLimit`

Default value: `0`

The maximum limit of the used threads in each operation. If it's 0 then the API using _core number * 2_ for the best performance.
Tip: you can limit it in each request header also. Check it in the API documentation. 


### `SlambyApi__RequestsLimiting__MaxConcurrentRequests`

Default value: `50`

With this setting you can set up a Maximum Concurrent Request number. If there are more concurrent requests than this number, the API will response with HTTP Status Code 503 (Service Unavailable).

## Issues

We use GitHub issues to track public bugs. Please ensure your description is clear and has sufficient instructions to be able to reproduce the issue.

## Contributing

Please check our contribution guide [here](https://github.com/slamby/slamby-api/blob/master/CONTRIBUTING.md)

## License

This project is licensed under the GNU Affero General Public License version 3.0.

For commercial use please contact us at sales@slamby.com and purchase commercial license.

## Contact

If you have any questions please visit our [community group](https://groups.google.com/forum/#!forum/slamby) or write an email to us at [support@slamby.com](mailto:support@slamby.com)
