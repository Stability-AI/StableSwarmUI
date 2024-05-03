# StableSwarmUI API Documentation - BasicAPIFeatures

> This is a subset of the API docs, see [/docs/API.md](/docs/API.md) for general info.

Basic general API routes, primarily for users and session handling.

#### Table of Contents:

- HTTP Route [AddNewPreset](#http-route-apiaddnewpreset)
- HTTP Route [ChangeUserSettings](#http-route-apichangeusersettings)
- HTTP Route [DeletePreset](#http-route-apideletepreset)
- HTTP Route [DuplicatePreset](#http-route-apiduplicatepreset)
- HTTP Route [GetCurrentStatus](#http-route-apigetcurrentstatus)
- HTTP Route [GetLanguage](#http-route-apigetlanguage)
- HTTP Route [GetMyUserData](#http-route-apigetmyuserdata)
- HTTP Route [GetNewSession](#http-route-apigetnewsession)
- HTTP Route [GetStabilityAPIKeyStatus](#http-route-apigetstabilityapikeystatus)
- HTTP Route [GetUserSettings](#http-route-apigetusersettings)
- WebSocket Route [InstallConfirmWS](#websocket-route-apiinstallconfirmws)
- HTTP Route [InterruptAll](#http-route-apiinterruptall)
- HTTP Route [ServerDebugMessage](#http-route-apiserverdebugmessage)
- HTTP Route [SetParamEdits](#http-route-apisetparamedits)
- HTTP Route [SetStabilityAPIKey](#http-route-apisetstabilityapikey)

## HTTP Route /API/AddNewPreset

#### Description

(ROUTE DESCRIPTION NOT SET)

#### Parameters

| Name | Type | Description | Default |
| --- | --- | --- | --- |
| title | String | (PARAMETER DESCRIPTION NOT SET) | **(REQUIRED)** |
| description | String | (PARAMETER DESCRIPTION NOT SET) | **(REQUIRED)** |
| raw | JObject | (PARAMETER DESCRIPTION NOT SET) | **(REQUIRED)** |
| preview_image | String | (PARAMETER DESCRIPTION NOT SET) | (null) |
| is_edit | Boolean | (PARAMETER DESCRIPTION NOT SET) | `False` |
| editing | String | (PARAMETER DESCRIPTION NOT SET) | (null) |

#### Return Format

```js
(RETURN INFO NOT SET)
```

## HTTP Route /API/ChangeUserSettings

#### Description

(ROUTE DESCRIPTION NOT SET)

#### Parameters

| Name | Type | Description | Default |
| --- | --- | --- | --- |
| rawData | JObject | (PARAMETER DESCRIPTION NOT SET) | **(REQUIRED)** |

#### Return Format

```js
(RETURN INFO NOT SET)
```

## HTTP Route /API/DeletePreset

#### Description

(ROUTE DESCRIPTION NOT SET)

#### Parameters

| Name | Type | Description | Default |
| --- | --- | --- | --- |
| preset | String | (PARAMETER DESCRIPTION NOT SET) | **(REQUIRED)** |

#### Return Format

```js
(RETURN INFO NOT SET)
```

## HTTP Route /API/DuplicatePreset

#### Description

(ROUTE DESCRIPTION NOT SET)

#### Parameters

| Name | Type | Description | Default |
| --- | --- | --- | --- |
| preset | String | (PARAMETER DESCRIPTION NOT SET) | **(REQUIRED)** |

#### Return Format

```js
(RETURN INFO NOT SET)
```

## HTTP Route /API/GetCurrentStatus

#### Description

(ROUTE DESCRIPTION NOT SET)

#### Parameters

| Name | Type | Description | Default |
| --- | --- | --- | --- |
| do_debug | Boolean | (PARAMETER DESCRIPTION NOT SET) | `False` |

#### Return Format

```js
(RETURN INFO NOT SET)
```

## HTTP Route /API/GetLanguage

#### Description

(ROUTE DESCRIPTION NOT SET)

#### Parameters

| Name | Type | Description | Default |
| --- | --- | --- | --- |
| language | String | (PARAMETER DESCRIPTION NOT SET) | **(REQUIRED)** |

#### Return Format

```js
(RETURN INFO NOT SET)
```

## HTTP Route /API/GetMyUserData

#### Description

(ROUTE DESCRIPTION NOT SET)

#### Parameters

**None.**

#### Return Format

```js
(RETURN INFO NOT SET)
```

## HTTP Route /API/GetNewSession

#### Description

(ROUTE DESCRIPTION NOT SET)

#### Parameters

| Name | Type | Description | Default |
| --- | --- | --- | --- |
| context | HttpContext | (PARAMETER DESCRIPTION NOT SET) | **(REQUIRED)** |

#### Return Format

```js
(RETURN INFO NOT SET)
```

## HTTP Route /API/GetStabilityAPIKeyStatus

#### Description

(ROUTE DESCRIPTION NOT SET)

#### Parameters

**None.**

#### Return Format

```js
(RETURN INFO NOT SET)
```

## HTTP Route /API/GetUserSettings

#### Description

(ROUTE DESCRIPTION NOT SET)

#### Parameters

**None.**

#### Return Format

```js
(RETURN INFO NOT SET)
```

## WebSocket Route /API/InstallConfirmWS

#### Description

(ROUTE DESCRIPTION NOT SET)

#### Parameters

| Name | Type | Description | Default |
| --- | --- | --- | --- |
| theme | String | (PARAMETER DESCRIPTION NOT SET) | **(REQUIRED)** |
| installed_for | String | (PARAMETER DESCRIPTION NOT SET) | **(REQUIRED)** |
| backend | String | (PARAMETER DESCRIPTION NOT SET) | **(REQUIRED)** |
| stability_api_key | String | (PARAMETER DESCRIPTION NOT SET) | **(REQUIRED)** |
| models | String | (PARAMETER DESCRIPTION NOT SET) | **(REQUIRED)** |
| install_amd | Boolean | (PARAMETER DESCRIPTION NOT SET) | **(REQUIRED)** |
| language | String | (PARAMETER DESCRIPTION NOT SET) | **(REQUIRED)** |

#### Return Format

```js
(RETURN INFO NOT SET)
```

## HTTP Route /API/InterruptAll

#### Description

(ROUTE DESCRIPTION NOT SET)

#### Parameters

| Name | Type | Description | Default |
| --- | --- | --- | --- |
| other_sessions | Boolean | (PARAMETER DESCRIPTION NOT SET) | `False` |

#### Return Format

```js
(RETURN INFO NOT SET)
```

## HTTP Route /API/ServerDebugMessage

#### Description

(ROUTE DESCRIPTION NOT SET)

#### Parameters

| Name | Type | Description | Default |
| --- | --- | --- | --- |
| message | String | (PARAMETER DESCRIPTION NOT SET) | **(REQUIRED)** |

#### Return Format

```js
(RETURN INFO NOT SET)
```

## HTTP Route /API/SetParamEdits

#### Description

(ROUTE DESCRIPTION NOT SET)

#### Parameters

| Name | Type | Description | Default |
| --- | --- | --- | --- |
| rawData | JObject | (PARAMETER DESCRIPTION NOT SET) | **(REQUIRED)** |

#### Return Format

```js
(RETURN INFO NOT SET)
```

## HTTP Route /API/SetStabilityAPIKey

#### Description

(ROUTE DESCRIPTION NOT SET)

#### Parameters

| Name | Type | Description | Default |
| --- | --- | --- | --- |
| key | String | (PARAMETER DESCRIPTION NOT SET) | **(REQUIRED)** |

#### Return Format

```js
(RETURN INFO NOT SET)
```

