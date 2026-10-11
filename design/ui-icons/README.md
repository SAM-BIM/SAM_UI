# SAM command icons

`manifest.json` maps every changed command assignment or compatibility resource to a frozen object glyph and operation badge. Existing slot dimensions are preserved. The vendored generator sources are identical to the approved SAM/SAM_UI reference; `vendor-hashes.json` records LF-normalized SHA-256 hashes.

Regenerate using `python tools/build.py` (Pillow and headless Edge required). Review `review/contact_sheet.png` at native scale on light, warning and dark backgrounds. Changes are limited to icon resources and resource tokens; identities, external integration logos and existing command behavior remain unchanged.
