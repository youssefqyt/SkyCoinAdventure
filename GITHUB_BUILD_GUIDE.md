# Free Cloud Android APK Build Guide (0 MB Local Disk Space)

This project is now configured with **GitHub Actions**. GitHub's cloud runners build the Android `.apk` for you in the cloud for free, so **no disk space is used on your computer**.

---

## Step 1: Push the project to GitHub

If you haven't pushed this repository to GitHub yet:

1. Go to [github.com/new](https://github.com/new) and create a repository (e.g. `SkyCoinAdventure`). You can choose **Private** or **Public**.
2. Open PowerShell in `c:\Users\Dell\Desktop\Gamev` and run:
   ```powershell
   git add .
   git commit -m "Configure GitHub Actions Android APK build"
   git branch -M main
   git remote add origin https://github.com/<YOUR-USERNAME>/<YOUR-REPO-NAME>.git
   git push -u origin main
   ```
   *(Replace `<YOUR-USERNAME>` and `<YOUR-REPO-NAME>` with your GitHub account details).*

---

## Step 2: Activate the Free Unity License (One-time setup, 2 minutes)

Because Unity requires a license to run in batch mode on GitHub's cloud servers, GameCI provides a 1-click activation tool:

1. On your GitHub repository page, click the **Actions** tab.
2. In the list on the left, click **Acquire Unity Activation File (.alf)**.
3. Click the **Run workflow** button on the right, then click the green **Run workflow**.
4. Wait about 30 seconds for the run to finish (green checkmark).
5. Click on the completed run, scroll to **Artifacts** at the bottom, and download `Unity_Activation_File`.
6. Unzip it to find the file ending in `.alf`.
7. Go to [license.unity3d.com](https://license.unity3d.com) in your web browser:
   - Sign in with your Unity ID.
   - Upload the `.alf` file.
   - Select **Unity Personal Edition** (free).
   - Download the generated `.ulf` license file.
8. Open that `.ulf` file with **Notepad** and copy the entire text.
9. In your GitHub repository:
   - Go to **Settings → Secrets and variables → Actions**.
   - Click **New repository secret**.
   - Name: `UNITY_LICENSE`
   - Secret: Paste the entire text from the `.ulf` file.
   - Click **Add secret**.

---

## Step 3: Generate your `.apk` in the Cloud

Now you can generate your APK anytime with a single click:

1. Go to the **Actions** tab on your GitHub repository.
2. In the left sidebar, click **Build Android APK (Cloud)**.
3. Click the **Run workflow** dropdown on the right, then click **Run workflow**.
4. GitHub's cloud servers will compile the project and build the Android APK.
5. When finished (green checkmark):
   - Click on the workflow run.
   - Under **Artifacts** at the bottom, click **SkyCoinAdventure-Android-APK** to download your ready-to-install `.apk`!

---

## Step 4: Install and Play on your Phone

1. Transfer the downloaded `.apk` to your phone (via USB cable, Google Drive, WhatsApp, or download directly from GitHub on your phone browser).
2. Tap the `.apk` on your phone and allow *Install unknown apps* if prompted.
3. Launch the game in **landscape mode**.
4. Click **Nouvelle partie** — the touch controls (virtual joystick, jump, sprint, camera look drag) appear automatically over the level!
