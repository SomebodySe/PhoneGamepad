
using System.Text.Json;

namespace PhoneGamepad;

public partial class MainPage : ContentPage
{
    // TCP Server 使用的当前手柄状态
    public static GamepadState CurrentGamepad { get; private set; } = new();
    private readonly GamepadState gamepad = CurrentGamepad;
    private readonly TcpGamepadServer tcpServer = new();

    // 编辑模式
    private bool isEditMode = false;
    private bool swapABXY = false;

    // 摇杆
    private const double JoystickRadius = 60.0;
    private const double JoystickFullThreshold = 0.95;

    // 布局保存
    private const string PositionPrefix = "PhoneGamepad_Position_";

    // 进入编辑模式之前的位置
    private readonly Dictionary<View, Point> originalPositions = new();

    // 编辑模式拖动状态
    private View? draggingView;
    private float dragStartRawX;
    private float dragStartRawY;
    private double dragStartTranslationX;
    private double dragStartTranslationY;

    // 构造函数
    public MainPage()
    {
        InitializeComponent();
        LoadSavedLayout();
        UpdateStatus();
        _ = tcpServer.StartAsync(5066);
    }

    // 页面
    protected override void OnAppearing()
    {
        base.OnAppearing();
        LoadSavedLayout();
#if ANDROID
        EnableAndroidMultiTouch();
#endif
    }

#if ANDROID
    private void EnableAndroidMultiTouch()
    {
        if (GamepadLayout.Handler?.PlatformView
            is Android.Views.ViewGroup nativeLayout)
        {
            nativeLayout.MotionEventSplittingEnabled = true;
        }
    }
#endif

    // 获取所有可以移动的控件
    private IEnumerable<View> GetEditableViews()
    {
        yield return LTButton;
        yield return LBButton;
        yield return LeftJoystick;
        yield return L3Button;
        yield return DPad;
        yield return BackButton;
        yield return StartButton;
        yield return RightJoystick;
        yield return R3Button;
        yield return ABXY;
        yield return RTButton;
        yield return RBButton;
    }

    // 状态显示
    private void UpdateStatus()
    {
        if (isEditMode)
        {
            StatusLabel.Text = "编辑模式：按住控件拖动";
            return;
        }

        List<string> pressed = new();

        if (gamepad.A)
            pressed.Add("A");
        if (gamepad.B)
            pressed.Add("B");
        if (gamepad.X)
            pressed.Add("X");
        if (gamepad.Y)
            pressed.Add("Y");
        if (gamepad.LB)
            pressed.Add("LB");
        if (gamepad.RB)
            pressed.Add("RB");
        if (gamepad.LT)
            pressed.Add("LT");
        if (gamepad.RT)
            pressed.Add("RT");
        if (gamepad.L3)
            pressed.Add("L3");
        if (gamepad.R3)
            pressed.Add("R3");
        if (gamepad.Start)
            pressed.Add("Start");
        if (gamepad.Back)
            pressed.Add("Back");
        if (gamepad.DPadUp)
            pressed.Add("↑");
        if (gamepad.DPadDown)
            pressed.Add("↓");
        if (gamepad.DPadLeft)
            pressed.Add("←");
        if (gamepad.DPadRight)
            pressed.Add("→");

        string buttons = pressed.Count == 0 ? "无按键" : string.Join(" ", pressed);

        StatusLabel.Text =
            $"A:{gamepad.A} B:{gamepad.B} " +
            $"X:{gamepad.X} Y:{gamepad.Y}  " +
            $"LX:{gamepad.LX:F2} LY:{gamepad.LY:F2}  " +
            $"RX:{gamepad.RX:F2} RY:{gamepad.RY:F2}  " +
            buttons;
    }

    // 清空手柄状态
    private void ClearGamepadState()
    {
        gamepad.A = false;
        gamepad.B = false;
        gamepad.X = false;
        gamepad.Y = false;
        gamepad.LB = false;
        gamepad.RB = false;
        gamepad.LX = 0;
        gamepad.LY = 0;
        gamepad.RX = 0;
        gamepad.RY = 0;
        gamepad.L3 = false;
        gamepad.R3 = false;
        gamepad.LT = false;
        gamepad.RT = false;
        gamepad.Start = false;
        gamepad.Back = false;
        gamepad.DPadUp = false;
        gamepad.DPadDown = false;
        gamepad.DPadLeft = false;
        gamepad.DPadRight = false;

        if (JoystickKnob != null)
        {
            JoystickKnob.TranslationX = 0;
            JoystickKnob.TranslationY = 0;
        }

        if (RightJoystickKnob != null)
        {
            RightJoystickKnob.TranslationX = 0;
            RightJoystickKnob.TranslationY = 0;
        }

        UpdateStatus();
    }

    // A
    // A
    private void AButton_Pressed(object sender, EventArgs e)
    {
        if (isEditMode)
            return;

        if (swapABXY)
            gamepad.B = true;
        else
            gamepad.A = true;

        UpdateStatus();
    }

    private void AButton_Released(object sender, EventArgs e)
    {
        if (isEditMode)
            return;

        if (swapABXY)
            gamepad.B = false;
        else
            gamepad.A = false;

        UpdateStatus();
    }

    // B
    private void BButton_Pressed(object sender, EventArgs e)
    {
        if (isEditMode)
            return;

        if (swapABXY)
            gamepad.A = true;
        else
            gamepad.B = true;

        UpdateStatus();
    }

    private void BButton_Released(object sender, EventArgs e)
    {
        if (isEditMode)
            return;

        if (swapABXY)
            gamepad.A = false;
        else
            gamepad.B = false;

        UpdateStatus();
    }

    // X
    private void XButton_Pressed(object sender, EventArgs e)
    {
        if (isEditMode)
            return;

        if (swapABXY)
            gamepad.Y = true;
        else
            gamepad.X = true;

        UpdateStatus();
    }

    private void XButton_Released(object sender, EventArgs e)
    {
        if (isEditMode)
            return;

        if (swapABXY)
            gamepad.Y = false;
        else
            gamepad.X = false;

        UpdateStatus();
    }

    // Y
    private void YButton_Pressed(object sender, EventArgs e)
    {
        if (isEditMode)
            return;

        if (swapABXY)
            gamepad.X = true;
        else
            gamepad.Y = true;

        UpdateStatus();
    }

    private void YButton_Released(object sender, EventArgs e)
    {
        if (isEditMode)
            return;

        if (swapABXY)
            gamepad.X = false;
        else
            gamepad.Y = false;

        UpdateStatus();
    }

    // LB
    private void LBButton_Pressed(object sender, EventArgs e)
    {
        if (isEditMode)
            return;
        gamepad.LB = true;
        UpdateStatus();
    }

    private void LBButton_Released(object sender, EventArgs e)
    {
        if (isEditMode)
            return;
        gamepad.LB = false;
        UpdateStatus();
    }

    // RB
    private void RBButton_Pressed(object sender, EventArgs e)
    {
        if (isEditMode)
            return;
        gamepad.RB = true;
        UpdateStatus();
    }

    private void RBButton_Released(object sender, EventArgs e)
    {
        if (isEditMode)
            return;
        gamepad.RB = false;
        UpdateStatus();
    }

    // LT
    private void LTButton_Pressed(object sender, EventArgs e)
    {
        if (isEditMode)
            return;
        gamepad.LT = true;
        UpdateStatus();
    }

    private void LTButton_Released(object sender, EventArgs e)
    {
        if (isEditMode)
            return;
        gamepad.LT = false;
        UpdateStatus();
    }

    // RT
    private void RTButton_Pressed(object sender, EventArgs e)
    {
        if (isEditMode)
            return;
        gamepad.RT = true;
        UpdateStatus();
    }

    private void RTButton_Released(object sender, EventArgs e)
    {
        if (isEditMode)
            return;
        gamepad.RT = false;
        UpdateStatus();
    }

    // L3
    private void L3Button_Pressed(object sender, EventArgs e)
    {
        if (isEditMode)
            return;
        gamepad.L3 = true;
        UpdateStatus();
    }

    private void L3Button_Released(object sender, EventArgs e)
    {
        if (isEditMode)
            return;
        gamepad.L3 = false;
        UpdateStatus();
    }

    // R3
    private void R3Button_Pressed(object sender, EventArgs e)
    {
        if (isEditMode)
            return;
        gamepad.R3 = true;
        UpdateStatus();
    }

    private void R3Button_Released(object sender, EventArgs e)
    {
        if (isEditMode)
            return;
        gamepad.R3 = false;
        UpdateStatus();
    }

    // Start
    private void StartButton_Pressed(object sender, EventArgs e)
    {
        if (isEditMode)
            return;
        gamepad.Start = true;
        UpdateStatus();
    }

    private void StartButton_Released(object sender, EventArgs e)
    {
        if (isEditMode)
            return;
        gamepad.Start = false;
        UpdateStatus();
    }

    // Back
    private void BackButton_Pressed(object sender, EventArgs e)
    {
        if (isEditMode)
            return;
        gamepad.Back = true;
        UpdateStatus();
    }

    private void BackButton_Released(object sender, EventArgs e)
    {
        if (isEditMode)
            return;
        gamepad.Back = false;
        UpdateStatus();
    }

    // DPad
    private void DPadUp_Pressed(object sender, EventArgs e)
    {
        if (isEditMode)
            return;
        gamepad.DPadUp = true;
        UpdateStatus();
    }

    private void DPadUp_Released(object sender, EventArgs e)
    {
        if (isEditMode)
            return;
        gamepad.DPadUp = false;
        UpdateStatus();
    }

    private void DPadDown_Pressed(object sender, EventArgs e)
    {
        if (isEditMode)
            return;
        gamepad.DPadDown = true;
        UpdateStatus();
    }

    private void DPadDown_Released(object sender, EventArgs e)
    {
        if (isEditMode)
            return;
        gamepad.DPadDown = false;
        UpdateStatus();
    }

    private void DPadLeft_Pressed(object sender, EventArgs e)
    {
        if (isEditMode)
            return;
        gamepad.DPadLeft = true;
        UpdateStatus();
    }

    private void DPadLeft_Released(object sender, EventArgs e)
    {
        if (isEditMode)
            return;
        gamepad.DPadLeft = false;
        UpdateStatus();
    }

    private void DPadRight_Pressed(object sender, EventArgs e)
    {
        if (isEditMode)
            return;
        gamepad.DPadRight = true;
        UpdateStatus();
    }

    private void DPadRight_Released(object sender, EventArgs e)
    {
        if (isEditMode)
            return;
        gamepad.DPadRight = false;
        UpdateStatus();
    }

    // 左摇杆
    private void Joystick_PanUpdated(object? sender, PanUpdatedEventArgs e)
    {
        if (isEditMode)
            return;

        switch (e.StatusType)
        {
            case GestureStatus.Running:
                double x = e.TotalX;
                double y = e.TotalY;
                double distance = Math.Sqrt(x * x + y * y);

                if (distance > JoystickRadius)
                {
                    x = x / distance * JoystickRadius;
                    y = y / distance * JoystickRadius;
                }

                gamepad.LX = (float)Math.Clamp((x / JoystickRadius) / JoystickFullThreshold, -1.0, 1.0);
                gamepad.LY = (float)Math.Clamp((y / JoystickRadius) / JoystickFullThreshold, -1.0, 1.0);

                JoystickKnob.TranslationX = x;
                JoystickKnob.TranslationY = y;
                UpdateStatus();
                break;

            case GestureStatus.Completed:
            case GestureStatus.Canceled:
                gamepad.LX = 0;
                gamepad.LY = 0;
                JoystickKnob.TranslationX = 0;
                JoystickKnob.TranslationY = 0;
                UpdateStatus();
                break;
        }
    }

    // 右摇杆
    private void RightJoystick_PanUpdated(object? sender, PanUpdatedEventArgs e)
    {
        if (isEditMode)
            return;

        switch (e.StatusType)
        {
            case GestureStatus.Running:
                double x = e.TotalX;
                double y = e.TotalY;
                double distance = Math.Sqrt(x * x + y * y);

                if (distance > JoystickRadius)
                {
                    x = x / distance * JoystickRadius;
                    y = y / distance * JoystickRadius;
                }

                gamepad.RX = (float)Math.Clamp((x / JoystickRadius) / JoystickFullThreshold, -1.0, 1.0);
                gamepad.RY = (float)Math.Clamp((y / JoystickRadius) / JoystickFullThreshold, -1.0, 1.0);

                RightJoystickKnob.TranslationX = x;
                RightJoystickKnob.TranslationY = y;
                UpdateStatus();
                break;

            case GestureStatus.Completed:
            case GestureStatus.Canceled:
                gamepad.RX = 0;
                gamepad.RY = 0;
                RightJoystickKnob.TranslationX = 0;
                RightJoystickKnob.TranslationY = 0;
                UpdateStatus();
                break;
        }
    }

    // 保存进入编辑模式之前的位置
    private void SaveCurrentPositions()
    {
        originalPositions.Clear();

        foreach (View view in GetEditableViews())
        {
            originalPositions[view] = new Point(view.TranslationX, view.TranslationY);
        }
    }

    // 保存当前布局
    private void SaveCurrentLayout()
    {
        foreach (View view in GetEditableViews())
        {
            if (string.IsNullOrEmpty(view.AutomationId))
            {
                continue;
            }

            string key = PositionPrefix + view.AutomationId;

            Preferences.Default.Set(key + "_X", view.TranslationX);
            Preferences.Default.Set(key + "_Y", view.TranslationY);
        }
    }

    // 加载保存的布局
    private void LoadSavedLayout()
    {
        foreach (View view in GetEditableViews())
        {
            if (string.IsNullOrEmpty(view.AutomationId))
            {
                continue;
            }

            string key = PositionPrefix + view.AutomationId;

            if (Preferences.Default.ContainsKey(key + "_X") &&
                Preferences.Default.ContainsKey(key + "_Y"))
            {
                view.TranslationX = Preferences.Default.Get(key + "_X", 0.0);
                view.TranslationY = Preferences.Default.Get(key + "_Y", 0.0);
            }
        }
    }

    // 判断触摸点属于哪个控件
    private View? FindViewAtPoint(double x, double y)
    {
        View[] views = GetEditableViews().ToArray();

        for (int i = views.Length - 1; i >= 0; i--)
        {
            View view = views[i];
            Rect bounds = view.Bounds;

            double left = bounds.X + view.TranslationX;
            double top = bounds.Y + view.TranslationY;
            double right = left + bounds.Width;
            double bottom = top + bounds.Height;

            if (x >= left && x <= right && y >= top && y <= bottom)
            {
                return view;
            }
        }

        return null;
    }

#if ANDROID
    // Android 编辑拖动层
    private Android.Views.View.IOnTouchListener? editDragTouchListener;

    private class EditDragTouchListener : Java.Lang.Object, Android.Views.View.IOnTouchListener
    {
        private readonly MainPage page;

        public EditDragTouchListener(MainPage page)
        {
            this.page = page;
        }

        public bool OnTouch(Android.Views.View? v, Android.Views.MotionEvent? e)
        {
            if (e == null)
                return false;

            switch (e.ActionMasked)
            {
                case Android.Views.MotionEventActions.Down:
                    page.BeginEditDrag(e.RawX, e.RawY);
                    return true;

                case Android.Views.MotionEventActions.Move:
                    page.UpdateEditDrag(e.RawX, e.RawY);
                    return true;

                case Android.Views.MotionEventActions.Up:
                case Android.Views.MotionEventActions.Cancel:
                    page.EndEditDrag();
                    return true;
            }

            return true;
        }
    }

    private void AttachEditDragLayer()
    {
        if (EditDragLayer.Handler?.PlatformView is not Android.Views.View nativeLayer)
        {
            return;
        }

        editDragTouchListener ??= new EditDragTouchListener(this);
        nativeLayer.SetOnTouchListener(editDragTouchListener);
    }

    private void DetachEditDragLayer()
    {
        if (EditDragLayer.Handler?.PlatformView is Android.Views.View nativeLayer)
        {
            nativeLayer.SetOnTouchListener(null);
        }

        editDragTouchListener = null;
    }

    // 开始拖动
    private void BeginEditDrag(float rawX, float rawY)
    {
        draggingView = null;

        View? target = FindViewAtScreenPoint(rawX, rawY);

        if (target == null)
            return;

        draggingView = target;
        dragStartRawX = rawX;
        dragStartRawY = rawY;
        dragStartTranslationX = target.TranslationX;
        dragStartTranslationY = target.TranslationY;
    }

    // 根据 Android 屏幕坐标寻找控件
    private View? FindViewAtScreenPoint(float rawX, float rawY)
    {
        View[] views = GetEditableViews().ToArray();

        for (int i = views.Length - 1; i >= 0; i--)
        {
            View view = views[i];

            if (!view.IsVisible)
                continue;

            if (view.Handler?.PlatformView is not Android.Views.View nativeView)
            {
                continue;
            }

            int[] location = new int[2];
            nativeView.GetLocationOnScreen(location);

            float left = location[0];
            float top = location[1];
            float right = left + nativeView.Width;
            float bottom = top + nativeView.Height;

            if (rawX >= left && rawX <= right && rawY >= top && rawY <= bottom)
            {
                return view;
            }
        }

        return null;
    }

    // 拖动过程中移动控件
    private void UpdateEditDrag(float rawX, float rawY)
    {
        if (draggingView == null)
            return;

#if ANDROID
        double density = DeviceDisplay.Current.MainDisplayInfo.Density;

        if (density <= 0)
            density = 1;

        double deltaX = (rawX - dragStartRawX) / density;
        double deltaY = (rawY - dragStartRawY) / density;
#else
        double deltaX = rawX - dragStartRawX;
        double deltaY = rawY - dragStartRawY;
#endif

        draggingView.TranslationX = dragStartTranslationX + deltaX;
        draggingView.TranslationY = dragStartTranslationY + deltaY;
    }

    // 结束拖动
    private void EndEditDrag()
    {
        draggingView = null;
    }
#endif

    // 编辑布局按钮
    private void EditLayoutButton_Clicked(object sender, EventArgs e)
    {
        EnableEditMode();
    }

    private void SwapABXYButton_Clicked(object sender, EventArgs e)
    {
        swapABXY = !swapABXY;

        AButton.Text = swapABXY ? "B" : "A";
        BButton.Text = swapABXY ? "A" : "B";
        XButton.Text = swapABXY ? "Y" : "X";
        YButton.Text = swapABXY ? "X" : "Y";

        SwapABXYButton.Text = swapABXY ? "交换 ABXY" : "交换 ABXY";

        ClearGamepadState();
    }

    // 进入编辑模式
    private void EnableEditMode()
    {
        isEditMode = true;
        SaveCurrentPositions();
        ClearGamepadState();

        EditDragLayer.IsVisible = true;
        EditDragLayer.InputTransparent = false;

#if ANDROID
        AttachEditDragLayer();
#endif

        EditLayoutButton.IsVisible = false;
        ResetLayoutButton.IsVisible = true;
        FinishEditButton.IsVisible = true;
        SwapABXYButton.IsVisible = true;

        StatusLabel.Text = "编辑模式：按住控件拖动";
    }

    // 恢复默认按钮
    private void ResetLayoutButton_Clicked(object sender, EventArgs e)
    {
        ResetToFactoryLayout();
    }

    // 恢复默认布局
    private void ResetToFactoryLayout()
    {
        foreach (View view in GetEditableViews())
        {
            view.TranslationX = 0;
            view.TranslationY = 0;
        }

        ResetJoystickVisuals();
        ClearGamepadState();
        UpdateStatus();
    }

    private void ResetJoystickVisuals()
    {
        if (JoystickKnob != null)
        {
            JoystickKnob.TranslationX = 0;
            JoystickKnob.TranslationY = 0;
        }

        if (RightJoystickKnob != null)
        {
            RightJoystickKnob.TranslationX = 0;
            RightJoystickKnob.TranslationY = 0;
        }

        gamepad.LX = 0;
        gamepad.LY = 0;
        gamepad.RX = 0;
        gamepad.RY = 0;
    }

    // 完成编辑
    private void FinishEditButton_Clicked(object sender, EventArgs e)
    {
        DisableEditMode();
    }

    // 退出编辑模式
    private void DisableEditMode()
    {
#if ANDROID
        DetachEditDragLayer();
#endif

        draggingView = null;
        EditDragLayer.IsVisible = false;
        EditDragLayer.InputTransparent = true;
        isEditMode = false;

        // 自动保存布局
        SaveCurrentLayout();

        EditLayoutButton.IsVisible = true;
        ResetLayoutButton.IsVisible = false;
        FinishEditButton.IsVisible = false;
        SwapABXYButton.IsVisible = false;

        ClearGamepadState();
        UpdateStatus();
    }
}
