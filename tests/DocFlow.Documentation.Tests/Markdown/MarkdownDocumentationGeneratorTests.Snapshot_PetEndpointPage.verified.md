# pet

## `POST /pets`

**Operation ID:** `createPet`

Create a pet

### Request Body

- `application/json` → [`Pet`](../domain-model.md#entity-pet)

### Responses

| Status | Content-Type | Schema | Description |
| --- | --- | --- | --- |
| `201` | `application/json` | [`Pet`](../domain-model.md#entity-pet) | Pet created |

## `GET /pets/{petId}`

**Operation ID:** `getPetById`

Get a pet by ID

### Parameters

| Name | In | Type | Required | Description |
| --- | --- | --- | --- | --- |
| `petId` | path | `integer(int64)` | yes |  |

### Responses

| Status | Content-Type | Schema | Description |
| --- | --- | --- | --- |
| `200` | `application/json` | [`Pet`](../domain-model.md#entity-pet) | Pet details |
| `404` | _none_ | _none_ | Pet not found |

## `GET /pets`

**Operation ID:** `listPets`

List all pets

### Responses

| Status | Content-Type | Schema | Description |
| --- | --- | --- | --- |
| `200` | `application/json` | array&lt;[`Pet`](../domain-model.md#entity-pet)&gt; | A list of pets |

