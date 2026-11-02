# Free Cloud Android APK Build Guide (0 MB Local Disk Space)

This project uses **GitHub Actions** to build the Android `.apk` for you in the cloud for free.

---

## Step 1: Upload the Generated License File (`.alf`) to Unity

I have already generated your Unity machine activation file directly on your PC:
📁 **File location:** `c:\Users\Dell\Desktop\Gamev\Unity_v6000.4.4f1.alf`

1. Open [license.unity3d.com](https://license.unity3d.com) in your web browser.
2. Sign in with your Unity ID.
3. Click **Browse** (or upload) and select the file `Unity_v6000.4.4f1.alf` from your `Desktop\Gamev` folder.
4. Select **Unity Personal Edition**, then select *"I don't use Unity in a professional capacity"*.
5. Click **Download License File**. A file ending in `.ulf` will be downloaded to your PC.

---

## Step 2: Add Secrets to GitHub

1. Open the downloaded `.ulf` file with **Notepad** and copy all the text.
2. Go to your repository on GitHub: [github.com/youssefqyt/SkyCoinAdventure](https://github.com/youssefqyt/SkyCoinAdventure)
3. Navigate to **Settings** → **Secrets and variables** → **Actions**.
4. Click **New repository secret** and add:
   - **Name:** `UNITY_LICENSE`
   - **Secret:** *Paste the full text from the `.ulf` file*
5. (Optional but recommended) Add two more secrets if prompted:
   - `UNITY_EMAIL`: Your Unity account email address.
   - `UNITY_PASSWORD`: Your Unity account password.

---

## Step 3: Run the Cloud Build to Get Your `.apk`

1. Go to the **Actions** tab on your GitHub repository.
2. In the left sidebar, click **Build Android APK (Cloud)**.
3. Click the **Run workflow** dropdown on the right, then click the green **Run workflow** button.
4. GitHub will build the APK on its cloud servers (takes ~8 minutes, uses 0 MB on your PC).
5. When the build completes with a green checkmark:
   - Click on the workflow run.
   - Scroll down to the **Artifacts** section at the bottom.
   - Click **SkyCoinAdventure-Android-APK** to download your ready-to-install `.apk`!

---

## Step 4: Install and Play on Your Phone

1. Transfer the `.apk` to your Android phone (or download it directly from GitHub using your phone's browser).
2. Tap the file to install it (enable *Install unknown apps* if asked).
3. Open the game in **landscape mode**.
4. Tap **Nouvelle partie** — the touch controls (virtual joystick, jump, sprint, camera look) will appear automatically!
