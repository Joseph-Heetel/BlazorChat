# Admin Api
## Intended Purpose
It is an integral part of the chat application motivation that a custom implementation would allow **deep integration with existing services** and infrastructure. The Admin Api, a HTTP REST Api, is the method chosen here of enabling this integration. It allows direct control of the entire chat application and is required for some crucial tasks, such as creating users, creating and managing channels, creating and managing forms.

## Api Reference
* You can use the Swagger Api viewer to browse the admin api endpoints. Build and launch the chat application, open your browser at [https://localhost:7196](https://localhost:7196) and click `Swagger Ui Api Viewer` on the index page
* You can use [Postman](https://www.postman.com/) and import the [preconfigured collection file]() for example use of endpoints

## Authorization
The Admin Api demonstrates an example authorization scheme using a bearer token (requires environment variables, see [setup.md](./setup.md))