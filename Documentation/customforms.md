# Forms
* Hierarchical definition of values
* Inputs can be of various types
    * Numbers
    * Strings
    * Selection
    * Toggles
* Inputs can be required or optional (similar concept as Nullability)
* Various options for input validation
* AdminApi can create custom forms
* AdminApi can attach requests targeted at a single user to messages
* AdminApi can access and filter responses recorded

# JSON Schema
[JSON Schema](https://json-schema.org/) was chosen as the format for defining both the layout and the validation of forms. This is by no means a new idea, similar implementations exist for angular and other UI frameworks. 

<details>
<summary>Here is an example of how it can be configured</summary>

```json
{
    "title": "Forms Demonstrator",
    "properties": {
        "strings": {
            "title": "String Values",
            "properties": {
                "lengths": {
                    "title": "Length Constrained",
                    "description": "Maximum and minimum length",
                    "type": "string",
                    "minLength": 3,
                    "maxLength": 8
                },
                "regexpattern": {
                    "title": "Pattern Constrained",
                    "description": "IP adresses match this regex",
                    "type": "string",
                    "pattern": "^(([0-9]|[1-9][0-9]|1[0-9]{2}|2[0-4][0-9]|25[0-5]).){3}([0-9]|[1-9][0-9]|1[0-9]{2}|2[0-4][0-9]|25[0-5])$"
                }
            }
        },
        "numbers": {
            "title": "Number Values",
            "properties": {
                "integer": {
                    "title": "Integer",
                    "description": "Allows only integers to be entered",
                    "type": "integer"
                },
                "minMax": {
                    "title": "Minima / Maxima",
                    "description": "Inclusive and exlusive maxima can be set",
                    "minimum": -0.5,
                    "exclusiveMaximum": 0.78,
                    "type": "number"
                },
                "multi": {
                    "title": "Multiple Of",
                    "description": "Allows only positive multiples of a certain number",
                    "multipleOf": 0.3,
                    "type": "number"
                }
            }
        },
        "selectors":{
            "title": "Selectors and Toggles",
            "properties":{
                "toggle":{
                    "title": "Toggle (boolean)",
                    "description": "Just a toggle",
                    "type":"boolean"
                },
                "optionaltoggle":{
                    "title": "Toggle (Optional boolean)",
                    "description": "Just a toggle",
                    "type": ["boolean", "null"]
                },
                "enum":{
                    "title":"Dropdown Selector",
                    "enum": ["Option 1", "Option 2", "Option 3"]
                }
            }
        },
        "requireds":{
            "title": "Required versus optional fields",
            "properties": {
                "optional": {
                    "title": "Optional Field",
                    "type": ["string", "null"]
                },
                "required":{
                    "title": "Required Field",
                    "type": "string"
                }
            },
            "required": ["required"]
        }
    },
    "required": ["strings", "numbers", "selectors", "requireds"]
}
```

</details>

# Intended Usage

* Setup
    1. If not already on the server, AdminApi is used to upload the form schema
    1. AdminApi is used to send a new message into a relevant channel (create if necessary)
    1. AdminApi is used to configure and attach a form request to the message
    1. The users client is notified that the message has been updated
* Response
    1. The user fills the form and it is validated on the client side
    1. The users client submits the response
    1. The server asserts that the client is allowed to submit a response to the request
    1. The server saves the response
* Evaluation
    1. The AdminApi is used to collect the response(s) filtered by request or form
    1. A generic json schema validator and/or custom tools are used to perform validation of the recorded answers

# Client Side Schema Visualization

See `Client/Components/Forms` folder

A custom data structure for parsing json schemas to constraints and meta information was implemented, as given implementations were disqualified for not using .NETs built in JSON implementation or for being overbuilt and restrictive in their access to underlying schema data. Some notes:

* `JsonSchemaNode` class represents a single property or property object as defined by the schema. It allows validating values against constraints and holds meta data defined in the json schema.
* `FormObjectViewer.razor` iterates a schema node and instantiates input components for the child schema nodes listed. Display of the objectviewers content can be toggled.
* All form input components inherit functionality from `FormValueBase`, which provides functionality for writing, reading and validating response values saved in a generic `JsonNode` (`System.Text.Json`) tree