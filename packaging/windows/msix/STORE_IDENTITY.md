# FryWorld - Microsoft Store Product Identity

This document records the official Microsoft Partner Center product reservation and package identity for **FryWorld**.

## 1. Product Identity

| Field | Value |
|---|---|
| **Package / Identity / Name** | `CodeFryDev.FryWorld` |
| **Package / Identity / Publisher** | `CN=7E83DE15-E15F-41B6-B068-989D9548D0BF` |
| **Package / Properties / PublisherDisplayName** | `Code Fry Dev` |
| **Package Family Name (PFN)** | `CodeFryDev.FryWorld_ntemjm2faw5zw` |
| **Package SID** | `S-1-15-2-2309085114-2933302328-250174424-3884047107-1361488908-649668362-1376833563` |
| **Store ID** | `9MTL090GWRXG` |

---

## 2. Store Links

- **Store Deep Link (Windows Protocol)**:
  ```
  ms-windows-store://pdp/?productid=9MTL090GWRXG
  ```
- **Web Store URL**:
  ```
  https://apps.microsoft.com/detail/9MTL090GWRXG
  ```

---

## 3. Packaging Configuration

- **Template**: [`packaging/windows/msix/AppxManifest.xml.template`](AppxManifest.xml.template)
- **Store Association**: [`packaging/windows/msix/Package.StoreAssociation.xml`](Package.StoreAssociation.xml)
- **Build Script**: [`packaging/windows/msix/build-msix.ps1`](build-msix.ps1) (defaults to the registered publisher ID `CN=7E83DE15-E15F-41B6-B068-989D9548D0BF`)
- **CI/CD Workflow**: [`.github/workflows/release.yml`](../../../.github/workflows/release.yml) automatically packs and signs the MSIX package on tag pushes.
