# Arena Switch (iPhone)

A small original iPhone app with **Private server** and **Official servers**
buttons. It switches the official App Store Arena app by writing or removing
Arena's `/Documents/services.conf`, the same file `Switch-iPhone-Arena.bat`
manages from the PC. It contains no Arena code or assets.

Status (2026-10-27): written, **not yet compiled** (no Rust or Xcode toolchain on
this PC) and **not tested on a phone**. Whether iOS lets an on-phone app open
Arena's House Arrest service through the loopback VPN is unconfirmed.

## How it works

LocalDevVPN loops the phone's network back to itself, so the app can reach the
phone's own lockdown service at 10.7.0.1 with a pairing file made once on a PC.
The Rust core (`rust/`, using the pinned `idevice` crate) asks House Arrest for
Arena's Documents folder and edits only `services.conf`. It only replaces or
removes a file it wrote itself (environment name "ArcaneForge").

## Build

GitHub Actions workflow `.github/workflows/ios-switch-app.yml` (macOS runner):
Rust static library for `aarch64-apple-ios`, XcodeGen project, unsigned
`xcodebuild`, then `ArcaneSwitch-unsigned.ipa` as a downloadable artifact. Run
it from the Actions tab once the folder is pushed. Nothing here needs secrets.

## Install and use

1. Install SideStore and LocalDevVPN on the iPhone (set a passcode if none).
2. Create a pairing file on the PC with the phone on USB (for example with
   iLoader or jitterbugpair). Treat it like a password: it grants full device
   access. Do not commit or upload it.
3. Install `ArcaneSwitch-unsigned.ipa` through SideStore with your Apple ID
   (free Apple ID apps need refreshing every 7 days, which SideStore does).
4. Open Arena Switch, import the pairing file, enter the server address
   (this PC's Tailscale address), connect LocalDevVPN, tap a button, then fully
   close and reopen Arena.

Connecting in Private mode still needs Tailscale on the phone and
`Host-VPN-Server.bat` running. Arena may refuse the plain-http sign-in address
under iOS App Transport Security; unverified.
