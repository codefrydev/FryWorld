# FryWorld.Sdk compatibility policy

Every FryWorld app, and every plugin in the store, depends on this SDK, so changing it carries rules.

## What is promised

**Plugins keep working.** A plugin compiled against an earlier release of the SDK with the same major version keeps:

- **compiling** against newer releases without changes (source compatibility), and
- **loading and running** in hosts built with newer releases without being rebuilt (binary compatibility).

**The contract assembly's identity never moves.** `FryWorld.Sdk.dll` has `AssemblyVersion` `1.0.0.0` in every release. Plugins and hosts therefore bind to it without redirects, whatever package version each was built with. `FileVersion` and `InformationalVersion` carry the real release number.

## How it is enforced

`dotnet pack` runs [package validation](https://learn.microsoft.com/dotnet/fundamentals/apicompat/package-validation/overview) against the last published release (`PackageValidationBaselineVersion` in `FryWorld.Sdk.csproj`). Any breaking change fails the pack. That includes a removed or renamed member, a changed signature, a new abstract member on an interface or abstract class, a removed `virtual`, or a parameterless constructor lost to a new overload. After each release, raise the baseline to the version just published.

## Allowed changes

- New types, members, overloads and optional settings.
- New interface members that have a default implementation.
- Bug fixes, including ones that change behaviour plugins could not reasonably rely on (for example, an effect that ran twice now running once).
- Stricter validation that turns silent misbehaviour into a clear error or diagnostic.

## Not allowed without a major version

- Removing or renaming anything public, or changing a signature.
- Adding abstract members to public interfaces or abstract classes.
- Changing the assembly identity.

## Deprecation

To replace an API, add the new one, then mark the old one `[Obsolete("Use X instead.")]`. It keeps working, with a compiler warning that names its replacement. Obsolete APIs are removed only in the next major version and are listed in the changelog when they are deprecated.

## Hosts and plugins in practice

- A plugin that needs a feature added in a later SDK release sets `minHostVersion` in its plugin.json. Older hosts then refuse it with a clear message instead of failing at run time.
- If a plugin is built against an SDK newer than the host's and uses an API the host lacks, it fails to mount with `PluginAbiMismatchException` or a missing-member error, recorded as a diagnostic.
