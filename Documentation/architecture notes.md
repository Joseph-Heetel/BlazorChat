# Client
## State Management
The clients state is completely determined by its services, which maintain connection with the server and cache data available to the user. 
* The ChatApiService is responsible of managing the users session (enabling communication with the Api in the first place) and wrapping Http requests to the Api in a simplified interface for other services and components.
* The ChatHubService maintains connection to the SignalR hub and provides C# events for SignalR events
* The ChatStateService maintains all other state information known to the client and provides interfaces for manipulating this state.
    * Collections of Users and Channels known to the account.
    * Pending and active RTC calls.
    * Currently viewed channel.
    * Messages of the currently viewed channel (often a subset of all available messages, within a timewindow).

A common concept used is the Observable (defined in Client/Observable.cs). It is a simple helper object combining a state with an event which is invoked whenever the state changes. This helper class is utilised to keep the state services and the UI components in sync.

## User Interface

Chat and Call Interfaces are entirely separate (if a call is active, the ChatRoot component is replaced by the CallRoot component in the Root component).

Components can be split roughly into two types:
* Viewers which primarily just display data set via parameters and usually don't interface directly with services
* Controlling elements which display more complex information provided by the service interfaces by arranging other controlling elements and viewers

# Server
## Database
### Entity Relation Diagram
![Entity Relation Diagram](./entity-relation-diagram.svg)

### Cosmos DB
Currently the sole database backend service implementation is Azure Cosmos DB. This has some implications on the design of other components:
* **Partition Keys**: Cosmos DB uses a partition key (a secondary non-unique identification component) which azure's servers use to scope queries to smaller sections of the database. 
    * For most tables these partition keys are just a hash of the primary key.
    * The message table is scoped by Channel Id as it allows scoping queries of messages constrained to a channel to be cheaper in RUs (and presumably also quicker).
* **Document Database**: CosmosDB is a document database, which means is able to store almost unstructured data in the form of Json documents. This option has been used to some extend, refer to the entity relation diagram
    * Rather than having a table describing media embeds these descriptions are part of the references instead, forming an object
    * The Form and Form Response tables store generic json in one of their fields
* **Queries**: CosmosDBs SQL dialect ([Transact SQL](https://docs.microsoft.com/de-de/sql/t-sql/language-reference)) has some unique filters. Some queries would have to be rewritten for other backends.

# Client <-> Server communication
* A REST Api allows the client to fetch data from the server
* A SignalR Hub is used for all paths of communications initiated by the server, enabling realtime communication
