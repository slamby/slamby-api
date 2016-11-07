#!bin/bash
set -e
dotnet restore
rm -rf ./publish
dotnet publish ./src/Slamby.API/project.json -c Release -o ./publish
cp -r ./scripts ./publish/
chmod +x ./publish/scripts/*.sh