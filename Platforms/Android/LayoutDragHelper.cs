
#if ANDROID

namespace PhoneGamepad;

public static class LayoutDragHelper
{
    private class DragListener :
        Java.Lang.Object,
        Android.Views.View.IOnTouchListener
    {
        private readonly Microsoft.Maui.Controls.View target;

        private readonly Action<Microsoft.Maui.Controls.View> onStarted;

        private readonly Action<Microsoft.Maui.Controls.View> onCompleted;


        private float startRawX;

        private float startRawY;

        private double startTranslationX;

        private double startTranslationY;


        public DragListener(
            Microsoft.Maui.Controls.View target,
            Action<Microsoft.Maui.Controls.View> onStarted,
            Action<Microsoft.Maui.Controls.View> onCompleted)
        {
            this.target = target;

            this.onStarted = onStarted;

            this.onCompleted = onCompleted;
        }


        public bool OnTouch(
            Android.Views.View? nativeView,
            Android.Views.MotionEvent? e)
        {
            if (e == null)
                return false;


            switch (e.ActionMasked)
            {
                case Android.Views.MotionEventActions.Down:

                    startRawX = e.RawX;

                    startRawY = e.RawY;

                    startTranslationX =
                        target.TranslationX;

                    startTranslationY =
                        target.TranslationY;


                    onStarted(target);

                    return true;


                case Android.Views.MotionEventActions.Move:

                    float deltaX =
                        e.RawX - startRawX;

                    float deltaY =
                        e.RawY - startRawY;


                    target.TranslationX =
                        startTranslationX + deltaX;

                    target.TranslationY =
                        startTranslationY + deltaY;


                    return true;


                case Android.Views.MotionEventActions.Up:

                case Android.Views.MotionEventActions.Cancel:

                    onCompleted(target);

                    return true;
            }


            return true;
        }
    }


    // =========================================================
    // 只给“真正需要拖动的 View”绑定
    //
    // 不再递归给 Button 子控件绑定
    // =========================================================

    private static readonly
        Dictionary<
            Microsoft.Maui.Controls.View,
            DragListener>
        listeners = new();


    public static void Attach(
        Microsoft.Maui.Controls.View target,

        Action<Microsoft.Maui.Controls.View> onStarted,

        Action<Microsoft.Maui.Controls.View> onCompleted)
    {
        if (target.Handler?.PlatformView
            is not Android.Views.View nativeView)
        {
            return;
        }


        var listener =
            new DragListener(
                target,
                onStarted,
                onCompleted);


        listeners[target] = listener;


        nativeView.SetOnTouchListener(
            listener);
    }


    public static void Detach(
        Microsoft.Maui.Controls.View target)
    {
        if (target.Handler?.PlatformView
            is not Android.Views.View nativeView)
        {
            return;
        }


        nativeView.SetOnTouchListener(null);


        listeners.Remove(target);
    }
}

#endif

