# Native port development

This directory currently contains the **shared-core integration harness**, not a
Linux/macOS desktop application. Do not package it as a TokenDock release.

The production Windows app and this harness compile the same token readers,
read-only SQLite adapter, preferences and custom-source implementation. SQLite is
resolved to the system library on each OS. Source paths are case-sensitive on
Unix. RAM uses `/proc/meminfo` (`MemAvailable`) on Linux and Mach/sysctl on macOS.
The macOS available value is a conservative free-plus-inactive estimate, not an
Activity Monitor memory-pressure reading. Speculative pages are not double counted.

```sh
python portable/create_fixture.py artifacts/fixture.db
dotnet run --project portable -c Release -- --check artifacts/fixture.db
```

Use a new fixture filename on repeat runs. The generator refuses to overwrite a
file. Tests never start source watchers on user directories, touch the clipboard,
open the camera, or show a desktop window. SQLite checks use synthetic input and
verify that read-only queries cannot modify or create a database.

The `Native port integration` workflow runs these checks on Windows, Linux and
macOS. It intentionally creates no release or downloadable application artifact.

## Desktop implementation

The [desktop application](../desktop/README.md) now implements the surface,
modules and native capture adapters. This directory remains only the shared-core
harness. See its README for implemented behavior, tested scope and unresolved
platform validation; neither executable is a supported Linux/macOS release yet.

The release gate in [PLATFORMS.md](../docs/PLATFORMS.md) remains closed.
