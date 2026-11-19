//! Switch the official Arena app between Wizards' servers and the private server,
//! on the iPhone itself.
//!
//! The phone's own services are reached through a loopback VPN (for example
//! LocalDevVPN at 10.7.0.1) using a pairing file made once on a PC, over Apple's
//! remote-pairing tunnel. House Arrest vends Arena's Documents folder; only
//! /Documents/services.conf is written or removed, and only a file this tool
//! wrote is replaced or removed. Mirrors tools/iphone_arena_switch.py.

use std::ffi::{c_char, CStr};
use std::net::{IpAddr, SocketAddr};
use std::time::Duration;

use idevice::afc::opcode::AfcFopenMode;
use idevice::house_arrest::HouseArrestClient;
use idevice::remote_pairing::{connect_tls_psk_tunnel_native, RemotePairingClient, RpPairingFile, RpPairingSocket};
use idevice::rsd::RsdHandshake;
use idevice::tcp::adapter::Adapter;
use idevice::tcp::handle::AdapterHandle;
use idevice::{IdeviceError, RsdService};
use serde_json::{json, Value};
use tokio::net::TcpStream;

/// Apple's remote-pairing daemon port (SideStore minimuxer's remotePairingPort).
const REMOTE_PAIRING_PORT: u16 = 49152;
const ARENA: &str = "com.wizards.mtga";
const REMOTE: &str = "/Documents/services.conf";
const ENVIRONMENT: &str = "ArcaneForge";

pub const ACTION_STATUS: i32 = 0;
pub const ACTION_PRIVATE: i32 = 1;
pub const ACTION_OFFICIAL: i32 = 2;

fn configuration(host: &str) -> Value {
    let base = format!("http://{host}:8787");
    json!({"$environments": [ENVIRONMENT], "environments": {ENVIRONMENT: {
        "name": ENVIRONMENT, "fdHost": host, "fdPort": 9445, "mdHost": host, "mdPort": 9444,
        "doorbellUri": base, "accountSystemBaseUri": format!("{base}/"),
        "accountSystemId": "arena-local", "accountSystemSecret": "local-only",
        "accountSystemEnvironment": 1, "HostPlatform": 1,
        "ecoUri": format!("{base}/eco"), "bikeUri": format!("{base}/local-business-events")}}})
}

fn is_ours(value: &Value) -> bool {
    value.get("$environments") == Some(&json!([ENVIRONMENT]))
}

fn describe(current: &Option<Value>) -> String {
    match current {
        None => "Official servers".into(),
        Some(value) if is_ours(value) => format!(
            "Private server ({})",
            value["environments"][ENVIRONMENT]["fdHost"].as_str().unwrap_or("?")
        ),
        Some(_) => "Unknown services.conf present".into(),
    }
}

fn step(name: &'static str) -> impl Fn(IdeviceError) -> String {
    move |e| format!("{name} failed: {e} ({e:?})")
}

/// Opens Apple's remote-pairing tunnel (iOS 17+) and its RSD handshake. Over a
/// network connection iOS drops the older lockdown TLS session, so this mirrors
/// idevice's ffi tunnel_create_rppairing + finish_tunnel, which SideStore uses.
async fn open_tunnel(pairing: &[u8], device: IpAddr) -> Result<(AdapterHandle, RsdHandshake), String> {
    let mut pairing_file = RpPairingFile::from_bytes(pairing).map_err(step("Reading pairing file"))?;
    let address = SocketAddr::new(device, REMOTE_PAIRING_PORT);
    let stream = TcpStream::connect(address)
        .await
        .map_err(|e| format!("Connecting to the phone failed (is LocalDevVPN on?): {e}"))?;
    let mut pairing_client = RemotePairingClient::new(RpPairingSocket::new(stream), "ArenaSwitch");
    // An existing pairing only needs pair-verify; no PIN is ever entered here.
    pairing_client
        .connect(&mut pairing_file, || async { String::new() })
        .await
        .map_err(step("Pairing check"))?;
    let tunnel_port = pairing_client.create_tcp_listener().await.map_err(step("Opening tunnel"))?;
    let tunnel_stream = TcpStream::connect(SocketAddr::new(device, tunnel_port))
        .await
        .map_err(|e| format!("Connecting tunnel failed: {e}"))?;
    let tunnel = connect_tls_psk_tunnel_native(tunnel_stream, pairing_client.encryption_key())
        .await
        .map_err(step("Securing tunnel"))?;
    let parse = |value: &str| value.parse::<IpAddr>().map_err(|e| format!("Tunnel address invalid: {e}"));
    let client_ip = parse(&tunnel.info.client_address)?;
    let server_ip = parse(&tunnel.info.server_address)?;
    let mtu = tunnel.info.mtu as usize;
    let rsd_port = tunnel.info.server_rsd_port;
    let mut adapter = Adapter::new(Box::new(tunnel.into_inner()), client_ip, server_ip);
    adapter.set_mss(mtu.min(8192) - 60);
    let mut adapter = adapter.to_async_handle();
    let rsd_stream = adapter.connect(rsd_port).await.map_err(|e| format!("Service directory failed: {e}"))?;
    let handshake = RsdHandshake::new(rsd_stream).await.map_err(step("Service directory"))?;
    Ok((adapter, handshake))
}

async fn switch(action: i32, pairing: &[u8], device: &str, host: &str) -> Result<String, String> {
    let device: IpAddr = device.parse().map_err(|_| "VPN address is not an IP address".to_string())?;
    let (mut adapter, mut handshake) = open_tunnel(pairing, device).await?;
    let house_arrest = HouseArrestClient::connect_rsd(&mut adapter, &mut handshake)
        .await
        .map_err(step("Starting file access"))?;
    let mut afc = house_arrest
        .vend_documents(ARENA)
        .await
        .map_err(|e| format!("Arena's folder unavailable: {e}"))?;

    let exists = afc
        .list_dir("/Documents")
        .await
        .map_err(|e| format!("Cannot list Arena's folder: {e}"))?
        .iter()
        .any(|name| name == "services.conf");
    let mut current = None;
    if exists {
        let mut file = afc.open(REMOTE, AfcFopenMode::RdOnly).await.map_err(|e| e.to_string())?;
        let bytes = file.read_entire().await.map_err(|e| e.to_string())?;
        file.close().await.map_err(|e| e.to_string())?;
        current = Some(serde_json::from_slice(&bytes).unwrap_or(Value::Null));
    }
    let ours = current.as_ref().is_some_and(is_ours);
    if current.is_some() && !ours && action != ACTION_STATUS {
        return Err("services.conf on the phone was not written by this app; left unchanged".into());
    }
    match action {
        ACTION_PRIVATE => {
            if host.trim().is_empty() {
                return Err("Enter the server address first".into());
            }
            let value = configuration(host.trim());
            let bytes = serde_json::to_vec_pretty(&value).map_err(|e| e.to_string())?;
            let mut file = afc.open(REMOTE, AfcFopenMode::WrOnly).await.map_err(|e| e.to_string())?;
            file.write_entire(&bytes).await.map_err(|e| e.to_string())?;
            file.close().await.map_err(|e| e.to_string())?;
            current = Some(value);
        }
        ACTION_OFFICIAL if current.is_some() => {
            afc.remove(REMOTE).await.map_err(|e| e.to_string())?;
            current = None;
        }
        _ => {}
    }
    Ok(describe(&current))
}

unsafe fn text(value: *const c_char) -> String {
    if value.is_null() {
        String::new()
    } else {
        CStr::from_ptr(value).to_string_lossy().into_owned()
    }
}

/// Runs one action. Returns 0 on success, 1 on failure; `out` receives a
/// NUL-terminated message (truncated to `out_capacity`).
///
/// # Safety
/// `pairing` must point to `pairing_len` readable bytes, string arguments must
/// be NUL-terminated or null, and `out` must have `out_capacity` writable bytes.
#[no_mangle]
pub unsafe extern "C" fn arcane_switch_run(
    action: i32,
    pairing: *const u8,
    pairing_len: usize,
    device_ip: *const c_char,
    server_host: *const c_char,
    out: *mut c_char,
    out_capacity: usize,
) -> i32 {
    let pairing = if pairing.is_null() { &[][..] } else { std::slice::from_raw_parts(pairing, pairing_len) };
    let (device, host) = (text(device_ip), text(server_host));
    let result = match tokio::runtime::Builder::new_current_thread().enable_all().build() {
        Ok(runtime) => runtime.block_on(async {
            tokio::time::timeout(Duration::from_secs(30), switch(action, pairing, &device, &host))
                .await
                .unwrap_or_else(|_| Err("Timed out reaching the phone".into()))
        }),
        Err(e) => Err(e.to_string()),
    };
    let (code, message) = match result {
        Ok(message) => (0, message),
        Err(message) => (1, message),
    };
    if !out.is_null() && out_capacity > 0 {
        let bytes = message.as_bytes();
        let n = bytes.len().min(out_capacity - 1);
        std::ptr::copy_nonoverlapping(bytes.as_ptr(), out as *mut u8, n);
        *out.add(n) = 0;
    }
    code
}
