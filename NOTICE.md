# Third-party notices

Prizma is an independent project. Its unified engine is original C#/.NET code maintained in this repository; it does not redistribute or execute `goodbyedpi.exe`, `winws2.exe`, the zapret Lua runtime, or Cygwin.

The packet processing design was developed with reference to:

- GoodbyeDPI and GoodbyeDPI-Turkey, especially the native fragmentation, reverse fragment ordering, low-TTL fake packet, and DNS redirection behavior. GoodbyeDPI is Copyright © ValdikSS and contributors and is licensed under Apache-2.0. The pinned Turkey reference is tag `release-0.2.3rc3-turkey`, commit `02fee64e1e44759b38aa4b05a46f8bcedaa3bec8`.
- bol-van/zapret2, especially TLS/HTTP classification and `multisplit` / `multidisorder` strategy concepts. zapret2 is Copyright © 2016–2026 bol-van and is licensed under MIT. The upstream repository remains the base history of this fork.
- hufrea/byedpi and Flowseal/zapret-discord-youtube were reviewed for SNI-relative split, host-scoped strategy and Windows QUIC fallback behavior. No source or executable from either project is redistributed.
- Cloudflare's public RFC 8484 wire-format DNS-over-HTTPS API is used by default. Cloudflare code is not embedded; HTTPS certificate name and chain validation remain enabled.

Prizma's C# implementation, command model, safety checks, tests, and UI are project-specific changes. Third-party source files are not copied wholesale into `src/Prizma.Engine`.

Release packages include unmodified `WinDivert.dll` and `WinDivert64.sys` from official WinDivert `v2.2.2-A`. WinDivert is Copyright © basil00 and is dynamically used under the LGPL Version 3 option. Its original license is shipped as `engine/LICENSE-WinDivert.txt`, allowing replacement of the dynamic library.

Source references:

- https://github.com/cagritaskn/GoodbyeDPI-Turkey
- https://github.com/ValdikSS/GoodbyeDPI
- https://github.com/bol-van/zapret2
- https://github.com/basil00/WinDivert
- https://developers.cloudflare.com/1.1.1.1/encryption/dns-over-https/make-api-requests/dns-wireformat/
