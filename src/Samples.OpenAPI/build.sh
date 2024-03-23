#!/usr/bin/env bash

rm -rf WebApp

openapi-generator-cli generate -c openapitools/config/webapp.json -t openapitools/templates/grapevine/
openapi-generator-cli generate -c openapitools/config/postman.json
