# Building the APK

## Prerequisites

- Unity 2022.3 LTS with **Android Build Support** installed (via Unity Hub → Installs → Add Modules)
- Android SDK and NDK bundled with Unity (selected during module install)
- A clone of this repo open in Unity

## 1 — Set your ROS IP

The APK hardcodes the ROS host IP at build time.

1. In the Hierarchy, select **ROSTCPConnector** (or whichever GameObject holds the `ROSConnection` component).
2. In the Inspector, set **ROS IP Address** to the WiFi IP of the laptop running ROS.
3. Leave **ROS Port** at `10000`.

> If you change networks, update this value and rebuild.

## 2 — Switch to Android Build Target

Open **File → Build Settings**.

- Select **Android** in the Platform list.
- Click **Switch Platform** (only needed once; Unity will reimport assets).

> Switching to Android also activates the `UNITY_ANDROID` scripting define, which compiles out all keyboard fallback inputs. See [Editor Setup](Editor-Setup.md) if you want to test with a keyboard.

## 3 — Check Player Settings

Go to **Edit → Project Settings → Player** (with Android selected at the top).

Confirm:

| Setting | Value |
|---------|-------|
| Package Name | `com.StephenCSui.XRTeleop` |
| Minimum API Level | Android 10 (API 29) |
| Scripting Backend | IL2CPP |
| Target Architectures | ARM64 |

These are already saved in the repo — verify they were not reset after a Unity upgrade.

## 4 — Check XR Plugin Management

Go to **Edit → Project Settings → XR Plugin Management** (Android tab).

Confirm **OpenXR** is checked under Plug-in Providers.

Under **OpenXR → Features**, confirm **Oculus Touch Controller Profile** is enabled.

## 5 — Build

In **File → Build Settings**:

- Click **Add Open Scenes** to include `SampleScene` if it is not already listed.
- Click **Build** and choose an output path.

Unity produces a single `.apk` file. See [Installing](Installing.md) for deployment steps.

## Scripting Defines Reference

| Platform | Active Defines |
|----------|---------------|
| Android | `USE_INPUT_SYSTEM_POSE_CONTROL` `USE_STICK_CONTROL_THUMBSTICKS` `ROS2` |
| Standalone | same as Android + keyboard fallback inputs compiled in |

These live in **Edit → Project Settings → Player → Other Settings → Scripting Define Symbols** and are already set in the repo.
