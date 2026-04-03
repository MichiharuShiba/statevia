# Definition 仕様

## Statevia Definition YAML

```yaml
version: 1

workflow:
  id: order-workflow
  name: Order Processing Workflow
  description: Example workflow demonstrating all node types

controls:
  cancel:
    event: order.cancelled
    description: Cancel the entire workflow

  resume:
    event: order.resumed
    description: Resume workflow if paused

nodes:
  - id: start
    type: start
    label: Start Order Flow
    description: Entry point
    next: createOrder
    tags:
      - entry
    ui:
      position:
        x: 100
        y: 100

  - id: createOrder
    type: action
    label: Create Order
    description: Persist order to database
    action: order.create

    input:
      orderId: $.input.orderId
      userId: $.input.userId
      items: $.input.items

    next: waitPayment

    onError:
      next: cancelOrder

    tags:
      - order
      - database

    ui:
      position:
        x: 300
        y: 100

  - id: waitPayment
    type: wait
    label: Wait Payment
    description: Waiting for payment completion
    event: payment.completed

    timeout: PT10M

    next: forkFulfillment

    onTimeout:
      next: cancelOrder

    tags:
      - payment
      - async

    ui:
      position:
        x: 500
        y: 100

  - id: forkFulfillment
    type: fork
    label: Parallel Fulfillment
    description: Execute shipping and notification in parallel

    branches:
      - prepareShipment
      - notifyUser

    tags:
      - parallel

    ui:
      position:
        x: 700
        y: 100

  - id: prepareShipment
    type: action
    label: Prepare Shipment
    action: shipping.prepare

    input:
      orderId: $.input.orderId

    next: joinFulfillment

    tags:
      - shipping

    ui:
      position:
        x: 900
        y: 40

  - id: notifyUser
    type: action
    label: Notify User
    action: notification.send

    input:
      userId: $.input.userId
      template: order-confirmation

    next: joinFulfillment

    tags:
      - notification

    ui:
      position:
        x: 900
        y: 160

  - id: joinFulfillment
    type: join
    label: Join Fulfillment
    description: Wait until all branches complete

    mode: all

    next: shipOrder

    tags:
      - synchronization

    ui:
      position:
        x: 1100
        y: 100

  - id: shipOrder
    type: action
    label: Ship Order
    action: shipping.ship

    input:
      orderId: $.input.orderId

    next: endSuccess

    tags:
      - shipping

    ui:
      position:
        x: 1300
        y: 100

  - id: cancelOrder
    type: action
    label: Cancel Order
    description: Cancel order due to failure or timeout
    action: order.cancel

    input:
      orderId: $.input.orderId
      reason: workflow_cancelled

    next: endCancelled

    tags:
      - cancel

    ui:
      position:
        x: 700
        y: 260

  - id: endSuccess
    type: end
    label: Completed Successfully

    tags:
      - end

    ui:
      position:
        x: 1500
        y: 100

  - id: endCancelled
    type: end
    label: Cancelled

    tags:
      - end
      - cancelled

    ui:
      position:
        x: 900
        y: 300

metadata:
  author: statevia-example
  version: 1.0.0
  documentation: Example demonstrating Statevia workflow schema
```

---

## スキーマ定義

```yaml
$schema: "https://json-schema.org/draft/2020-12/schema"
$id: "https://statevia.dev/schemas/workflow-definition.v1.schema.yaml"
title: "Statevia Workflow Definition (v1)"
type: object
additionalProperties: false
required:
  - version
  - workflow
  - nodes

properties:
  version:
    type: integer
    const: 1

  workflow:
    type: object
    additionalProperties: false
    required: [id]
    properties:
      id:
        type: string
        minLength: 1
      name:
        type: string
        minLength: 1
      description:
        type: string

  controls:
    type: object
    additionalProperties: false
    properties:
      cancel:
        $ref: "#/$defs/controlDefinition"
      resume:
        $ref: "#/$defs/controlDefinition"

  nodes:
    type: array
    minItems: 1
    items:
      $ref: "#/$defs/node"

  metadata:
    type: object
    description: "UI/Authoring用の自由領域（エンジンは無視してよい）"
    additionalProperties: true

$defs:
  controlDefinition:
    type: object
    additionalProperties: false
    required: [event]
    properties:
      event:
        type: string
        minLength: 1
      description:
        type: string

  nodeId:
    type: string
    pattern: "^[A-Za-z_][A-Za-z0-9_\\-]*$"

  actionRef:
    type: string
    minLength: 1
    description: "例: order.create / shipping.ship"

  eventType:
    type: string
    minLength: 1
    description: "例: payment.completed / order.cancelled"

  inputPathRefString:
    type: string
    description: >-
      states 形式の input と同一のパス式。$ または $.seg1.seg2（セグメントは英数字と _）。
      ${...} 形式のテンプレは採用しない（現時点で導入予定なし）。詳細は v2-workflow-definition-spec.md §5.1。

  jsonValue:
    description: "任意JSON値"
    anyOf:
      - type: "null"
      - type: boolean
      - type: number
      - type: string
      - type: array
        items:
          $ref: "#/$defs/jsonValue"
      - type: object
        additionalProperties:
          $ref: "#/$defs/jsonValue"

  baseNode:
    type: object
    additionalProperties: false
    required: [id, type]
    properties:
      id:
        $ref: "#/$defs/nodeId"
      type:
        type: string
      label:
        type: string
      description:
        type: string
      tags:
        type: array
        items:
          type: string
      ui:
        type: object
        description: "UI配置など（エンジンは無視してよい）"
        additionalProperties: true

  node:
    oneOf:
      - $ref: "#/$defs/startNode"
      - $ref: "#/$defs/endNode"
      - $ref: "#/$defs/actionNode"
      - $ref: "#/$defs/waitNode"
      - $ref: "#/$defs/forkNode"
      - $ref: "#/$defs/joinNode"

  startNode:
    allOf:
      - $ref: "#/$defs/baseNode"
      - type: object
        additionalProperties: false
        required: [type, next]
        properties:
          type:
            const: start
          next:
            $ref: "#/$defs/nodeId"

  endNode:
    allOf:
      - $ref: "#/$defs/baseNode"
      - type: object
        additionalProperties: false
        required: [type]
        properties:
          type:
            const: end

  actionNode:
    allOf:
      - $ref: "#/$defs/baseNode"
      - type: object
        additionalProperties: false
        required: [type, action]
        properties:
          type:
            const: action
          action:
            $ref: "#/$defs/actionRef"
          input:
            description: "アクション呼び出しの入力。パスは $ / $. のみ（states の input と同一）。${...} は不可"
            anyOf:
              - $ref: "#/$defs/jsonValue"
              - type: object
                additionalProperties:
                  anyOf:
                    - $ref: "#/$defs/jsonValue"
                    - $ref: "#/$defs/inputPathRefString"
          output:
            description: "実行結果の格納先（将来拡張）"
            type: object
            additionalProperties: true
          next:
            $ref: "#/$defs/nodeId"
          onError:
            description: "失敗時の遷移先（将来拡張）"
            type: object
            additionalProperties: false
            properties:
              next:
                $ref: "#/$defs/nodeId"

  waitNode:
    allOf:
      - $ref: "#/$defs/baseNode"
      - type: object
        additionalProperties: false
        required: [type, event]
        properties:
          type:
            const: wait
          event:
            $ref: "#/$defs/eventType"
          timeout:
            description: "ISO 8601 duration 例: PT10M, P1D"
            type: string
            pattern: "^P(?!$)(\\d+Y)?(\\d+M)?(\\d+D)?(T(\\d+H)?(\\d+M)?(\\d+S)?)?$"
          next:
            $ref: "#/$defs/nodeId"
          onTimeout:
            description: "タイムアウト時の遷移先（将来拡張）"
            type: object
            additionalProperties: false
            properties:
              next:
                $ref: "#/$defs/nodeId"

  forkNode:
    allOf:
      - $ref: "#/$defs/baseNode"
      - type: object
        additionalProperties: false
        required: [type, branches]
        properties:
          type:
            const: fork
          branches:
            type: array
            minItems: 2
            items:
              $ref: "#/$defs/nodeId"

  joinNode:
    allOf:
      - $ref: "#/$defs/baseNode"
      - type: object
        additionalProperties: false
        required: [type, next]
        properties:
          type:
            const: join
          next:
            $ref: "#/$defs/nodeId"
          mode:
            description: "合流条件（将来拡張）"
            type: string
            enum: [all]
            default: all
```

---

## Node Type 定義

Stateviaの思想に合わせて
**NodeType は最小5種がよいです**

| type   | 説明         |
| ------ | ------------ |
| start  | 開始         |
| action | 処理実行     |
| wait   | イベント待ち |
| fork   | 並列分岐     |
| join   | 並列合流     |
| end    | 終了         |

---

## Action Node

```yaml
- id: createOrder
  type: action
  action: order.create
  input:
    orderId: $.input.orderId
```

---

## Wait Node

```yaml
- id: paymentWait
  type: wait
  event: payment.completed
```

イベント受信 API

```text
POST /executions/{id}/events
```

```json
{
  "type": "payment.completed",
  "payload": {}
}
```

---

## Fork

```yaml
- id: forkShipping
  type: fork
  branches:
    - preparePackage
    - notifyUser
```

ExecutionGraph

```text
fork
 ├ preparePackage
 └ notifyUser
```

---

## Join

```yaml
- id: joinShipping
  type: join
  next: shipOrder
```

joinは

```text
全branch完了 → next
```

---

## Cancel / Resume

```yaml
controls:
  cancel:
    event: order.cancelled

  resume:
    event: order.resumed
```

Wait状態で

```text
cancel event
↓
Execution Cancelled
```

---

## Engineに渡す内部構造

YAML → Engine では

```ts
type WorkflowDefinition = {
  id: string;
  nodes: Record<string, NodeDefinition>;
};

type NodeDefinition =
  | StartNode
  | ActionNode
  | WaitNode
  | ForkNode
  | JoinNode
  | EndNode;
```
