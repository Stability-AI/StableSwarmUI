# StableSwarmUI API Documentation - GridGeneratorExtension

> This is a subset of the API docs, see [/docs/API.md](/docs/API.md) for general info.

(CLASS DESCRIPTION NOT SET)

#### Table of Contents:

- HTTP Route [GridGenDeleteData](#http-route-apigridgendeletedata)
- HTTP Route [GridGenDoesExist](#http-route-apigridgendoesexist)
- HTTP Route [GridGenGetData](#http-route-apigridgengetdata)
- HTTP Route [GridGenListData](#http-route-apigridgenlistdata)
- WebSocket Route [GridGenRun](#websocket-route-apigridgenrun)
- HTTP Route [GridGenSaveData](#http-route-apigridgensavedata)

## HTTP Route /API/GridGenDeleteData

#### Description

(ROUTE DESCRIPTION NOT SET)

#### Parameters

| Name | Type | Description | Default |
| --- | --- | --- | --- |
| gridName | String | (PARAMETER DESCRIPTION NOT SET) | **(REQUIRED)** |

#### Return Format

```js
(RETURN INFO NOT SET)
```

## HTTP Route /API/GridGenDoesExist

#### Description

(ROUTE DESCRIPTION NOT SET)

#### Parameters

| Name | Type | Description | Default |
| --- | --- | --- | --- |
| folderName | String | (PARAMETER DESCRIPTION NOT SET) | **(REQUIRED)** |

#### Return Format

```js
(RETURN INFO NOT SET)
```

## HTTP Route /API/GridGenGetData

#### Description

(ROUTE DESCRIPTION NOT SET)

#### Parameters

| Name | Type | Description | Default |
| --- | --- | --- | --- |
| gridName | String | (PARAMETER DESCRIPTION NOT SET) | **(REQUIRED)** |

#### Return Format

```js
(RETURN INFO NOT SET)
```

## HTTP Route /API/GridGenListData

#### Description

(ROUTE DESCRIPTION NOT SET)

#### Parameters

**None.**

#### Return Format

```js
(RETURN INFO NOT SET)
```

## WebSocket Route /API/GridGenRun

#### Description

(ROUTE DESCRIPTION NOT SET)

#### Parameters

| Name | Type | Description | Default |
| --- | --- | --- | --- |
| raw | JObject | (PARAMETER DESCRIPTION NOT SET) | **(REQUIRED)** |
| outputFolderName | String | (PARAMETER DESCRIPTION NOT SET) | **(REQUIRED)** |
| doOverwrite | Boolean | (PARAMETER DESCRIPTION NOT SET) | **(REQUIRED)** |
| fastSkip | Boolean | (PARAMETER DESCRIPTION NOT SET) | **(REQUIRED)** |
| generatePage | Boolean | (PARAMETER DESCRIPTION NOT SET) | **(REQUIRED)** |
| publishGenMetadata | Boolean | (PARAMETER DESCRIPTION NOT SET) | **(REQUIRED)** |
| dryRun | Boolean | (PARAMETER DESCRIPTION NOT SET) | **(REQUIRED)** |
| weightOrder | Boolean | (PARAMETER DESCRIPTION NOT SET) | **(REQUIRED)** |
| outputType | String | (PARAMETER DESCRIPTION NOT SET) | **(REQUIRED)** |
| continueOnError | Boolean | (PARAMETER DESCRIPTION NOT SET) | **(REQUIRED)** |
| showOutputs | Boolean | (PARAMETER DESCRIPTION NOT SET) | **(REQUIRED)** |

#### Return Format

```js
(RETURN INFO NOT SET)
```

## HTTP Route /API/GridGenSaveData

#### Description

(ROUTE DESCRIPTION NOT SET)

#### Parameters

| Name | Type | Description | Default |
| --- | --- | --- | --- |
| gridName | String | (PARAMETER DESCRIPTION NOT SET) | **(REQUIRED)** |
| isPublic | Boolean | (PARAMETER DESCRIPTION NOT SET) | **(REQUIRED)** |
| rawData | JObject | (PARAMETER DESCRIPTION NOT SET) | **(REQUIRED)** |

#### Return Format

```js
(RETURN INFO NOT SET)
```

