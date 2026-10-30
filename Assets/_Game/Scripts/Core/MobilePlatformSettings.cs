using UnityEngine;

namespace SkyCoin
{
    /// <summary>
    /// Runtime settings that make the game behave well on a phone.
    ///
    /// These are not "graphics settings": they are the small platform switches Unity does not
    /// apply for you. Everything here is a no-op on desktop, so the same build still runs on PC.
    ///
    ///   targetFrameRate 60  - without this, mobile defaults to 30 fps on many devices.
    ///   vSyncCount 0        - vSync overrides targetFrameRate, so it must be off for the cap
    ///                         to be respected.
    ///   sleepTimeout Never  - the screen must not turn off while the player is reading a menu.
    ///   landscape only      - the game is designed for a wide screen; portrait would crop it.
    /// </summary>
    public static class MobilePlatformSettings
    {
        public static void Apply()
        {
            if (!Application.isMobilePlatform) return;

            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 0;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;

            // Allow both landscape directions (so the phone can be turned either way) but
            // never portrait. This works together with Player Settings > Resolution and
            // Presentation > Allowed Orientations.
            Screen.orientation = ScreenOrientation.AutoRotation;
            Screen.autorotateToPortrait = false;
            Screen.autorotateToPortraitUpsideDown = false;
            Screen.autorotateToLandscapeLeft = true;
            Screen.autorotateToLandscapeRight = true;
        }
    }
}
