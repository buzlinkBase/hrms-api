using DevExpress.XtraSplashScreen;

namespace Buzlink.HR.UI
{
    public class Progress
    {
        public static IOverlaySplashScreenHandle ShowProgressPanel(Control control)
        {
            return SplashScreenManager.ShowOverlayForm(control);
        }
        public static void CloseProgressPanel(IOverlaySplashScreenHandle handle)
        {
            if (handle != null) SplashScreenManager.CloseOverlayForm(handle);
            handle = null;
        }
    }

    public class MainProgress
    {
        public static Action<int> handler;
    }
}
