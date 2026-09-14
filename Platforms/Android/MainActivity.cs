using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using AndroidX.Core.View;

namespace PhoneGamepad;

[Activity(
    Theme = "@style/Maui.SplashTheme",
    MainLauncher = true,

    // 固定横屏
    ScreenOrientation = ScreenOrientation.Landscape,

    ConfigurationChanges =
        ConfigChanges.ScreenSize |
        ConfigChanges.Orientation |
        ConfigChanges.UiMode |
        ConfigChanges.ScreenLayout |
        ConfigChanges.SmallestScreenSize |
        ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    protected override void OnCreate(
        Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        MakeFullscreen();
    }

    private void MakeFullscreen()
    {
        if (Window == null)
            return;

        // =====================================================
        // 允许 App 内容延伸到系统栏区域
        // =====================================================

        WindowCompat.SetDecorFitsSystemWindows(
            Window,
            false);


        // =====================================================
        // 允许内容进入刘海 / 摄像头区域
        // =====================================================

        if (Build.VERSION.SdkInt >= BuildVersionCodes.P)
        {
            Window.Attributes.LayoutInDisplayCutoutMode =
                LayoutInDisplayCutoutMode.ShortEdges;
        }


        // =====================================================
        // 获取系统栏控制器
        // =====================================================

        WindowInsetsControllerCompat controller =
            WindowCompat.GetInsetsController(
                Window,
                Window.DecorView);


        // =====================================================
        // 隐藏状态栏 + 导航栏
        // =====================================================

        controller.Hide(
            WindowInsetsCompat.Type.SystemBars());


        // =====================================================
        // 允许从边缘滑动临时显示系统栏
        // =====================================================

        controller.SystemBarsBehavior =
            WindowInsetsControllerCompat
                .BehaviorShowTransientBarsBySwipe;
    }
}