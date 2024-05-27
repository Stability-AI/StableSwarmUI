# StableSwarmUI API Documentation - T2IAPI

> This is a subset of the API docs, see [/docs/API.md](/docs/API.md) for general info.

API routes for actual text-to-image processing and directly related features.

#### Table of Contents:

- HTTP Route [AddImageToHistory](#http-route-apiaddimagetohistory)
- HTTP Route [DeleteImage](#http-route-apideleteimage)
- HTTP Route [GenerateText2Image](#http-route-apigeneratetext2image)
- WebSocket Route [GenerateText2ImageWS](#websocket-route-apigeneratetext2imagews)
- HTTP Route [ListImages](#http-route-apilistimages)
- HTTP Route [ListT2IParams](#http-route-apilistt2iparams)
- HTTP Route [OpenImageFolder](#http-route-apiopenimagefolder)
- HTTP Route [ToggleImageStarred](#http-route-apitoggleimagestarred)
- HTTP Route [TriggerRefresh](#http-route-apitriggerrefresh)

## HTTP Route /API/AddImageToHistory

#### Description

Takes an image and stores it directly in the user's history.
Behaves identical to GenerateText2Image but never queues a generation.

#### Parameters

| Name | Type | Description | Default |
| --- | --- | --- | --- |
| image | String | Data URL of the image to save. | **(REQUIRED)** |
| rawInput | JObject | Raw mapping of input should contain general T2I parameters (see listing on Generate tab of main interface) to values, eg `{ "prompt": "a photo of a cat", "model": "OfficialStableDiffusion/sd_xl_base_1.0", "steps": 20, ... }`. Note that this is the root raw map, ie all params go on the same level as `images`, `session_id`, etc. | **(REQUIRED)** |

#### Return Format

```js
    "images":
    [
        {
            "image": "View/local/raw/2024-01-02/0304-a photo of a cat-etc-1.png", // the image file path, GET this path to read the image content
            "batch_index": "0", // which image index within the batch this is
            "metadata": "{ ... }" // image metadata string, usually a JSON blob stringified. Not guaranteed to be.
        }
    ]
```

## HTTP Route /API/DeleteImage

#### Description

Delete an image from history.

#### Parameters

| Name | Type | Description | Default |
| --- | --- | --- | --- |
| path | String | The path to the image to delete. | **(REQUIRED)** |

#### Return Format

```js
"success": true
```

## HTTP Route /API/GenerateText2Image

#### Description

Generate images from text prompts, directly as an HTTP route. See the examples in the API docs root page.

#### Parameters

| Name | Type | Description | Default |
| --- | --- | --- | --- |
| images | Int32 | The number of images to generate. | **(REQUIRED)** |
| rawInput | JObject | Raw mapping of input should contain general T2I parameters (see listing on Generate tab of main interface) to values, eg `{ "prompt": "a photo of a cat", "model": "OfficialStableDiffusion/sd_xl_base_1.0", "steps": 20, ... }`. Note that this is the root raw map, ie all params go on the same level as `images`, `session_id`, etc. | **(REQUIRED)** |

#### Return Format

```js
    "images":
    [
        {
            "image": "View/local/raw/2024-01-02/0304-a photo of a cat-etc-1.png", // the image file path, GET this path to read the image content
            "batch_index": "0", // which image index within the batch this is
            "metadata": "{ ... }" // image metadata string, usually a JSON blob stringified. Not guaranteed to be.
        }
    ]
```

## WebSocket Route /API/GenerateText2ImageWS

#### Description

Generate images from text prompts, with WebSocket updates. This is the most important route inside of Swarm.

#### Parameters

| Name | Type | Description | Default |
| --- | --- | --- | --- |
| images | Int32 | The number of images to generate. | **(REQUIRED)** |
| rawInput | JObject | Raw mapping of input should contain general T2I parameters (see listing on Generate tab of main interface) to values, eg `{ "prompt": "a photo of a cat", "model": "OfficialStableDiffusion/sd_xl_base_1.0", "steps": 20, ... }`. Note that this is the root raw map, ie all params go on the same level as `images`, `session_id`, etc. | **(REQUIRED)** |

#### Return Format

```js
    // A status update, contains a full `GetCurrentStatus` response, but pushed actively whenever status changes during generation
    "status":
    {
        "waiting_gens": 1,
        "loading_models": 0,
        "waiting_backends": 1,
        "live_gens": 0
    },
    "backend_status":
    {
        "status": "running",
        "class": "",
        "message": "",
        "any_loading": false
    },
    "supported_features": ["featureid", ...]

    // A progress update
    "gen_progress":
    {
        "batch_index": "0", // which image index within the batch is being updated here
        "overall_percent": 0.1, // eg how many nodes into a workflow graph, as a fraction from 0 to 1
        "current_percent": 0.0, // how far within the current node, as a fraction from 0 to 1
        "preview": "data:image/jpeg;base64,abc123" // a preview image (data-image-url), if available. If there's no preview, this key is omitted.
    }

    // An image generation result
    "image":
    {
        "image": "View/local/raw/2024-01-02/0304-a photo of a cat-etc-1.png", // the image file path, GET this path to read the image content
        "batch_index": "0", // which image index within the batch this is
        "metadata": "{ ... }" // image metadata string, usually a JSON blob stringified. Not guaranteed to be.
    }

    // After image generations, sometimes there are images to discard (eg scoring extension may discard images below a certain score)
    "discard_indices": [0, 1, 2, ...] // batch indices of images to discard, if any
```

## HTTP Route /API/ListImages

#### Description

Gets a list of images in a saved image history folder.

#### Parameters

| Name | Type | Description | Default |
| --- | --- | --- | --- |
| path | String | The folder path to start the listing in. Use an empty string for root. | **(REQUIRED)** |
| depth | Int32 | Maximum depth (number of recursive folders) to search. | **(REQUIRED)** |
| sortBy | String | What to sort the list by - `Name` or `Date`. | `Name` |
| sortReverse | Boolean | If true, the sorting should be done in reverse. | `False` |

#### Return Format

```js
    "folders": ["Folder1", "Folder2"],
    "files":
    [
        {
            "src": "path/to/image.jpg",
            "metadata": "some-metadata" // usually a JSON blob encoded as a string. Not guaranteed.
        }
    ]
```

## HTTP Route /API/ListT2IParams

#### Description

Get a list of available T2I parameters.

#### Parameters

**None.**

#### Return Format

```js
"list":
[
    {
        "name": "Param Name Here",
        "id": "paramidhere",
        "description": "parameter description here",
        "type": "type", // text, integer, etc
        "subtype": "Stable-Diffusion", // can be null
        "default": "default value here",
        "min": 0,
        "max": 10,
        "view_max": 10,
        "step": 1,
        "values": ["value1", "value2"], // or null
        "examples": ["example1", "example2"], // or null
        "visible": true,
        "advanced": false,
        "feature_flag": "flagname", // or null
        "toggleable": true,
        "priority": 0,
        "group":
        {
            "name": "Group Name Here",
            "id": "groupidhere",
            "toggles": true,
            "open": false,
            "priority": 0,
            "description": "group description here",
            "advanced": false,
            "can_shrink": true
        },
        "always_retain": false,
        "do_not_save": false,
        "do_not_preview": false,
        "view_type": "big", // dependent on type
        "extra_hidden": false
    }
],
"models":
{
    "Stable-Diffusion": ["model1", "model2"],
    "LoRA": ["model1", "model2"],
    // etc
},
"wildcards": ["wildcard1", "wildcard2"],
"param_edits": // can be null
{
    // (This is interface-specific data)
}
```

## HTTP Route /API/OpenImageFolder

#### Description

Open an image folder in the file explorer. Used for local users directly.

#### Parameters

| Name | Type | Description | Default |
| --- | --- | --- | --- |
| path | String | The path to the image to show in the image folder. | **(REQUIRED)** |

#### Return Format

```js
"success": true
```

## HTTP Route /API/ToggleImageStarred

#### Description

Toggle whether an image is starred or not.

#### Parameters

| Name | Type | Description | Default |
| --- | --- | --- | --- |
| path | String | The path to the image to star. | **(REQUIRED)** |

#### Return Format

```js
"new_state": true
```

## HTTP Route /API/TriggerRefresh

#### Description

Trigger a refresh of the server's data, returning parameter data.

#### Parameters

| Name | Type | Description | Default |
| --- | --- | --- | --- |
| strong | Boolean | If true, fully refresh everything. If false, just grabs the list of current available parameters (waiting for any pending refreshes first). | `True` |

#### Return Format

```js
    // see `ListT2IParams` for details
    "list": [...],
    "models": [...],
    "wildcards": [...],
    "param_edits": [...]
```

