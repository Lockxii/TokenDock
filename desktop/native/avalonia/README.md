# macOS accessibility action bridge

Avalonia 12.1.4, pinned upstream commit `2d79ba55fae1b1e71c69f93a668080c9029e6a31` (MIT).

The external AX regression test found correctly named buttons that accept AXPress but advertise no actions. The patch makes the legacy action list explicit using the existing automation providers and dispatches those same actions through the existing implementations. Disabled peers do not advertise activation actions. Scroll-to-visible is preserved.

`scripts/build-avalonia-macos.sh` builds the pinned upstream native project and copies the library into each macOS publish directory. The normal bundle signing pass signs this library too. Managed packages remain pinned to 12.1.4; upgrades require reviewing/removing this patch. CI tests the actual OS accessibility bridge, not just managed peers. This is not evidence of a complete VoiceOver usability review.
