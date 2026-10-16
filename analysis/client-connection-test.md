# Installed client connection test

Tested installed Windows MTG Arena `0.1.14403.1326110` on 2026-10-03.

The supported `MTGA_Data/StreamingAssets/services.conf` override selects a dedicated Local environment. Its WAS login/profile, Doorbell, FrontDoor and business-event URLs select local services. An expiring localhost-only TLS server certificate was installed in this Windows user's trust store; default OS certificate validation and online revocation checking passed without a certificate-validation bypass.

The actual Arena UI displayed the Local environment. A dedicated local test account completed WAS form login and profile retrieval. The protocol service accepted its issued JWT and returned a FrontDoor session. Windows reported an established connection from the MTGA.exe process to `127.0.0.1:9443`; the host received subsequent campaign graph and business-event commands. This is stock-client connection evidence, beyond the separate protocol test harness.

Authentication alone did not load the frontend. The first failure was unsupported `GraphGetGraphDefinitions`. Returning an empty graph list then exposed a distinct wait: `CampaignGraphManager.Initialized` requires a nonempty definition dictionary. Subsequent work adds an explicitly synthetic completed local tutorial graph and the recovered startup/deck contracts, rather than fetching production account state.

`Restore-OfficialClient.ps1` restores the saved configuration and removes only the newly installed localhost certificate. `-RestorePreferences` also imports the preserved pre-test Arena preferences. Close Arena before using the restore command. Account and deck data in the local backend are preserved.

Deck-builder rendering, import and save require separate end-to-end verification; connection evidence does not establish those behaviors or playable matches.
