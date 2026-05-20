# XR Teleoperation — Unity Project

Unity-side of the XR teleoperation system. Runs on Meta Quest 2 and communicates with ROS 2 over WiFi via ROS-TCP-Connector.

## Wiki

- [Building the APK](wiki/Building.md)
- [Installing & Running on the Quest](wiki/Installing.md)
- [Editor Setup & Debugging](wiki/Editor-Setup.md)

## Requirements

- Unity 2022.3 LTS
- Android Build Support module (installed via Unity Hub)
- Meta XR SDK / OpenXR plugin (already configured in ProjectSettings)
- ROS-TCP-Connector v0.7.0-preview (included in Packages/)

The ROS-side stack lives in a separate repo: [XR_Teleoperation](https://github.com/StephenUTS/XR_Teleoperation)
