# Mobile Packet Transfer Through A Shared Folder

## Purpose

This document defines a file-based transfer workflow for mobile contribution
packets using a synced folder such as Nextcloud. It is the M13-05 bridge
between mobile capture (M13-02 / M13-03) and desktop import (M13-04).

It is planning material for Milestone 13. The packet shape, the capture
surface, and the import path are all draft and subject to change.

## Scope

In scope:

- a documented end-to-end handoff that moves a mobile packet from the
  capture surface to desktop Memora using a synced folder
- operator setup steps for one reference stack (Nextcloud) and an explicit
  list of equivalent alternates
- a roundtrip validation procedure an operator can run by hand
- explicit limits and known failure modes

Out of scope:

- a hosted Memora backend
- a custom sync engine
- automatic background polling of the shared folder by desktop Memora
- credential handling, account provisioning, or device pairing
- conflict resolution beyond what the sync tool already provides

## Bridge, Not Sync Engine

Memora does not own the sync. The sync tool moves files between devices.
Memora reads packets from a local directory after the sync tool has placed
them there.

Concretely:

- mobile produces a `.md` packet file
- the sync tool stores that file in a folder both devices can see
- desktop Memora consumes the packet file from a local path

This separation matters:

- Memora keeps its filesystem-first, local-first guarantees
- Memora does not become responsible for network reachability, account
  state, or conflict resolution
- the operator can swap Nextcloud for any equivalent tool without changing
  Memora's import surface

## Reference Stack: Nextcloud

The default reference stack is a Nextcloud server (self-hosted or hosted)
with the official mobile and desktop clients installed:

- Nextcloud mobile app on the capture device, signed in to the operator's
  Nextcloud account
- Nextcloud desktop client on the Memora machine, signed in to the same
  account, with a synced root mapped to a known local path (for example
  `C:\Users\<user>\Nextcloud` on Windows or `~/Nextcloud` on macOS / Linux)
- one dedicated folder inside the synced root, used only for mobile packets
  (for example `Nextcloud/MemoraInbox`)

Equivalent alternates that fit the same bridge pattern:

- ownCloud (effectively identical to Nextcloud for this purpose)
- Syncthing (peer-to-peer, no hosted server required)
- iCloud Drive, Google Drive, Dropbox, OneDrive (commercial alternatives)
- any user-managed shared folder that surfaces as a local directory on both
  ends

Anything that exposes the shared content as a normal local directory on
the Memora machine will work. The Memora side does not call the sync tool
directly; it reads from a local path.

## End-To-End Workflow

The flow is deliberately small.

```text
[capture device]                 [shared folder]                 [memora desktop]

mobile capture (M13-02/M13-03)
        |
        v
share .md     ----- save into ----->  MemoraInbox/    --- sync tool mirrors --->  C:\...\Nextcloud\MemoraInbox\
                                                                                          |
                                                                                          v
                                                                          FileBackedMobilePacketImporter
                                                                                          |
                                                                                          v
                                                                          <workspace>/imports/mobile/<packet_id>.json
```

Operator steps:

1. **One-time setup**
   - install and sign in to the sync tool on both devices
   - create a dedicated folder for mobile packets inside the synced root
     (for example `MemoraInbox/`)
   - note the local path of that folder on the Memora machine
2. **Capture on mobile** (M13-02 / M13-03)
   - open the Memora Mobile Capture Android app (`src/Memora.Mobile`)
   - fill the form for the chosen intent
   - tap **Share .md file**
   - in the Android share sheet, pick the Nextcloud app (or the system
     "Save to Files" target pointing at the synced `MemoraInbox/`
     folder); the share sheet's behavior is the only thing that decides
     where the file lands
3. **Sync** (sync tool)
   - the sync tool detects the new file and mirrors it to the Memora
     machine
   - wait for the desktop sync client to show the file as fully synced
4. **Import on desktop** (M13-04)
   - point `FileBackedMobilePacketImporter.ImportFromFile` at the local
     path of the synced packet file and the target Memora workspace
   - the importer parses, validates, and writes
     `<workspace>/imports/mobile/<packet_id>.json` on success, or returns
     a structured `MobilePacketParseDiagnostic` set on failure
5. **Review** (existing flows)
   - the persisted JSON record is non-canonical planning or proposal input
   - it remains review-only until a future Milestone 13 follow-up surfaces
     mobile packets in the existing review queue and a human reviewer
     chooses to promote them through the governed approval flow

The shared folder only moves bytes. Every governance and validation step
still happens on the Memora desktop.

## Optional Convenience: Inbox Folder

Operators may keep a single shared folder named `MemoraInbox/` and never
rename it. Memora imports each packet by `packet_id`, so multiple devices
saving into the same inbox will not collide. Re-importing the same packet
is a no-op (`AlreadyImported`).

A simple operator routine:

- accumulate packets in `MemoraInbox/`
- run desktop import against the folder periodically
- delete or archive imported packet files from `MemoraInbox/` once the
  desktop has confirmed `Imported` or `AlreadyImported`

Memora does not require the source `.md` file to live anywhere specific
after import; the persisted JSON record under
`<workspace>/imports/mobile/` is the durable copy.

## Validation Procedure

End-to-end roundtrip an operator can run by hand:

1. Sign in to the same Nextcloud account on the capture device and the
   Memora machine. Create `Nextcloud/MemoraInbox` on the Memora machine.
2. Wait until both clients show the folder as synced.
3. On the capture device, open the Memora Mobile Capture Android app.
   Choose the `question` intent. Fill out a recognizable title and
   body. Tap **Share .md file**.
4. In the Android share sheet, route the file into `Nextcloud/MemoraInbox`
   (either via the Nextcloud app or the system "Save to Files" target
   pointed at the synced folder).
5. Wait until the Memora-machine sync client shows the file as fully
   synced.
6. On the Memora machine, run a one-off call to
   `FileBackedMobilePacketImporter.ImportFromFile(workspaceRoot, packetPath)`
   where `packetPath` is the local path to the synced file. The
   `MobilePacketImportResult` should report `Outcome = Imported` and a
   non-null `PersistedPath` under
   `<workspace>/imports/mobile/<packet_id>.json`.
7. Open the persisted JSON and confirm:
   - `packetId` matches the mobile envelope
   - `intent = question`
   - `lifecycleTarget = planning_input`
   - `canonical = false`
   - `body` contains the recognizable text from step 3
8. Re-run step 6 with the same file. The result should be
   `AlreadyImported` with the same `PersistedPath` and the existing JSON
   file should be unchanged.
9. Edit the mobile-side `.md` file in `MemoraInbox` to insert
   `status: approved` inside the frontmatter. Re-sync, re-run step 6
   against the edited file. The result should be `Rejected` with a
   `mobile_packet.envelope.reserved_field` diagnostic and no new JSON
   written.

Steps 1, 2, and 5 are sync-tool responsibilities; they are part of the
validation only to confirm the bridge moved the file. Steps 6-9 are the
Memora-side guarantees the bridge protects.

## Limits And Failure Modes

- **Sync lag**: the sync tool may take seconds to minutes to mirror a
  packet, especially on cellular or constrained networks. The operator
  should wait for the desktop sync client to report a synced state before
  attempting import. Memora does not poll or retry the shared folder.
- **Partial uploads**: many sync tools write to a temporary name and
  rename. Importing a packet whose name is still a temporary artifact
  (`.part`, `~`, `.tmp`) is the operator's responsibility to avoid. Memora
  rejects malformed packets cleanly but cannot tell a half-written file
  from a finalized one.
- **Conflicts**: if two devices save different packets with the same
  filename, the sync tool will produce a renamed conflict copy. Memora
  imports by `packet_id`, so the actual filename is not load-bearing.
  Each conflict copy with a distinct `packet_id` imports independently.
- **Trust boundary**: the shared folder is outside Memora. Anything that
  can write to it can submit a packet. Memora still applies safety,
  validation, and lifecycle rules on import, but the operator must accept
  that "shared folder write access" is the practical trust boundary.
- **Multi-project workspaces**: the importer takes one workspace root per
  call. Operators who manage multiple Memora projects should keep
  per-project inbox folders or resolve the target workspace manually
  before import; Memora does not infer the project from the packet's
  `target_project_hint` today.

## Why This Workflow Is Safe Enough For The MVP

- Canonical truth is untouched. The persisted JSON lives in `imports/mobile/`
  and is explicitly non-canonical (`canonical: false`).
- Reserved canonical fields, intent/lifecycle mismatches, unknown intents,
  empty bodies, and unsupported versions are rejected on import with
  structured diagnostics.
- Idempotent re-import means accidental re-syncs do not create duplicates.
- The bridge has no Memora-side service to operate, secure, or update. The
  sync tool stays the only network-facing component.

## Future Work Acknowledged

These are intentionally deferred follow-ups:

- a desktop UI surface that lists `imports/mobile/` entries and lets a
  reviewer convert them into proposed canonical artifacts
- a batch importer that watches a folder and imports new packets
  automatically with explicit operator opt-in
- same-network sync as an alternative to a shared-folder bridge (covered
  by M13-06)
- multi-project resolution from `target_project_hint`

Each of these would extend the bridge without violating the rule that
Memora reads packets from a local path produced by an external sync tool.
