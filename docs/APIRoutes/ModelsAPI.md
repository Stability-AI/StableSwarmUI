# StableSwarmUI API Documentation - ModelsAPI

> This is a subset of the API docs, see [/docs/API.md](/docs/API.md) for general info.

API routes related to handling models (including loras, wildcards, etc).

#### Table of Contents:

- HTTP Route [DeleteWildcard](#http-route-apideletewildcard)
- HTTP Route [DescribeModel](#http-route-apidescribemodel)
- HTTP Route [EditModelMetadata](#http-route-apieditmodelmetadata)
- HTTP Route [EditWildcard](#http-route-apieditwildcard)
- HTTP Route [ListLoadedModels](#http-route-apilistloadedmodels)
- HTTP Route [ListModels](#http-route-apilistmodels)
- HTTP Route [SelectModel](#http-route-apiselectmodel)
- WebSocket Route [SelectModelWS](#websocket-route-apiselectmodelws)
- HTTP Route [TestPromptFill](#http-route-apitestpromptfill)

## HTTP Route /API/DeleteWildcard

#### Description

Deletes a wildcard file.

#### Parameters

| Name | Type | Description | Default |
| --- | --- | --- | --- |
| card | String | Exact filepath name of the wildcard. | **(REQUIRED)** |

#### Return Format

```js
"success": true
```

## HTTP Route /API/DescribeModel

#### Description

Returns a full description for a single model.

#### Parameters

| Name | Type | Description | Default |
| --- | --- | --- | --- |
| modelName | String | Full filepath name of the model being requested. | **(REQUIRED)** |
| subtype | String | What model sub-type to use, can be eg `LoRA` or `Wildcards` or etc. | `Stable-Diffusion` |

#### Return Format

```js
    "model":
    {
        "name": "namehere",
        "title": "titlehere",
        "author": "authorhere",
        "description": "descriptionhere",
        "preview_image": "data:image/jpg;base64,abc123",
        "loaded": false, // true if any backend has the model loaded currently
        "architecture": "archhere", // model class ID
        "class": "classhere", // user-friendly class name
        "compat_class": "compatclasshere", // compatibility class name
        "standard_width": 1024,
        "standard_height": 1024,
        "license": "licensehere",
        "date": "datehere",
        "usage_hint": "usagehinthere",
        "trigger_phrase": "triggerphrasehere",
        "merged_from": "mergedfromhere",
        "tags": ["tag1", "tag2"],
        "is_safetensors": true,
        "is_negative_embedding": false,
        "local": true // false means remote servers (Swarm-API-Backend) have this model, but this server does not
    }
```

## HTTP Route /API/EditModelMetadata

#### Description

Modifies the metadata of a model. Returns before the file update is necessarily saved.

#### Parameters

| Name | Type | Description | Default |
| --- | --- | --- | --- |
| model | String | Exact filepath name of the model. | **(REQUIRED)** |
| title | String | New model `title` metadata value. | **(REQUIRED)** |
| author | String | New model `author` metadata value. | **(REQUIRED)** |
| type | String | New model `description` metadata value (architecture ID). | **(REQUIRED)** |
| description | String | New model `description` metadata value. | **(REQUIRED)** |
| standard_width | Int32 | New model `standard_width` metadata value. | **(REQUIRED)** |
| standard_height | Int32 | New model `standard_height` metadata value. | **(REQUIRED)** |
| preview_image | String | New model `preview_image` metadata value (image-data-string format, or null to not change). | **(REQUIRED)** |
| usage_hint | String | New model `usage_hint` metadata value. | **(REQUIRED)** |
| date | String | New model `date` metadata value. | **(REQUIRED)** |
| license | String | New model `license` metadata value. | **(REQUIRED)** |
| trigger_phrase | String | New model `trigger_phrase` metadata value. | **(REQUIRED)** |
| prediction_type | String | New model `prediction_type` metadata value. | **(REQUIRED)** |
| tags | String | New model `tags` metadata value (comma-separated list). | **(REQUIRED)** |
| is_negative_embedding | Boolean | New model `is_negative_embedding` metadata value. | `False` |
| subtype | String | The model's sub-type, eg `Stable-Diffusion`, `LoRA`, etc. | `Stable-Diffusion` |

#### Return Format

```js
"success": true
```

## HTTP Route /API/EditWildcard

#### Description

Edits a wildcard file.

#### Parameters

| Name | Type | Description | Default |
| --- | --- | --- | --- |
| card | String | Exact filepath name of the wildcard. | **(REQUIRED)** |
| options | String | Newline-separated string listing of wildcard options. | **(REQUIRED)** |
| preview_image | String | Image-data-string of a preview, or null to not change. | **(REQUIRED)** |

#### Return Format

```js
"success": true
```

## HTTP Route /API/ListLoadedModels

#### Description

Returns a list of currently loaded Stable-Diffusion models (ie at least one backend has it loaded).

#### Parameters

**None.**

#### Return Format

```js
"models":
[
    {
        "name": "namehere",
        // see `DescribeModel` for the full model description
    }
]
```

## HTTP Route /API/ListModels

#### Description

Returns a list of models available on the server within a given folder, with their metadata.

#### Parameters

| Name | Type | Description | Default |
| --- | --- | --- | --- |
| path | String | What folder path to search within. Use empty string for root. | **(REQUIRED)** |
| depth | Int32 | Maximum depth (number of recursive folders) to search. | **(REQUIRED)** |
| subtype | String | Model sub-type - `LoRA`, `Wildcards`, etc. | `Stable-Diffusion` |
| sortBy | String | What to sort the list by - `Name`, `DateCreated`, or `DateModified. | `Name` |
| sortReverse | Boolean | If true, the sorting should be done in reverse. | `False` |

#### Return Format

```js
    "folders": ["folder1", "folder2"],
    "files":
    [
        {
            "name": "namehere",
            // etc., see `DescribeModel` for the full model description
        }
    ]
```

## HTTP Route /API/SelectModel

#### Description

Forcibly loads a model immediately on some or all backends.

#### Parameters

| Name | Type | Description | Default |
| --- | --- | --- | --- |
| model | String | The full filepath of the model to load. | **(REQUIRED)** |
| backendId | String | The ID of a backend to load the model on, or null to load on all. | (null) |

#### Return Format

```js
"success": true
```

## WebSocket Route /API/SelectModelWS

#### Description

Forcibly loads a model immediately on some or all backends, with live status updates over websocket.

#### Parameters

| Name | Type | Description | Default |
| --- | --- | --- | --- |
| model | String | (PARAMETER DESCRIPTION NOT SET) | **(REQUIRED)** |

#### Return Format

```js
"success": true
```

## HTTP Route /API/TestPromptFill

#### Description

Tests how a prompt fills. Useful for testing wildcards, `<random:...`, etc.

#### Parameters

| Name | Type | Description | Default |
| --- | --- | --- | --- |
| prompt | String | The prompt to fill. | **(REQUIRED)** |

#### Return Format

```js
    "result": "your filled prompt"
```

