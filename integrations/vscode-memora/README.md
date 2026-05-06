# Memora Review Inbox Extension

This extension adds a VS Code and Cursor tree view for Memora reviewable artifacts. It reads from the local Memora OpenAPI companion service and does not treat draft or proposed artifacts as approved truth.

## Configuration

- `memora.apiBaseUrl`: local Memora API base URL, defaulting to `http://127.0.0.1:5081`.
- `memora.projectId`: Memora project id to inspect.

## Commands

- `Memora Review Inbox: Refresh Memora Review Inbox`
- `Memora Review Inbox: Open Memora Artifact Preview`
- `Memora Review Inbox: Open Memora Artifact File`
- `Memora Review Inbox: Approve Memora Artifact`
- `Memora Review Inbox: Reject Memora Artifact`

## Behavior

The inbox lists artifacts returned by:

- `GET /api/projects/{projectId}/review/inbox`
- `GET /api/projects/{projectId}/review/preview?path={relativePath}`
- `POST /api/projects/{projectId}/review/decisions`

Previews are read-only markdown documents built from the API response. Opening the source file opens the filesystem artifact that remains the canonical persistence boundary. Approval and rejection commands post decisions to Memora so lifecycle validation and filesystem persistence stay in the governed service path.
