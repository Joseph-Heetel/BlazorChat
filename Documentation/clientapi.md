# Client -> Server Communication via REST Api
All data flows where the client requests, manipulates or removes a server-side object is implemented via a REST Api.
The Api can be explored via swagger (linked from the index page). Please be aware that swagger does not understand custom Json converters, ItemIds in particular.
## Endpoint Procedures
The order of operations in endpoint implementations always follows the same principles:
1. Check authentication (very cheap)
1. Validate input (very cheap)
1. Validate resource access (expensive)
1. Manipulate data (very expensive)
1. Return a result

If any of the above fail the implementation immediately quits execution of the endpoint and returns an appropriate code. This approach minimizes area of attack, defers more expensive tasks as long as possible and reduces chance of data inconsistencies.
## Common Responses
* `200 Ok`
* `400 BadRequest` is returned whenever an endpoint is invoked with malformed input data
* `403 Unauthorized` is returned whenever an endpoint is invoked without necessary authorization
* `404 Notfound` is returned in any of these cases:
    * The endpoint does not exist
    * The requested resource does not exist
    * The authorized user cannot access the requested resource (same response as if resource does not exist to prevent abuse)
* `415 Unsupported Media Type` is returned whenever a file upload endpoint is invoked with incompatible media type
* `500 Internal Server Error` is returned whenever a server internal data manipulation fails (despite input data passing validation)
* `503 Service Unavailable` is returned when a server endpoint is invoked which was disabled (usually due to missing environment variables containing connection strings etc.)
