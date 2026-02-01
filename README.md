# NARAI Manual

This manual provides a short overview of the project structure and explains how to deploy the application to an Android smartphone using Unity.

---

# Project Structure

The repository follows the default Unity project organization.

<img width="535" height="379" alt="image" src="https://github.com/user-attachments/assets/3b4bb59b-fde1-4487-99ce-b7898fd5619e" />

The most relevant folders are:

- **Assets** – contains all application resources and source code  
- **Packages** – contains Unity package dependencies  
- **ProjectSettings** – stores general Unity project configuration  
- **Evaluation Questionnaires** – contains raw data from the user study  

---

## Assets Folder

The internal structure of the `Assets` folder is shown below.

<img width="1643" height="457" alt="image" src="https://github.com/user-attachments/assets/623c969b-6659-4e61-9208-9afa1947fdc2" />

Important subfolders include:

- **Scenes** – contains the AR scene of the application  
- **Scripts** – contains all application logic  
- **Prefabs** – contains reusable objects such as the AR information canvas  
- **UI / Audio / Materials** – supporting visual and sound assets  

---

# Installation and Deployment

Follow the steps below to install and deploy the app to an Android device.

---

## Step 1 – Install Unity

Install **Unity Hub** and a compatible Unity Editor version:

https://learn.unity.com/tutorial/install-the-unity-hub-and-editor

During installation, make sure to include:

- Android Build Support
- Android SDK & NDK Tools
- OpenJDK

<img width="1435" height="928" alt="image" src="https://github.com/user-attachments/assets/cbd28b5e-e2e9-4da8-8867-352e1d5b6f44" />

---

## Step 2 – Enable Developer Options on Android

On your Android smartphone:

1. Open **Settings**
2. Enable **Developer Mode**
3. Enable **USB Debugging**

Example:

<img width="1080" height="2340" alt="image" src="https://github.com/user-attachments/assets/555bcb7a-c9c9-412e-9920-0f595a8f91ff" />

---

## Step 3 – Download the Repository

Download the project:

```bash
https://anonymous.4open.science/r/NARAI-9B8D
```

---

## Step 4 – Open the Project in Unity

1. Start **Unity Hub**
2. Click **Add Project**
3. Select the cloned project folder
4. Open the project with the recommended Unity version

---

## Step 5 – Configure Build Settings

1. In Unity, go to **File → Build Profiles**
2. Select **Android** in the Platforms panel
3. Click **Switch Platform**
4. Ensure the main AR scene is listed in **Scenes In Build**

Example:

<img width="1086" height="907" alt="image" src="https://github.com/user-attachments/assets/9aa48083-ce8c-4e67-a944-46e90ef9cc57" />

---

## Step 6 – Connect the Device

1. Connect the smartphone via USB cable
2. Allow debugging access when prompted
3. Verify the device appears inside Unity

---

## Step 7 – Build and Run

1. Click **Build And Run**
2. Create/select a folder named `Build/`
3. Save the generated `.apk` file
4. Unity automatically installs and launches the app

The `.apk` file is the installable Android application package.

---

# First Launch

When starting the app for the first time:

- Grant camera permissions
- Allow internet access (required for AI analysis)
- Ensure good lighting for scanning products

---

# Troubleshooting

## Device not detected
- Reconnect USB cable
- Enable USB debugging again
- Install Android drivers
- Restart Unity

## Build errors
- Verify Android Build Support is installed
- Switch platform to Android
- Restart Unity

## Slow or failed analysis
- Check internet connection
- Ensure product is clearly visible
- Improve lighting conditions

---

# Notes

- The app uses generative AI via the OpenAI Web API; internet access is required.
- The system is designed for Android smartphones but can be adapted for iOS using ARFoundation.
- For reproducibility and research transparency, the source code is included in the repository.
