// One authoritative input context shared by existing gameplay readers and UI.
public enum GameInputContext { Gameplay, AITextEntry, UINavigating, ModalDialog }
public static class GameplayInputGate {
    static GameInputContext context = GameInputContext.Gameplay;
    static bool applicationFocused = true;
    public static GameInputContext Context { get { return context; } }
    public static bool Typing { get { return context == GameInputContext.AITextEntry; } set { context = value ? GameInputContext.AITextEntry : GameInputContext.Gameplay; } }
    public static bool BlocksGameplay { get { return !applicationFocused || context != GameInputContext.Gameplay; } }
    public static bool UIKeyboardNavigation { get { return applicationFocused && (context == GameInputContext.UINavigating || context == GameInputContext.ModalDialog); } }
    public static void SetContext(GameInputContext next) { context = next; }
    public static void SetApplicationFocus(bool focused) { applicationFocused = focused; }
}
