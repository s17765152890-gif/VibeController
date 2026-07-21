# TCL BT_RC901A_B1 BLE capture guide

This guide documents the read-only-first workflow for validating the experimental RC901A backend. It is intended for developers and hardware testers; the published v1.1.0 binary does not include this backend.

## Safety boundary

VibeController may:

- enumerate a Windows-paired `BT_RC901A_B1`;
- read cached or live GATT service metadata and Battery Level;
- subscribe to Notify or Indicate characteristics under HID (`1812`), D0FF, and D1FF;
- write the standard Client Characteristic Configuration Descriptor required to enable those notifications.

VibeController must not:

- write a TCL vendor characteristic;
- open, inspect, or write the DFU service `00006287-3c17-d293-8e48-14fe2e4da212`;
- send guessed initialization, audio, firmware, or pairing commands;
- assign a logical button to a packet that has not been reproduced on the physical remote.

## What “Driver error” means on this model

On the tested BT_RC901A_B1 (VID `0416`, PID `0301`), the root Bluetooth LE device and the D0FF vendor service start normally. The Windows `hidbthle` child driver fails with problem code 10 because it rejects the remote firmware's HID report descriptor: `A non constant main item was declared without a corresponding usage.`

This failure explains the Windows Settings badge, but it does not prove that the vendor GATT services are unavailable. The dedicated backend deliberately avoids relying on the failed HID child driver.

## Pair or repair the bond

1. Put the remote within one metre of the computer and use fresh batteries.
2. If Windows already lists `BT_RC901A_B1` but VibeController reports `Unreachable`, remove that device from **Settings → Bluetooth & devices**. This is necessary when Windows retains an old bond key while the remote has reset its key.
3. Hold the center D-pad `OK + Back` buttons together for about five seconds until the remote enters pairing mode. This is the verified combination for BT_RC901A_B1; it differs from some other TCL remote models.
4. Add `BT_RC901A_B1` again in Windows and wait for pairing to complete.
5. Do not pair the remote back to the TV during capture; most BLE remotes keep only one active host bond.
6. Start VibeController, choose **TCL RC901A**, save, then press **Reconnect** in the direct-BLE panel.

Expected states:

- **Scanning**: finding the paired Association Endpoint;
- **Connecting**: opening the BLE device and GATT services;
- **Connected**: every inspected service was available;
- **ConnectedLimited**: at least one notification characteristic was subscribed, but another inspected service failed;
- **Error / Unreachable**: Windows knows the device record but cannot establish the encrypted BLE link. Repair the bond before changing code.

## Capture one key at a time

1. Expand **Raw packet log** in Settings and click **Clear**.
2. Press and release exactly one physical key three times at a steady pace.
3. Copy the timestamp, service UUID, characteristic UUID, packet length, and full uppercase hex payload for every new sample.
4. Clear the log before moving to the next key.
5. Capture at least: Up, Down, Left, Right, OK, Back, Home, Menu, Mic, Volume Up/Down, Mute, Channel Up/Down, and digits 0–9.
6. Repeat one earlier key after several other keys to prove that its signature is stable and not a sequence counter.
7. Identify both press and release behavior. A registry entry is not acceptable if it can leave a logical key stuck.
8. Add only verified signatures to `Rc901aReportInterpreter`, with an automated test for press, release, and unknown-packet rejection.

Exact consecutive packets are deduplicated only in the diagnostic list. The live interpreter still receives every notification, so press/release behavior is not hidden from input processing.

## Evidence from the first Windows probe

The initial hardware probe established:

- BLE address: `F0:2B:18:6C:CF:51`;
- nine cached services: Generic Attribute, Generic Access, Battery, Device Information, HID, a second Generic Attribute service, D1FF, D0FF, and the excluded DFU service;
- the default `DeviceInformation` view incorrectly reported paired devices as unpaired, while `DeviceInformationKind.AssociationEndpoint` reported the correct state;
- the remote emitted both connectable-directed and connectable-undirected advertisements at roughly -60 to -70 dBm;
- during the pairing-mode test using `OK + Back`, 512 target advertisements were observed in 45 seconds;
- Windows still returned `Unreachable`, which is consistent with stale host/remote bond keys and requires an explicit remove-and-re-pair before packet capture.

Do not work around a stale bond by disabling Bluetooth security, installing an unsigned driver, or writing vendor/DFU characteristics.

## References

- [Microsoft: Bluetooth GATT client](https://learn.microsoft.com/windows/uwp/devices-sensors/gatt-client)
- [Microsoft: `GattSession.MaintainConnection`](https://learn.microsoft.com/uwp/api/windows.devices.bluetooth.genericattributeprofile.gattsession.maintainconnection)
