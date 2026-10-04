# Contributing

## Changes

1. Create a branch from `main` for each change.
2. Keep the change focused on one feature or fix.
3. Build the mod with the command in the README.
4. Test gameplay changes in a separate r2modman profile.
5. Open a pull request that describes the change and the test results.

Use clear commit messages, such as `fix: prevent game over after a party wipe`.
Do not commit game DLLs, BepInEx libraries, build output, logs, or credentials.
Put compiled ZIP packages in GitHub Releases.

## Multiplayer checks

For changes that affect deaths or stage travel, test a solo host and a host with another connected player.
Check normal teleporter travel and a full party wipe.
Record the game build and mod version with the results.

## Releases

Keep the plugin version and `manifest.json` version equal.
Add the release changes to `CHANGELOG.md`.
Use a version tag such as `v0.2.0` for a published release.

## Assets

The icon uses game artwork.
See `THIRD_PARTY_NOTICES.md` for its source.
