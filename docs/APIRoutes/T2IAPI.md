# StableSwarmUI API Documentation - T2IAPI

> This is a subset of the API docs, see [/docs/API.md](/docs/API.md) for general info.

API routes for actual text-to-image processing and directly related features.

#### Table of Contents:

- HTTP Route [DeleteImage](#http-route-apideleteimage)
- HTTP Route [GenerateText2Image](#http-route-apigeneratetext2image)
- WebSocket Route [GenerateText2ImageWS](#websocket-route-apigeneratetext2imagews)
- HTTP Route [ListImages](#http-route-apilistimages)
- HTTP Route [ListT2IParams](#http-route-apilistt2iparams)
- HTTP Route [OpenImageFolder](#http-route-apiopenimagefolder)
- HTTP Route [ToggleImageStarred](#http-route-apitoggleimagestarred)
- HTTP Route [TriggerRefresh](#http-route-apitriggerrefresh)

## HTTP Route /API/DeleteImage

#### Description

(ROUTE DESCRIPTION NOT SET)

#### Parameters

| Name | Type | Description | Default |
| --- | --- | --- | --- |
| path | String | (PARAMETER DESCRIPTION NOT SET) | **(REQUIRED)** |

#### Return Format

```js
(RETURN INFO NOT SET)
```

## HTTP Route /API/GenerateText2Image

#### Description

(ROUTE DESCRIPTION NOT SET)

#### Parameters

| Name | Type | Description | Default |
| --- | --- | --- | --- |
| images | Int32 | (PARAMETER DESCRIPTION NOT SET) | **(REQUIRED)** |
| rawInput | JObject | (PARAMETER DESCRIPTION NOT SET) | **(REQUIRED)** |

#### Return Format

```js
(RETURN INFO NOT SET)
```

## WebSocket Route /API/GenerateText2ImageWS

#### Description

(ROUTE DESCRIPTION NOT SET)

#### Parameters

| Name | Type | Description | Default |
| --- | --- | --- | --- |
| images | Int32 | (PARAMETER DESCRIPTION NOT SET) | **(REQUIRED)** |
| rawInput | JObject | (PARAMETER DESCRIPTION NOT SET) | **(REQUIRED)** |

#### Return Format

```js
(RETURN INFO NOT SET)
```

## HTTP Route /API/ListImages

#### Description

(ROUTE DESCRIPTION NOT SET)

#### Parameters

| Name | Type | Description | Default |
| --- | --- | --- | --- |
| path | String | (PARAMETER DESCRIPTION NOT SET) | **(REQUIRED)** |
| depth | Int32 | (PARAMETER DESCRIPTION NOT SET) | **(REQUIRED)** |
| sortBy | String | (PARAMETER DESCRIPTION NOT SET) | `Name` |
| sortReverse | Boolean | (PARAMETER DESCRIPTION NOT SET) | `False` |

#### Return Format

```js
(RETURN INFO NOT SET)
```

## HTTP Route /API/ListT2IParams

#### Description

(ROUTE DESCRIPTION NOT SET)

#### Parameters

**None.**

#### Return Format

```js
(RETURN INFO NOT SET)
```

## HTTP Route /API/OpenImageFolder

#### Description

(ROUTE DESCRIPTION NOT SET)

#### Parameters

| Name | Type | Description | Default |
| --- | --- | --- | --- |
| path | String | (PARAMETER DESCRIPTION NOT SET) | **(REQUIRED)** |

#### Return Format

```js
(RETURN INFO NOT SET)
```

## HTTP Route /API/ToggleImageStarred

#### Description

(ROUTE DESCRIPTION NOT SET)

#### Parameters

| Name | Type | Description | Default |
| --- | --- | --- | --- |
| path | String | (PARAMETER DESCRIPTION NOT SET) | **(REQUIRED)** |

#### Return Format

```js
(RETURN INFO NOT SET)
```

## HTTP Route /API/TriggerRefresh

#### Description

(ROUTE DESCRIPTION NOT SET)

#### Parameters

| Name | Type | Description | Default |
| --- | --- | --- | --- |
| strong | Boolean | (PARAMETER DESCRIPTION NOT SET) | `True` |

#### Return Format

```js
(RETURN INFO NOT SET)
```

