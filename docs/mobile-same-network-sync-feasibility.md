# Same-Network Sync Feasibility For Mobile Packets

## Purpose

This document evaluates whether Memora should add a same-network sync path
for mobile contribution packets, in addition to the M13-05 shared-folder
bridge.

It is a design spike. No sync implementation is included or proposed for
Milestone 13. The recommendation is to defer.

## Scope

In scope:

- evaluating candidate same-network transports that could move a packet
  from a mobile device to the Memora desktop without a hosted backend or
  third-party sync tool
- documenting the trust, discovery, and conflict concerns each candidate
  raises
- comparing the candidate transports to the shared-folder bridge already
  documented in `docs/mobile-nextcloud-transfer.md`
- making an explicit defer-or-pursue recommendation

Out of scope:

- building any same-network sync transport
- changing the packet format, the capture surface, or the desktop importer
- replacing the shared-folder bridge

## Candidate Transports

### 1. mDNS Discovery Plus Local HTTP POST

Memora desktop advertises a `_memora-mobile-inbox._tcp` service on the
local network via mDNS. The mobile device discovers the service and
performs an HTTP `POST` of the packet markdown to the advertised endpoint.

Strengths:

- works on a typical home or office LAN with no extra infrastructure
- transport is a one-shot upload, not a continuous sync
- the desktop side is the only listener, matching Memora's local-first
  posture

Concerns:

- Memora desktop now runs a network listener, even if bound to LAN
  interfaces. That is a new attack surface.
- authentication on first contact is unsolved: a paired-token model adds
  state, scope, and rotation complexity well beyond a "tiny" feature
- multiple Memora instances on the same LAN need disambiguation; mDNS
  service names collide silently
- many corporate networks block multicast or isolate clients from each
  other, breaking discovery without telling the operator
- packet boundary blur: the operator may forget which Memora project they
  are sending to

### 2. WebRTC Data Channel Over LAN

Mobile and desktop establish a peer-to-peer WebRTC data channel after a
shared signaling exchange (QR code, manually entered code, or a third-party
signaling service).

Strengths:

- the protocol is mature and widely understood
- the data channel is encrypted by default
- no listening HTTP server required on the desktop in the simplest mode

Concerns:

- WebRTC without a hosted signaling service still needs a manual exchange
  step. Manual codes are usability friction on a small screen.
- with a hosted signaling service, the workflow stops being backend-free.
- TURN servers are often required when STUN cannot punch through corporate
  NATs. That re-introduces hosted infrastructure.
- both ends now have a WebRTC stack to operate. WebRTC in MAUI Android and
  in a .NET desktop runtime is non-trivial dependency surface and platform
  glue.

### 3. QR Code Single-Shot Handoff

Mobile renders a QR code containing the packet content (or a URL plus a
short-lived key). Desktop scans the code with a camera (or pastes the URL
into a local form) to ingest the packet.

Strengths:

- visually clear handoff with no network listener
- bypasses corporate NAT, mDNS, and signaling concerns entirely
- naturally one-packet-at-a-time, which matches the M13 "mobile capture
  MVP" mindset

Concerns:

- QR payload size: a typical packet exceeds a single comfortable scan. A
  multi-frame animated QR scheme is possible but adds protocol complexity.
- requires the operator to either have a webcam scanning capability on the
  desktop or to manually copy a short URL into the desktop UI.
- this is not really "sync" — it is a manual transfer that happens to use
  a different physical medium than a USB cable.

### 4. Mobile Device-As-Server, Desktop Pulls

The Memora Mobile MAUI app runs a short-lived local web server. Desktop
fetches packets from the mobile device across the LAN.

Strengths:

- avoids opening a long-running listener on the Memora machine
- terminates when the mobile session ends
- the MAUI app could in principle host the listener, so this is technically
  reachable now that the mobile producer is a real app rather than a static
  page

Concerns:

- typical mobile OSes restrict background networking aggressively; this
  channel would be unreliable on iOS in particular and is constrained on
  modern Android battery-saver profiles
- the listener would need its own pairing model, TLS material, and a fresh
  per-session port management story, which is large for an MVP add-on
- the same trust, discovery, and conflict concerns from option 1 apply,
  flipped to the mobile side

### 5. SMB / AFP / NFS Network Share

The operator points the Nextcloud-style synced folder at a network share
served from the Memora machine (or any LAN host). Mobile saves into the
share via a file manager that speaks the share protocol.

Strengths:

- collapses to the M13-05 shared-folder bridge once the share is mounted
- no new Memora-side protocol

Concerns:

- mobile-side SMB/AFP/NFS clients are uncommon and brittle
- the Memora machine now hosts a file share, which has authentication and
  permission concerns of its own
- this is effectively a worse version of the shared-folder bridge with a
  more fragile front end

## Trust Concerns Common To All Candidates

- **Who can submit a packet**: any device on the LAN must be considered
  potentially hostile (guest Wi-Fi, IoT devices, malware on adjacent
  hosts). A pure same-network transport offers little defense without a
  paired secret.
- **Replay and forgery**: without a signed envelope, a packet captured in
  transit can be replayed or modified. The current packet format has no
  signature field. Adding one is a real new design step.
- **Authorization scope**: Memora has no concept of "this device may
  submit packets to this project" yet. Same-network sync makes that
  concept urgent in a way the shared-folder bridge does not (the bridge
  inherits the sync tool's account boundary).
- **Audit**: same-network packets arrive with no provenance beyond the
  envelope. The shared-folder bridge at least leaves the sync tool's
  filesystem history as a corroborating trail.

## Discovery Concerns

- **Multiple Memora instances on one LAN**: a developer with two laptops
  cannot tell mDNS service names apart without per-instance configuration.
- **Operator on guest Wi-Fi or a different VLAN**: discovery silently
  fails. The operator cannot tell whether the desktop is unreachable or
  simply not running.
- **Corporate networks**: multicast and peer-to-peer connections are
  routinely blocked. The feature would work on home networks and fail on
  exactly the networks teams use for actual project work.

## Conflict Concerns

- **Same packet sent twice**: the desktop importer is already idempotent
  by `packet_id` (M13-04), so duplicates are not a correctness problem.
  But a same-network sender that retries on network failure can flood the
  desktop without a backoff or de-duplication step on the mobile side.
- **Multiple senders at once**: the M13-04 importer is single-call. A
  hypothetical receiver service would need its own queue, batching, and
  error reporting. That goes beyond "tiny and low-risk."
- **Out-of-order arrival**: packets carry `created_at`, but Memora
  currently treats them as independent inputs. Same-network sync does not
  introduce new ordering guarantees, but it does invite operators to
  expect them.

## Comparison With The Shared-Folder Bridge

| Dimension | Shared-folder bridge (M13-05) | Same-network sync (this doc) |
|---|---|---|
| Hosted backend required | No | No (varies by candidate) |
| New Memora-side listener | No | Yes (most candidates) |
| Authentication needed beyond OS account | Inherits the sync tool's account | Requires new pairing model |
| Works on corporate networks | Yes (sync tool handles WAN/proxy) | Often no (multicast blocked) |
| Trust boundary | Sync-tool account (visible, audit-friendly) | LAN membership (less defensible) |
| Implementation surface | Zero new Memora code | Non-trivial new transport |
| Failure mode | Sync lag, conflict-copy filenames | Silent discovery failure, replay risk |
| Operator burden | One-time sync setup | Per-session pairing, listener lifecycle |
| Maps onto existing M13-04 importer | Yes, directly | Requires receiver service first |

The shared-folder bridge already covers the Milestone 13 outcome
("mobile notes become structured Memora evidence or proposals through the
desktop review flow") without introducing any of the concerns above.

## Recommendation

**Defer.** No same-network sync transport is small enough or safe enough
to fit the M13-06 "tiny and low-risk" gate. The shared-folder bridge
(M13-05) already satisfies the milestone goal. Building a same-network
transport now would:

- add a new attack surface to a local-first system that has none today
- introduce identity, pairing, and replay concerns the packet format does
  not yet model
- pull in cross-platform networking dependencies that are large compared
  to the rest of M13

The right time to revisit this is after:

- the desktop side has a real review UI for mobile packets (a follow-up
  to M13-04 once M11 review UI matures)
- the packet format gains a signed integrity field if a trust boundary
  between mobile and desktop becomes meaningful
- multi-project resolution is supported on import, so a packet can be
  routed to the right workspace without operator hand-holding

If a future revisit happens, the QR code single-shot handoff (option 3)
is the smallest plausible starting point: it stays one-shot, requires no
network listener, and degrades cleanly to manual copy-paste when scanning
is unavailable. It is also the candidate that least resembles "sync" and
therefore raises the fewest new expectations.

## Out-Of-Scope Acknowledgement

This document does not propose, implement, or schedule any transport. It
does not introduce a packet signature field, a pairing model, or a
receiver service. Memora's mobile contribution path for Milestone 13
remains:

1. mobile capture (M13-02 / M13-03)
2. shared-folder bridge (M13-05)
3. desktop import (M13-04)
4. existing review and approval flow (M11)

Any future same-network work must reopen the trust, discovery, and
conflict questions above before adding a single line of transport code.
