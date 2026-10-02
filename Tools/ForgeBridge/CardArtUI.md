# Card artwork chooser

The detail view's **异画 · 下载** button opens the right-hand chooser. It lists
Forge printings of the same rules card, six at a time. Browsing fetches only small
thumbnails and caches them in `Application.persistentDataPath/CardArtThumbnails`.

Select a version and choose **下载并设为偏好** (or **使用此版本 · 设为偏好**
when installed). Preferences are per rules-card identity, local to this client,
and saved in PlayerPrefs. All `CardImageLibrary` consumers use them. Clearing the
preference restores the existing default image resolution order and does not
delete downloads or alter deck identities. Failed/cancelled downloads do not set
preferences. Closing the drawer cancels its outstanding downloads.

Settings select normal or large quality for future downloads. Existing images
are preserved. The fallback source is Scryfall's image CDN; an existing
`card-images.json` custom image server is still honored.

Run `python Tools/ForgeBridge/Export-ArtSources.py` after updating the Forge
catalog or local Scryfall bulk cache. It writes exact-printing/name matches to
`Assets/StreamingAssets/CardArtSources.jsonl`, which ships in Unity builds.
The chooser loads this optional index on demand, off the main thread. No API
search requests or full-resolution downloads occur merely from opening it.

## Verification (2026-09-22, Unity Editor)

- Opened Lightning Bolt's chooser: 67 versions, six items/page.
- Selected 2X2/361 for 2ED/162: `TryLoadFullCard` returned the exact selected sprite.
- Preference survived a script/domain reload; clearing it restored the original
  2ED/162 sprite. Serialized deck content was identical before and after.
- Invoked the production download/write path against the real CDN in an isolated
  `.tools/art-ui-validation/downloads` directory: validated JPEG, 80,841 bytes.
- Pre-cancelled download returned cancellation without setting a preference.
- Verified layout screenshot: `Captures/Card-Art-Chooser.png`.
- A standalone Windows player build was not run for this change.
