using Foundation;
using UIKit;

namespace FractalGpu.BenchIos;

[Register(nameof(AppDelegate))]
public class AppDelegate : UIApplicationDelegate
{
    public override bool FinishedLaunching(UIApplication application, NSDictionary? launchOptions) => true;

    // iOS 27 requires the UIScene life cycle (legacy window-in-AppDelegate apps trap at launch).
    public override UISceneConfiguration GetConfiguration(UIApplication application, UISceneSession connectingSceneSession, UISceneConnectionOptions options) =>
        new("Default", connectingSceneSession.Role) { DelegateType = typeof(SceneDelegate) };
}

[Register(nameof(SceneDelegate))]
public class SceneDelegate : UIWindowSceneDelegate
{
    public override UIWindow? Window { get; set; }

    public override void WillConnect(UIScene scene, UISceneSession session, UISceneConnectionOptions connectionOptions)
    {
        if (scene is not UIWindowScene windowScene) return;
        Window = new UIWindow(windowScene)
        {
            RootViewController = new UINavigationController(new BenchViewController()),
        };
        Window.MakeKeyAndVisible();
    }
}
