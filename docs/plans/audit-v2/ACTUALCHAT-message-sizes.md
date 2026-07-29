# ActualChat: largest realistic single RPC message

Read-only analysis of `D:\Projects\ActualChat` (working tree as-is, uncommitted work
included; nothing was modified). Cross-checked against Fusion `master` at
`D:\Projects\ActualLab.Fusion`.

## 0. The defaults under review, and how they relate

| Default | Value | Where |
|---|---|---|
| `RpcByteMessageSerializer.Defaults.MaxArgumentDataSize` | `130_000_000` | `D:\Projects\ActualLab.Fusion\src\ActualLab.Rpc\Serialization\RpcByteMessageSerializer.cs:13` |
| `RpcTextMessageSerializer.Defaults.MaxArgumentDataSize` | `130_000_000` | `D:\Projects\ActualLab.Fusion\src\ActualLab.Rpc\Serialization\RpcTextMessageSerializer.cs:13` |
| `RpcWebSocketTransport.Options.MaxMessageSize` | `142_261_962` (derived) | `D:\Projects\ActualLab.Fusion\src\ActualLab.Rpc\WebSockets\RpcWebSocketTransport.cs:25-28` |

`MaxMessageSize = MaxEnvelopeSize + 1 + MaxArgumentDataSize`
(`...\Serialization\RpcTextMessageSerializerV3.cs:28-29`), where
`MaxEnvelopeSize = 259 + 6 * (4096 + 31 * (255 + 65536)) = 12_261_961`
(`RpcTextMessageSerializerV3.cs:16-24`).

Two consequences that matter for the recommendation:

1. **`MaxMessageSize` carries a fixed ~12.26 MB envelope allowance** that does *not*
   shrink when `MaxArgumentDataSize` shrinks. That allowance is itself absurd
   (31 headers × 64 KiB each × 6× JSON expansion) and is a separate lever worth pulling.
2. **The array-pool rounding applies to `MaxMessageSize`, not `MaxArgumentDataSize`.**
   `ArrayPoolBufferCapacity.Round` returns the next power of two
   (`D:\Projects\ActualLab.Fusion\src\ActualLab.Core\Collections\ArrayPoolBufferCapacity.cs:22-28`);
   the WS read buffer grows up to `MaxMessageSize`
   (`RpcWebSocketTransport.cs:96,105,110`). Today: `142_261_962` → **256 MiB**.
   (`130_000_000` alone would only round to 128 MiB.)

## 1. Does ActualChat override any of these? **No.**

`rg 'MaxArgumentDataSize|MaxMessageSize|MaxHeaderSize|MaxFrameSize|RpcLimits'` across
`D:\Projects\ActualChat\src`, all `appsettings*.json` and all deployment YAML returns
**zero** .NET overrides. The only hits are the TypeScript port's `RpcLimits`
(`D:\Projects\ActualChat\src\nodejs\src\actuallab-rpc\rpc-limits.ts`), which carries
*timing* limits only — no size limits. The single RPC-related setting in ActualChat's
serializer/transport wiring is the serialization-format list
(`D:\Projects\ActualChat\src\dotnet\Core\Module\CoreModuleInitializer.cs:47-79`) and
`WebSocketOptions.KeepAliveInterval`
(`D:\Projects\ActualChat\src\dotnet\App.Server\Module\AppServerModule.cs:95-97`).

**ActualChat runs entirely on the Fusion defaults**, for both client↔server and
backend↔backend (everything is WebSocket: `Api.Contracts\Module\ApiContractsModule.cs:104`
`AddWebSocketClient`, `App.Server\Module\AppServerModule.cs:158` `MapRpcWebSocketServer`,
`Core.Server\Rpc\RpcHostBuilder.cs:282` `Rpc.AddWebSocketClient`). The 16 MB
`RpcStreamTransport.Options.MaxFrameSize` guard
(`D:\Projects\ActualLab.Fusion\src\ActualLab.Rpc\Infrastructure\RpcStreamTransport.cs:24`)
is **not** in play — it belongs to the stream/pipe transport, which ActualChat does not use.

## 2. Ranked table — largest realistic single-message payloads

Direction: `C→S` client→server, `S→C` server→client, `B↔B` backend↔backend.

| # | What | Dir | Contract member (file:line) | Est. bytes | Reasoning |
|---|---|---|---|---|---|
| 1 | **`Uploads_Append.Chunk` (`byte[]`)** — the file-upload chunk on the *non-streaming* path | C→S | `D:\Projects\ActualChat\src\dotnet\Api.Contracts\Media\IUploads.cs:41` | **4 194 304** (4 MiB) | `ChunkSizeSelector`: `MinChunkSize = 256 KB` × `MaxChunkSizeMultiplier = 16` (`D:\Projects\ActualChat\src\dotnet\UI\Services\FileUploader\ChunkedFileUploader.cs:127-128`; the `// 8 Mb` comment there is wrong — 16 × 256 KB = 4 MiB). TS twin has the same numbers: `...\UI.Blazor\Services\FileUploads\chunked-file-upload.ts:251-253`. **The server never validates chunk size** (`...\Media.Service\Uploads.cs:56-63`), so this is a *client-side self-limit*, not an enforced bound. |
| 2 | **`IExternalContacts.List` → `ExternalContact[]`** — the whole synced address book in one response | S→C | `D:\Projects\ActualChat\src\dotnet\Api.Contracts\Contacts\IExternalContacts.cs:9` | **~1.5 MB @ 10k contacts; ~7.6 MB @ 50k. UNBOUNDED.** | ~152 B/element (`ExternalContactId` ~88 B + `Version` ~9 B + `HashString` ~50 B + framing). No paging, no `Take`, no server cap (`...\Contacts.Service\ExternalContactsBackend.cs:31-50`). `CacheMode = NoCache`, re-fetched on every sync cycle (`...\UI.Blazor.App\Services\ContactSync.cs:89-91`). Bounded only by how many contacts the device has. |
| 3 | **Video `RpcStream<VideoFrame>` batch** | S→C | `D:\Projects\ActualChat\src\dotnet\Api.Contracts\Streaming\ILiveVideoStreams.cs:8` | **~2–3 MB** worst case | A stream *message* is a batch of ≤ `min(BatchSize, AckAdvance)` items (`...\ActualLab.Rpc\Infrastructure\RpcSharedStream.cs:221,379`; `RpcStream.cs:71` `BatchSize = 64`). ActualChat sets `AckAdvance = 45` (`...\Api\Constants.Video.cs:115`, applied at `...\Streaming.Service\Services\LiveVideoStreams.cs:156`). Top screencast layer = 11 375 kbps (`...\Core\Media\VideoLayerDef.cs:12`; codec `Efficiency ≥ 1` so H.264 is the worst case, `...\Core\Media\VideoCodecDef.cs:9-14`) ⇒ 1.42 MB/s ⇒ 45 frames @ 30 fps = 1.5 s ≈ **2.1 MB** average; a batch straddling a keyframe of the 3 s GOP (`Constants.Video.cs:58`) pushes it to ~3 MB. |
| 4 | **`UploadsBackend_Append.Chunk`** | S→B / B↔B | `D:\Projects\ActualChat\src\dotnet\Media.Contracts\IUploadsBackend.cs:66` | **1 048 576** from the streaming path; **4 MiB pass-through** from `Uploads_Append` | `Constants.Uploads.FlushSize = 1 MB` (`...\Api\Constants.Uploads.cs:24`), used at `...\Media.Service\Uploads.cs:77,100,118`. But `Uploads.OnAppend` forwards the client's `byte[]` verbatim (`...\Media.Service\Uploads.cs:62`), so row 1's 4 MiB propagates onto the backend hop. |
| 5 | **Upload `RpcStream<byte[]>` batch (`AppendStream`)** — the *streaming* upload path | C→S | `D:\Projects\ActualChat\src\dotnet\Api.Contracts\Media\IUploads.cs:19` | **1 048 576** (1 MiB) | 64 items/batch × `Constants.Uploads.SubChunkSize = 16 KB` (`...\Api\Constants.Uploads.cs:11`). Producers: .NET `StreamRpcUploader.cs:69`, TS `stream-file-upload.ts:65`. `AckAdvance = 256` (`Constants.Uploads.cs:15`) does not bind below `BatchSize`. |
| 6 | **Client→server video frame bundle (`PushStream`)** | C→S | `D:\Projects\ActualChat\src\dotnet\Api.Contracts\Streaming\ILiveVideoStreams.cs:61` | **≤ ~1.5 MB** | The TS RPC port has **no** stream batcher (`rg 'batchSize' src/nodejs/src/actuallab-rpc` → no hits), so one `VideoFrameBundle` per message. A 2-layer screencast keyframe bundle at 15 750 kbps aggregate ⇒ a keyframe can be several hundred KB to ~1.5 MB. |
| 7 | **`ExternalContacts_BulkChange` (request + echoed response)** | C→S / S→C | `D:\Projects\ActualChat\src\dotnet\Api.Contracts\Contacts\IExternalContacts.cs:11,16-22` | shipped clients **~48–126 KB**; server-permitted max **~0.5–1.3 MB** | ~480 B/change typical, ~1.26 KB heavy (each phone/email hash is an untruncated SHA-256 base64 = 44 chars, `...\Api\Identifiers\ContactIdExt.cs:16-17`). Client batches at 100 (`...\UI.Blazor.App\Services\ContactSync.cs:10`); the server cap is `MaxChangeCount = 1_000` (`IExternalContacts.cs:21`, enforced `...\Contacts.Service\ExternalContacts.cs:36-38`). **Per-contact size is not validated at all** (no cap on hash count or name length). |
| 8 | **`IChats.GetTile` → `ChatTile`** | S→C, also B↔B | `D:\Projects\ActualChat\src\dotnet\Api.Contracts\Chat\IChats.cs:54` | shipped clients **2–20 KB**; contract-reachable **up to ~55 MB** | Tile layers are 5/20/80/320/1280 (`ServerIdTileStack = Long5To1K`, `...\Api\Constants.TileStacks.cs:9`, bound at `...\Api\Constants.cs:57`). The UI only ever requests layer 0/1 (5 or 20 entries): `ChatUI.Tiles.cs:76,764-772` uses `ViewIdTileStack` (5/20), `ChatEntryReader.cs:8-9,22-23` uses `ReaderIdTileStack.FirstLayer` (5). But `ChatsBackend.GetTile` accepts *any* valid layer including 1280 (`...\Chat.Service\ChatsBackend.cs:318`) and `Chats.GetTile` forwards the client's range unvalidated (`...\Chat.Service\Chats.cs:85`). Worst case within existing constants: 1280 × (unbounded `Content` + 10 attachments (`Constants.cs:151`) + 20 link previews (`Constants.cs:362`) + audio `TimeMap`) ≈ 43 KB/entry ≈ **55 MB**. |
| 9 | **`IDiagnostics.GetMeshDiagInfo` → `MeshDiagInfo`** (admin-only) | S→C, and B↔B per node | `D:\Projects\ActualChat\src\dotnet\Api.Contracts\Chat\IDiagnostics.cs:11,21-29` | **UNBOUNDED. Realistically 10s of MB on a busy prod mesh.** | `DiagnosticsBackendLocal.GetMeshInfo` enumerates `RpcHub.InternalServices.Peers.Values` — i.e. **every connected client WebSocket** — into `RpcPeerDiagInfo[]` (`...\Chat.Service\DiagnosticsBackendLocal.cs:65-82`), each carrying a JSON-serialized handshake + connection blob (`:112-132`) ≈ 600 B–1 KB. `GetMeshInfo(tag, extraLevel: 1)` then fans out to **every online mesh node** and concatenates the results into `Others` (`DiagnosticsBackendLocal.cs:21-49`, entered from `...\Chat.Service\Diagnostics.cs:24`). 5 pods × 5 000 connections × 800 B ≈ **20 MB in one response**. |
| 10 | **`ServerKvas_Set.Value` / `ServerKvas_SetMany.Items`** | C→S | `D:\Projects\ActualChat\src\dotnet\Core\Kvas\IServerKvas.cs:28,39` | **UNBOUNDED** (in practice small settings records) | Arbitrary client-supplied `byte[]` under an arbitrary client-supplied key, no count or size validation anywhere. `IServerKvasBackend.List` (`...\Users.Contracts\IServerKvasBackend.cs:13`) returns every value under a prefix — also uncapped, but only used for guest-key migration (`...\Users.Service\ServerKvas.cs:174`). |
| 11 | **`Chats_UpsertEntry.Text` / `ChatEntry.Content`** | C→S / S→C | `D:\Projects\ActualChat\src\dotnet\Api.Contracts\Chat\IChats.cs:212`; `...\Api\Chat\ChatEntry.cs:53` | **UNBOUNDED** | There is **no max message length anywhere in ActualChat** — not in the contract, not in `Chats.OnUpsertEntry` (which checks only non-emptiness, `...\Chat.Service\Chats.cs:428-431`), not in the DB (`...\Chat.Service\Db\DbChatEntry.cs:15,71` explicitly suppresses the unlimited-string-length warning), and not in the editor (no `maxlength` in `...\UI.Blazor.App\Components\ChatMessageEditor\`). A user pasting a 30 MB text blob produces a 30 MB `Chats_UpsertEntry`, which then multiplies by tile size on every read. |
| 12 | **`IChats.ListMentionableAuthors` → full `Author` + `Avatar` per member** | S→C | `D:\Projects\ActualChat\src\dotnet\Api.Contracts\Chat\IChats.cs:109` | **UNBOUNDED**; ~3–4 MB @ 10k members | ~250–400 B/element; `AuthorsBackend.ListAuthorIdsInternal` has no `Take` (`...\Chat.Service\AuthorsBackend.cs:611-624`). |
| 13 | **`IExternalContactsBackend.ListReferencingContactIds` → `ApiSet<ExternalContactId>`** | B↔B | `D:\Projects\ActualChat\src\dotnet\Contacts.Contracts\IExternalContactsBackend.cs:21` | **UNBOUNDED**; ~88 B × (number of *other* users who have you in their address book) | `...\Contacts.Service\ExternalContactsBackend.cs:108-125` — a raw `ToListAsync` with no cap. Grows with popularity, not with the caller's own data. |
| 14 | **Audio streams** (`RpcStream<AudioFrame>`, `RpcStream<MuxedAudioStreamItem>`, `RpcStream<TranscriptDiff>`) | both | `D:\Projects\ActualChat\src\dotnet\Api.Contracts\Streaming\ILiveAudioStreams.cs:19,25,30,35,50` | **~5 KB per batch** | Opus at `Constants.Audio.Bitrate = 32000` (`...\Api\Constants.Audio.cs:60`) with 20 ms frames = 80 B/frame; batch ≤ 61 (default `AckAdvance`, `...\ActualLab.Rpc\RpcStream.cs:45`). Completely irrelevant to the limit. |
| 15 | Bulk reads with real caps: `GetVisualMediaPeriod`/`GetFilePeriod`/`GetLinkPeriod` (300, `...\Api\Chat\ChatContentPeriod.cs:10`), `ISearch.Find*` (50, `Constants.cs:367`), `INotifications.ListActive` (~64, `Constants.cs:349`), `IChatUsages.GetRecencyList` (100), `ISharedLocations.ListLive` (100, `Constants.cs:198`), `IDiagnostics.GetFlows` (100 default, `...\Api\Flows\FlowsQuery.cs:7`) | S→C | — | **≤ ~180 KB each** | Not a factor. |

### Paths that are HTTP, not RPC — explicitly out of scope

These carry the biggest blobs in the product and are **not** constrained by
`MaxArgumentDataSize` / `MaxMessageSize` at all:

- `POST api/chat-media/{chatId}/upload` — `IFormFile`, `[RequestSizeLimit]` = 2 × `Constants.Attachments.FileSizeLimit`, explicit check at `file.Length > 500 MB`
  (`D:\Projects\ActualChat\src\dotnet\Chat.Service\Controllers\ChatMediaController.cs:15-42`; limit at `...\Api\Constants.cs:149`).
- `POST .../upload-picture` — avatar, 50 MB (`...\Users.Service\Controllers\AvatarPicturesController.cs:17-45`; `Constants.cs:150`).
- `GET api/content/{**blobId}` — all media downloads (`...\Media.Service\Controllers\ContentController.cs:11`).
- `GET api/audio-blob/{chatSid}/{localId}` (`...\Chat.Service\Controllers\AudioBlobController.cs:15`).

Media is **never** returned over RPC as bytes — `IMedia`/`IGifs`/`LinkPreview` carry URLs
only (`...\Api.Contracts\Media\IMedia.cs`, `IGifs.cs`). The one exception worth noting is
`GifItem.BlurPreview` (`...\Api.Contracts\Media\IGifs.cs:26`), a base64 blur placeholder
from Giphy — small, but uncapped × page size.

There is **no RPC contract anywhere in ActualChat that uploads logs, crash dumps, stack
traces or telemetry batches.** MAUI log files go to the OS share sheet via a purely local
interface (`...\UI.Blazor.App\Services\NativeAppSettings\IMauiLogAccessor.cs`).

## 3. Recommended `MaxArgumentDataSize`

### The number: **`16_777_216` (16 MiB)**

Justification chain:

- The largest payload ActualChat is **designed** to send in one message is **4 MiB**
  (`Uploads_Append.Chunk`, row 1). 16 MiB gives **4× headroom** over it.
- The largest payload ActualChat **realistically** sends today is
  `IExternalContacts.List` at **~7.6 MB** for an extreme (50 000-entry) device address
  book — that's what sits just under the recommendation, with **2.2× headroom**.
- Everything in rows 3–8 sits at or below 3 MB.
- 16 MiB also puts the derived `MaxMessageSize` at `16_777_216 + 12_261_962 = 29_039_178`,
  which the array pool rounds to **32 MiB** — an 8× reduction from today's 256 MiB.

### Important: pick the number for the *pool rounding*, not for the argument size

Because `MaxMessageSize = MaxArgumentDataSize + 12_261_962` and the pool rounds
`MaxMessageSize` up to a power of two, the three candidate thresholds behave very
differently from what their names suggest:

| `MaxArgumentDataSize` | derived `MaxMessageSize` | pooled array actually reaches |
|---|---|---|
| `130_000_000` (today) | `142_261_962` | **256 MiB** |
| `65_000_000` | `77_261_962` | **128 MiB** |
| `67_108_864` (64 MiB) | `79_370_826` | **128 MiB** |
| `54_000_000` | `66_261_962` | **128 MiB** ⚠️ (just over 64 MiB) |
| `54_846_901` and below | `≤ 67_108_863` | **64 MiB** ← the exact break-even |
| `16_777_216` (16 MiB) | `29_039_178` | **32 MiB** |
| `21_292_469` and below | `≤ 33_554_431` | **32 MiB** ← break-even |

So **`64 MiB` and `65_000_000` buy you only a 2× reduction** (256 → 128 MiB). Only the
16 MiB candidate meaningfully shrinks the worst-case per-connection allocation.

If a 64 MiB pool ceiling is the target, the right number is **`54_000_000`**, not
`67_108_864`.

Separately: **`MaxEnvelopeSize = 12_261_961` is worth attacking on its own.** It is
`6 × (4096 + 31 × (255 + 65536))` — i.e. 31 headers × 64 KiB each × 6× JSON expansion
(`RpcTextMessageSerializerV3.cs:16-24`). Dropping `RpcByteMessageSerializer.MaxHeaderSize`
from 64 KiB to, say, 4 KiB would take the envelope allowance to ~0.8 MB and let
`MaxMessageSize` track `MaxArgumentDataSize` closely.

### Would anything break at the three candidate thresholds?

| Threshold | Verdict |
|---|---|
| **16 MiB (`16_777_216`)** | **Nothing that ships breaks.** All designed paths (rows 1, 3–8, 14–15) have ≥ 2× headroom. Three residual risks, all of which are latent ActualChat bugs rather than legitimate needs: (a) `IDiagnostics.GetMeshDiagInfo` on a busy production mesh — **this is the one thing likely to actually hit 16 MiB in prod today**, admin-only; (b) `IExternalContacts.List` for a >100 000-contact address book (very unlikely — Android/iOS address books that large are pathological); (c) a user pasting >16 MB of text into one message. |
| **64 MiB (`67_108_864`)** | Nothing breaks, including `GetMeshDiagInfo` on a moderate mesh and a hypothetical 1280-entry `GetTile`. But it only halves the pooled allocation (256 → 128 MiB), so it buys little of the security benefit. |
| **`65_000_000`** | Same as 64 MiB in every respect — same 128 MiB pool bucket, same breakage profile (none). No reason to prefer it over `67_108_864` other than being a round decimal. |

### Recommendation summary

- Set `MaxArgumentDataSize = 16_777_216` in both serializers.
- Before or alongside that, fix `IDiagnostics.GetMeshDiagInfo` (§4, item 1) — it is the
  only shipped path with a plausible chance of exceeding 16 MiB.
- If you want a zero-touch change with no ActualChat-side fix required, use
  **`54_000_000`** (64 MiB pool) as an intermediate step, then tighten to 16 MiB once the
  diagnostics endpoint is capped.

## 4. Latent problems on the ActualChat side — large payloads that should not be single RPC arguments

Ordered by how much they matter to the limit decision.

1. **`IDiagnostics.GetMeshDiagInfo` fans a per-connection list out across the whole mesh.**
   `D:\Projects\ActualChat\src\dotnet\Chat.Service\DiagnosticsBackendLocal.cs:65-82` maps
   *every* RPC peer — which on an API pod means every connected end-user WebSocket — into
   a `RpcPeerDiagInfo` carrying a JSON handshake blob, and `:21-49` then aggregates one
   such object per mesh node into `MeshDiagInfo.Others`
   (`...\Api.Contracts\Chat\IDiagnostics.cs:28`). Response size is
   `O(nodes × connections_per_node)` with no cap. **This should be paged (or filtered to
   mesh peers only, excluding client peers), not returned whole.** It is also the single
   biggest reason to be careful about dropping to 16 MiB.

2. **`Uploads_Append` sends up to 4 MiB as one RPC argument when a perfectly good streaming
   path already exists next to it.** `IUploads.AppendStream`
   (`...\Api.Contracts\Media\IUploads.cs:19`) does exactly the right thing — 16 KB
   sub-chunks through `RpcStream<byte[]>`. `Uploads_Append`
   (`IUploads.cs:41`) is the legacy path and is still wired up for two hosts:
   - non-MAUI Blazor with a `StreamUploadSource` →
     `...\UI.Blazor\Module\BlazorUICoreModule.cs:129-132` registers `StreamUploader` →
     `...\UI.Blazor\Services\FileUploads\StreamUploader.cs:5` → `ChunkedFileUploader`;
   - the iOS share extension →
     `...\App.Maui.IosShareExt\UI\Fusion\Ios\IosHub.cs:31`.

   Retiring `Uploads_Append` in favour of `AppendStream` everywhere would drop the largest
   designed single-message payload from **4 MiB to 1 MiB** and would let the Fusion default
   go materially lower. The server-side chunk-size check is also missing
   (`...\Media.Service\Uploads.cs:56-63`), so a hostile client is not bound by 4 MiB at all.

3. **`IExternalContacts.List` returns the entire address book unpaged.**
   `...\Api.Contracts\Contacts\IExternalContacts.cs:9`, impl
   `...\Contacts.Service\ExternalContactsBackend.cs:31-50`. The *write* side is already
   batched (100 client-side, 1000 server cap) — the read side should be too. Paging it, or
   replacing it with a bucketed digest, removes the second-largest realistic message.

4. **`IConversations.GetTile` accepts an arbitrary `Range<long>` with no tile validation.**
   `...\Api.Contracts\Chat\IConversations.cs:9` →
   `...\Chat.Service\ConversationsBackend.cs:115` calls
   `IdTileStack.LastLayer.GetCoveringTiles(lidTileRange)` directly, without the
   `GetTile`/`AssertIsTile` guard that `ChatsBackend.cs:318` and
   `ConversationsBackend.cs:59` both use. `GetCoveringTiles` is an unbounded
   `while (tile.End < range.End)` loop
   (`...\Core\Mathematics\TileLayer.cs:95-103`), so `(0, long.MaxValue)` is a server-side
   hang/OOM before any response is produced. This is a DoS independent of the message-size
   limit, but it belongs on the same fix list.

5. **No max message length exists.** `Chats_UpsertEntry.Text` /`ChatEntry.Content`
   (`IChats.cs:212`, `ChatEntry.cs:53`) are uncapped at the contract, the validation
   (`Chats.cs:428-431`), the DB (`DbChatEntry.cs:15,71`) and the editor. Whatever
   `MaxArgumentDataSize` ends up being becomes, de facto, ActualChat's maximum message
   length. A explicit cap (a few hundred KB) would be strictly better.

6. **`ServerKvas_Set` / `ServerKvas_SetMany`** (`...\Core\Kvas\IServerKvas.cs:28,39`) accept
   arbitrary client-supplied `byte[]` under arbitrary keys with zero validation — an
   uncontrolled client→server blob channel that happens to only carry small settings today.

7. **Per-contact payload in `ExternalContacts_BulkChange` is unvalidated.** The 1000-change
   cap exists (`...\Contacts.Service\ExternalContacts.cs:36-38`) but nothing limits hashes
   per contact or name length, so 1000 changes can be made arbitrarily large.

8. **`PlaybackQualityInfo.StallNote`** (`...\Api.Contracts\Streaming\Quality\PlaybackQualityInfo.cs:17`)
   is client→server free text; the `Truncate(500)` at
   `...\Streaming.Service\Services\LiveVideoStreams.cs:352` is applied only when *logging*,
   i.e. after deserialization.

## 5. Method notes / what "one message" means here

- For `RpcStream<T>`, a single wire message is a **batch** of up to
  `min(RpcStream.BatchSize, AckAdvance)` items
  (`...\ActualLab.Rpc\Infrastructure\RpcSharedStream.cs:221,256-273,379`;
  `RpcStream.cs:18,45,71` — `BatchSize` default 64, max 1024; `AckAdvance` default 61).
  ActualChat never overrides `BatchSize` (`rg 'BatchSize' src/dotnet` shows only unrelated
  DB/flow batch sizes), and its only `AckAdvance` overrides are
  `Constants.Uploads.RpcStreamAckAdvance = 256` (`...\Api\Constants.Uploads.cs:15`) and
  `Constants.Video.RpcStreamAckAdvance = 45` (`...\Api\Constants.Video.cs:115`).
  The TypeScript RPC port has no batcher at all, so client-originated streams send one
  item per message.
- Video bitrates come from `VideoLayerDef` (`...\Core\Media\VideoLayerDef.cs:5-13`):
  camera 312.5 / 1250 / 4000 kbps, screencast 4375 / 11375 kbps, divided by a codec
  `Efficiency ≥ 1` (`...\Core\Media\VideoCodecDef.cs:9-14`) — so H.264 is the worst case
  and the base numbers are the ceiling.
- The `IChats.GetTile` 55 MB figure in row 8 is *contract-reachable but not exercised by
  any shipped client*, which is why it does not drive the recommendation. If someone ever
  wires the UI to request layer-4 (1280-entry) tiles, the recommendation would have to be
  revisited — or, better, the layer stack exposed over the API should be capped at 320.
