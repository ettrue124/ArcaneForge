//! Switch the official Arena app between Wizards' servers and the private server,
//! on the iPhone itself.
//!
//! The phone's own lockdown service is reached through a loopback VPN (for
//! example LocalDevVPN at 10.7.0.1) using a pairing file made once on a PC.
//! House Arrest vends Arena's Documents folder; only /Documents/services.conf is
//! written or removed, and only a file this tool wrote is replaced or removed.
//! Mirrors tools/iphone_arena_switch.py.

use std::ffi::{c_char, CStr};
use std::net::IpAddr;
use std::time::Duration;

use idevice::afc::opcode::AfcFopenMode;
use idevice::house_arrest::HouseArrestClient;
use idevice::lockdown::LockdownClient;
use idevice::pairing_file::PairingFile;
use idevice::provider::{IdeviceProvider, TcpProvider};
use idevice::{IdeviceError, IdeviceService};
use serde_json::{json, Value};

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

async fn switch(action: i32, pairing: &[u8], device: &str, host: &str) -> Result<String, String> {
    let pairing_file = PairingFile::from_bytes(pairing).map_err(|e| format!("Pairing file unreadable: {e}"))?;
    let addr: IpAddr = device.parse().map_err(|_| "Device address is not an IP address".to_string())?;
    let provider = TcpProvider { addr, scope_id: None, pairing_file, label: "arcane-switch".into() };
    // Step by step (rather than HouseArrestClient::connect) so a failure names the step.
    let step = |name: &'static str| move |e: IdeviceError| format!("{name} failed: {e} ({e:?})");
    let pairing = provider.get_pairing_file().await.map_err(step("Reading pairing file"))?;
    let mut lockdown = LockdownClient::connect(&provider)
        .await
        .map_err(step("Connecting to the phone (is the VPN on?)"))?;
    let legacy = lockdown.start_session(&pairing).await.map_err(step("Secure session"))?;
    let (port, ssl) = lockdown
        .start_service(HouseArrestClient::service_name().to_string())
        .await
        .map_err(step("Starting file access"))?;
    let mut service = provider.connect(port).await.map_err(step("Opening file access"))?;
    if ssl {
        service.start_session(&pairing, legacy).await.map_err(step("Securing file access"))?;
    }
    let house_arrest = HouseArrestClient::new(service);
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
