import SwiftUI
import UniformTypeIdentifiers

@main
struct ArcaneSwitchApp: App {
    var body: some Scene {
        WindowGroup { ContentView() }
    }
}

/// Calls the Rust core off the main thread.
enum Switcher {
    static func run(_ action: Int32, pairing: Data, deviceIP: String, host: String) async -> (ok: Bool, message: String) {
        await Task.detached {
            var buffer = [CChar](repeating: 0, count: 1024)
            let code = pairing.withUnsafeBytes { bytes in
                arcane_switch_run(action, bytes.bindMemory(to: UInt8.self).baseAddress, pairing.count,
                                  deviceIP, host, &buffer, buffer.count)
            }
            return (code == 0, String(cString: buffer))
        }.value
    }
}

struct ContentView: View {
    @AppStorage("serverHost") private var host = ""
    @AppStorage("deviceIP") private var deviceIP = "10.7.0.1"
    @State private var status = "Import your pairing file to start"
    @State private var working = false
    @State private var importing = false

    private static var pairingURL: URL {
        FileManager.default.urls(for: .applicationSupportDirectory, in: .userDomainMask)[0]
            .appendingPathComponent("pairing.plist")
    }

    private var pairing: Data? { try? Data(contentsOf: Self.pairingURL) }

    var body: some View {
        NavigationStack {
            Form {
                Section("Arena is using") {
                    Text(status).font(.headline)
                }
                Section {
                    Button("Private server") { perform(ARCANE_SWITCH_PRIVATE) }
                    Button("Official servers") { perform(ARCANE_SWITCH_OFFICIAL) }
                    Button("Refresh") { perform(ARCANE_SWITCH_STATUS) }
                } footer: {
                    Text("After switching, fully close Arena and open it again. Keep LocalDevVPN connected.")
                }
                .disabled(working || pairing == nil)
                Section("Settings") {
                    LabeledContent("Server address") {
                        TextField("100.x.x.x", text: $host).multilineTextAlignment(.trailing)
                            .keyboardType(.numbersAndPunctuation).autocorrectionDisabled()
                    }
                    LabeledContent("VPN address") {
                        TextField("10.7.0.1", text: $deviceIP).multilineTextAlignment(.trailing)
                            .keyboardType(.numbersAndPunctuation).autocorrectionDisabled()
                    }
                    Button(pairing == nil ? "Import pairing file" : "Replace pairing file") { importing = true }
                }
            }
            .navigationTitle("Arena Switch")
            .fileImporter(isPresented: $importing, allowedContentTypes: [.propertyList, .data]) { result in
                importPairing(result)
            }
            .task { if pairing != nil { perform(ARCANE_SWITCH_STATUS) } }
        }
    }

    private func perform(_ action: Int32) {
        guard let pairing else { return }
        working = true
        status = "Working..."
        Task {
            let result = await Switcher.run(action, pairing: pairing, deviceIP: deviceIP, host: host)
            status = result.ok && action != ARCANE_SWITCH_STATUS
                ? result.message + " - now reopen Arena" : result.message
            working = false
        }
    }

    private func importPairing(_ result: Result<URL, Error>) {
        guard case .success(let url) = result else { return }
        let scoped = url.startAccessingSecurityScopedResource()
        defer { if scoped { url.stopAccessingSecurityScopedResource() } }
        do {
            let data = try Data(contentsOf: url)
            let folder = Self.pairingURL.deletingLastPathComponent()
            try FileManager.default.createDirectory(at: folder, withIntermediateDirectories: true)
            // The pairing file grants full device access; keep it out of backups and file sharing.
            try data.write(to: Self.pairingURL, options: [.atomic, .completeFileProtection])
            var values = URLResourceValues()
            values.isExcludedFromBackup = true
            var stored = Self.pairingURL
            try stored.setResourceValues(values)
            perform(ARCANE_SWITCH_STATUS)
        } catch {
            status = "Could not import pairing file"
        }
    }
}
