# Core API Specification

Core-API exposes the workflow engine via REST.

---

## REST Endpoints

### Definitions

POST /definitions

Register workflow definition.

```text

{
    "name": "sample",
    "yaml": "workflow yaml"
}

```

---

GET /definitions/{id}

Returns definition metadata.

---

## Workflow Runs

POST /workflows

Start workflow.

```text

{
    "definitionId": "abc123"
}

```

Response

```text

{
    "workflowId": "wf_123"
}

```

---

GET /workflows/{id}

Returns workflow snapshot.

```text

{
    "workflowId": "wf_123",
    "status": "Running",
    "activeStates": ["A"]
}

```

---

POST /workflows/{id}/cancel

Cancels workflow.

---

POST /workflows/{id}/events

Publish event.

```text

{
    "name": "Approve"
}

```

---

GET /workflows/{id}/graph

Returns ExecutionGraph.

```text

{
    "nodes": [],
    "edges": []
}

```

---

## API Flow

```text

Client
↓
Controller
↓
Repository
↓
Engine
↓
Database

```
