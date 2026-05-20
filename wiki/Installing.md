# Installing & Running on the Quest

## Prerequisites

- ADB installed on your laptop (`sudo apt install adb` on Ubuntu)
- Meta Quest 2 with **Developer Mode** enabled (must be done from the owner Meta account via the Meta mobile app)
- USB cable connecting Quest to laptop

## Enable USB Debugging

1. Connect the Quest via USB.
2. Put on the headset — a prompt appears asking to allow USB debugging from this computer. Accept it.
3. Verify the device is visible:

```bash
adb devices
# Should show: <serial>    device
# If it shows "unauthorized", see Troubleshooting below
```

## Install APK

```bash
adb install -r path/to/XRTeleop.apk
```

`-r` reinstalls without uninstalling first (preserves data). Use it for updates.

## Full Uninstall + Reinstall

```bash
adb uninstall com.StephenCSui.XRTeleop
adb install path/to/XRTeleop.apk
```

Do a full reinstall when the package name or signing key changes, or when a partial install needs to be cleared.

## Launch App

```bash
adb shell monkey -p com.StephenCSui.XRTeleop 1
```

## Force Stop App

```bash
adb shell am force-stop com.StephenCSui.XRTeleop
```

Force stop terminates the process immediately — use this instead of removing the headset when you want a clean restart.

## Screen Recording

```bash
# Start recording (runs until you Ctrl+C)
adb shell screenrecord /sdcard/capture.mp4

# Pull the file to your desktop
adb pull /sdcard/capture.mp4 ~/Desktop/capture.mp4
```

## Troubleshooting

**`adb devices` shows `unauthorized`**
Developer mode must be enabled from the *owner* Meta account, not a secondary account. Switch to the owner profile in the headset, replug the USB cable, and accept the prompt in the headset.

**App installs but crashes on launch**
The APK was built with the wrong ROS IP or a mismatched ROS-TCP-Connector version. Rebuild with the correct IP and `ros_tcp_endpoint` v0.7.0 on the ROS side.

**App launches but robot does not move**
Quest and laptop are not on the same WiFi network, or the ROS stack is not running. Confirm with `ros2 topic list` on the laptop that `/unity/hand_pose` is being published after you enable teleoperation.
