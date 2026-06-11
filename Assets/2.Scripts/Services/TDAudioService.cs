/// Audio facade — single access point for BGM and SFX.
/// Initialized once in TDControl.Init() and persists for the app lifetime.
public static class TDAudioService
{
    public static TDBGMPlayer BGM { get; private set; }
    public static TDSFXPlayer SFX { get; private set; }

    public static void Init()
    {
        BGM = new TDBGMPlayer();
        SFX = new TDSFXPlayer();
        SFX.Init();

        TDAudioPrefs.OnBgmMuteChanged += BGM.ApplyMute;
    }

    public static void Cleanup()
    {
        if (BGM != null) TDAudioPrefs.OnBgmMuteChanged -= BGM.ApplyMute;
        BGM = null;
        SFX = null;
    }
}
