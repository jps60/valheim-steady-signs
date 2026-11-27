# Publishing Steady Signs

Build the checked ZIP locally and publish the GitHub release using the existing release scripts. Publishing a stable GitHub release starts the **Publish storefronts** workflow. It verifies the release SHA-256, sends the original ZIP to Thunderstore, and packages the same DLL under `BepInEx/plugins/SteadySigns/` for Nexus.

## One-time setup

In GitHub, open **Settings → Secrets and variables → Actions**.

| Storefront | Secret | Repository variable |
| --- | --- | --- |
| Thunderstore | `TCLI_AUTH_TOKEN` from the team's service account | `THUNDERSTORE_NAMESPACE` with the exact team namespace |
| Nexus Mods | `NEXUSMODS_API_KEY` from the account with upload access | `NEXUSMODS_FILE_ID` for the main file |

For Nexus, create the Valheim mod page and upload its first main file manually. To generate that ZIP, run **Actions → Publish storefronts → Run workflow** with the existing release tag and destination **verify**, then download the `storefront-packages` artifact and use its `-Nexus.zip`. Find the main file ID under **Advanced** on the Files tab or in **Manage Files**. Disclose the AI-generated code and knot artwork when setting up the page. [Nexus upload action](https://github.com/Nexus-Mods/upload-action#how-to-find-the-file-id--mod-id) · [Nexus AI disclosure policy](https://help.nexusmods.com/article/180-ai-policy-changes-2026)

## Verify or retry an existing release

Run **Publish storefronts** manually with the published tag, for example `v1.2.0`. Choose **verify** to prepare both packages without uploading. Choose **thunderstore** or **nexus** to publish only to that storefront. When retrying a failed job, select only the storefront that still needs the version.

The workflow uses the same Thunderstore CLI approach as [ValheimModTemplate](https://github.com/AugusDogus/ValheimModTemplate/blob/main/.github/workflows/release.yml). The existing local release script supplies the ZIP. If GitHub release creation moves into Actions using `GITHUB_TOKEN`, call the storefront jobs in that workflow or dispatch this one explicitly. [GitHub token behavior](https://docs.github.com/en/actions/concepts/security/github_token#when-github_token-triggers-workflow-runs)
