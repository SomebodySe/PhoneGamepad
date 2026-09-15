#include <iostream>
#include <string>
#include <vector>
#include <filesystem>
#include <thread>
#include <chrono>
#include <winsock2.h>
#include <windows.h>
#include <ViGEm/Client.h>
#include <nlohmann/json.hpp>

#pragma comment(lib, "ws2_32.lib")
#pragma comment(lib, "ViGEmClient.lib")
#pragma comment(lib, "SetupAPI.lib")

using json = nlohmann::json;
namespace fs = std::filesystem;

constexpr int TCP_PORT = 5066;
constexpr int RETRY_SECONDS = 2;
constexpr bool DEBUG_OUTPUT = false;

// [-1,1] 转 XInput 摇杆范围
SHORT StickToXInput(float value)
{
    value = (value > 1.0f) ? 1.0f : (value < -1.0f) ? -1.0f : value;
    return static_cast<SHORT>(value >= 0.0f ? value * 32767.0f : value * 32768.0f);
}

// 设置 Xbox 按钮
void SetButton(WORD& buttons, bool pressed, WORD button)
{
    if (pressed) buttons |= button;
    else buttons &= ~button;
}

// 获取当前 EXE 所在目录
fs::path GetExeDirectory()
{
    wchar_t buffer[MAX_PATH]{};
    GetModuleFileNameW(nullptr, buffer, MAX_PATH);
    return fs::path(buffer).parent_path();
}

// EXE 同目录优先，否则逐级向上寻找 platform-tools
fs::path FindAdbPath()
{
    fs::path current = GetExeDirectory();

    for (int i = 0; i < 7 && !current.empty(); ++i)
    {
        fs::path adb = current / L"platform-tools" / L"adb.exe";
        if (fs::exists(adb)) return adb;

        fs::path parent = current.parent_path();
        if (parent == current) break;
        current = parent;
    }

    return {};
}

// 执行 adb 命令
bool RunAdb(const fs::path& adbPath, const std::wstring& arguments, DWORD& exitCode)
{
    std::wstring commandLine = L"\"" + adbPath.wstring() + L"\" " + arguments;
    std::vector<wchar_t> command(commandLine.begin(), commandLine.end());
    command.push_back(L'\0');

    STARTUPINFOW si{};
    PROCESS_INFORMATION pi{};
    si.cb = sizeof(si);
    si.dwFlags = STARTF_USESHOWWINDOW;
    si.wShowWindow = SW_HIDE;

    BOOL result = CreateProcessW(
        nullptr,
        command.data(),
        nullptr,
        nullptr,
        FALSE,
        CREATE_NO_WINDOW,
        nullptr,
        adbPath.parent_path().c_str(),
        &si,
        &pi
    );

    if (!result) return false;

    DWORD waitResult = WaitForSingleObject(pi.hProcess, 5000);

    if (waitResult == WAIT_TIMEOUT)
    {
        TerminateProcess(pi.hProcess, 1);
        CloseHandle(pi.hProcess);
        CloseHandle(pi.hThread);
        return false;
    }

    exitCode = 1;
    GetExitCodeProcess(pi.hProcess, &exitCode);

    CloseHandle(pi.hProcess);
    CloseHandle(pi.hThread);

    return true;
}

// 创建 ADB TCP Forward
bool EnsureAdbForward()
{
    static bool lastReady = false;
    static bool printedNotFound = false;

    fs::path adbPath = FindAdbPath();

    if (adbPath.empty())
    {
        if (!printedNotFound)
        {
            std::cout << "[ADB] 找不到 platform-tools\\adb.exe，2 秒后重试...\n";
            printedNotFound = true;
        }

        lastReady = false;
        return false;
    }

    printedNotFound = false;

    DWORD exitCode = 1;

    // 启动 ADB Server
    if (!RunAdb(adbPath, L"start-server", exitCode) || exitCode != 0)
    {
        if (lastReady)
            std::cout << "\n[ADB] ADB Server 启动失败，2 秒后重试...\n";
        else
            std::cout << "[ADB] ADB Server 启动失败，2 秒后重试...\n";

        lastReady = false;
        return false;
    }

    // 删除旧 Forward，失败也没关系
    RunAdb(adbPath, L"forward --remove tcp:5066", exitCode);

    // 创建新的 Forward
    if (!RunAdb(adbPath, L"forward tcp:5066 tcp:5066", exitCode) || exitCode != 0)
    {
        if (lastReady)
            std::cout << "\n[ADB] 手机未连接或 Forward 失败，2 秒后重试...\n";
        else
            std::cout << "[ADB] 手机未连接或 Forward 失败，2 秒后重试...\n";

        lastReady = false;
        return false;
    }

    if (!lastReady)
        std::cout << "[ADB] Port Forward OK.\n";

    lastReady = true;
    return true;
}

// 创建虚拟 Xbox 360 手柄
bool CreateVirtualController(PVIGEM_CLIENT& client, PVIGEM_TARGET& controller)
{
    client = nullptr;
    controller = nullptr;

    client = vigem_alloc();

    if (client == nullptr)
    {
        std::cout << "[ViGEm] vigem_alloc() 失败，可能没有正确安装 ViGEmBus。\n";
        return false;
    }

    VIGEM_ERROR error = vigem_connect(client);

    if (!VIGEM_SUCCESS(error))
    {
        std::cout << "[ViGEm] 连接驱动失败，错误码: " << error << "\n";
        vigem_free(client);
        client = nullptr;
        return false;
    }

    controller = vigem_target_x360_alloc();

    if (controller == nullptr)
    {
        std::cout << "[ViGEm] 创建 Xbox 360 Controller 失败。\n";
        vigem_disconnect(client);
        vigem_free(client);
        client = nullptr;
        return false;
    }

    error = vigem_target_add(client, controller);

    if (!VIGEM_SUCCESS(error))
    {
        std::cout << "[ViGEm] 添加虚拟手柄失败: " << error << "\n";
        vigem_target_free(controller);
        vigem_disconnect(client);
        vigem_free(client);
        controller = nullptr;
        client = nullptr;
        return false;
    }

    std::cout << "[ViGEm] Virtual Xbox 360 Controller created!\n";
    return true;
}

// 删除虚拟手柄
void DestroyVirtualController(PVIGEM_CLIENT& client, PVIGEM_TARGET& controller)
{
    if (client != nullptr && controller != nullptr)
        vigem_target_remove(client, controller);

    if (controller != nullptr)
    {
        vigem_target_free(controller);
        controller = nullptr;
    }

    if (client != nullptr)
    {
        vigem_disconnect(client);
        vigem_free(client);
        client = nullptr;
    }
}

// 在固定的控制台行刷新调试信息
void PrintDebugLine(
    bool A, bool B, bool X, bool Y,
    bool LB, bool RB,
    bool LT, bool RT,
    bool L3, bool R3,
    bool DPadUp, bool DPadDown,
    bool DPadLeft, bool DPadRight,
    float LX, float LY,
    float RX, float RY)
{
    static bool initialized = false;
    static COORD debugPosition{};

    HANDLE console = GetStdHandle(STD_OUTPUT_HANDLE);

    if (!initialized)
    {
        CONSOLE_SCREEN_BUFFER_INFO info{};

        if (GetConsoleScreenBufferInfo(console, &info))
        {
            debugPosition = info.dwCursorPosition;
            initialized = true;
        }
    }

    if (!initialized)
        return;

    COORD oldPosition{};

    CONSOLE_SCREEN_BUFFER_INFO info{};
    if (GetConsoleScreenBufferInfo(console, &info))
        oldPosition = info.dwCursorPosition;

    SetConsoleCursorPosition(console, debugPosition);

    std::cout
        << "A=" << (A ? 1 : 0)
        << " B=" << (B ? 1 : 0)
        << " X=" << (X ? 1 : 0)
        << " Y=" << (Y ? 1 : 0)
        << " | LB=" << (LB ? 1 : 0)
        << " RB=" << (RB ? 1 : 0)
        << " | LT=" << (LT ? 1 : 0)
        << " RT=" << (RT ? 1 : 0)
        << " | L3=" << (L3 ? 1 : 0)
        << " R3=" << (R3 ? 1 : 0)
        << " | DPad="
        << (DPadUp ? "U" : "-")
        << (DPadDown ? "D" : "-")
        << (DPadLeft ? "L" : "-")
        << (DPadRight ? "R" : "-")
        << " | LX=" << LX
        << " LY=" << LY
        << " | RX=" << RX
        << " RY=" << RY
        << "                         "
        << std::flush;

    SetConsoleCursorPosition(console, oldPosition);
}

// 把 JSON 状态转换成 Xbox 报告
bool UpdateControllerFromJson(
    const json& data,
    XUSB_REPORT& report,
    int& debugCounter)
{
    bool A = data.value("A", false);
    bool B = data.value("B", false);
    bool X = data.value("X", false);
    bool Y = data.value("Y", false);

    bool LB = data.value("LB", false);
    bool RB = data.value("RB", false);

    bool Start = data.value("Start", false);
    bool Back = data.value("Back", false);

    bool DPadUp = data.value("DPadUp", false);
    bool DPadDown = data.value("DPadDown", false);
    bool DPadLeft = data.value("DPadLeft", false);
    bool DPadRight = data.value("DPadRight", false);

    bool L3 = data.value("L3", false);
    bool R3 = data.value("R3", false);

    bool LT = data.value("LT", false);
    bool RT = data.value("RT", false);

    float LX = data.value("LX", 0.0f);
    float LY = data.value("LY", 0.0f);
    float RX = data.value("RX", 0.0f);
    float RY = data.value("RY", 0.0f);

    // ABXY
    SetButton(report.wButtons, A, XUSB_GAMEPAD_A);
    SetButton(report.wButtons, B, XUSB_GAMEPAD_B);
    SetButton(report.wButtons, X, XUSB_GAMEPAD_X);
    SetButton(report.wButtons, Y, XUSB_GAMEPAD_Y);

    // LB / RB
    SetButton(report.wButtons, LB, XUSB_GAMEPAD_LEFT_SHOULDER);
    SetButton(report.wButtons, RB, XUSB_GAMEPAD_RIGHT_SHOULDER);

    // Start / Back
    SetButton(report.wButtons, Start, XUSB_GAMEPAD_START);
    SetButton(report.wButtons, Back, XUSB_GAMEPAD_BACK);

    // D-Pad
    SetButton(report.wButtons, DPadUp, XUSB_GAMEPAD_DPAD_UP);
    SetButton(report.wButtons, DPadDown, XUSB_GAMEPAD_DPAD_DOWN);
    SetButton(report.wButtons, DPadLeft, XUSB_GAMEPAD_DPAD_LEFT);
    SetButton(report.wButtons, DPadRight, XUSB_GAMEPAD_DPAD_RIGHT);

    // L3 / R3
    SetButton(report.wButtons, L3, XUSB_GAMEPAD_LEFT_THUMB);
    SetButton(report.wButtons, R3, XUSB_GAMEPAD_RIGHT_THUMB);

    // LT / RT：按下 255，松开 0
    report.bLeftTrigger = LT ? 255 : 0;
    report.bRightTrigger = RT ? 255 : 0;

    // 摇杆：Y 轴反向
    report.sThumbLX = StickToXInput(LX);
    report.sThumbLY = StickToXInput(-LY);
    report.sThumbRX = StickToXInput(RX);
    report.sThumbRY = StickToXInput(-RY);
    // 每 5 帧显示一次调试信息
    if (DEBUG_OUTPUT)
    {
        debugCounter++;

        if (debugCounter >= 5)
        {
            debugCounter = 0;

            PrintDebugLine(
                A, B, X, Y,
                LB, RB,
                LT, RT,
                L3, R3,
                DPadUp, DPadDown,
                DPadLeft, DPadRight,
                LX, LY,
                RX, RY);
        }
    }

    return true;
}

int main()
{
    std::cout << "PhoneGamepad Receiver\n";

    // 初始化 Winsock
    WSADATA wsaData{};
    int result = WSAStartup(MAKEWORD(2, 2), &wsaData);

    if (result != 0)
    {
        std::cout << "[Socket] WSAStartup 失败: " << result << "\n";
        std::cout << "程序保持运行，2 秒后重试...\n";

        while (true)
            std::this_thread::sleep_for(std::chrono::seconds(RETRY_SECONDS));
    }

    PVIGEM_CLIENT client = nullptr;
    PVIGEM_TARGET controller = nullptr;

    // 主循环：永远不主动退出
    while (true)
    {
        // 没有虚拟手柄就尝试创建
        if (controller == nullptr)
        {
            if (!CreateVirtualController(client, controller))
            {
                std::cout << "[ViGEm] 2 秒后重试...\n\n";
                std::this_thread::sleep_for(std::chrono::seconds(RETRY_SECONDS));
                continue;
            }
        }

        // ADB Forward
        if (!EnsureAdbForward())
        {
            std::this_thread::sleep_for(std::chrono::seconds(RETRY_SECONDS));
            continue;
        }

        // 创建 TCP Socket
        SOCKET sock = socket(AF_INET, SOCK_STREAM, IPPROTO_TCP);

        if (sock == INVALID_SOCKET)
        {
            std::cout << "[Socket] socket() 失败: " << WSAGetLastError() << "\n";
            std::this_thread::sleep_for(std::chrono::seconds(RETRY_SECONDS));
            continue;
        }

        sockaddr_in serverAddress{};
        serverAddress.sin_family = AF_INET;
        serverAddress.sin_port = htons(TCP_PORT);
        serverAddress.sin_addr.s_addr = htonl(INADDR_LOOPBACK);

        std::cout << "[Socket] Connecting to phone...\n";

        // 连接手机
        result = connect(
            sock,
            reinterpret_cast<sockaddr*>(&serverAddress),
            sizeof(serverAddress)
        );

        if (result == SOCKET_ERROR)
        {
            std::cout
                << "[Socket] 手机尚未连接，2 秒后重试。错误码: "
                << WSAGetLastError()
                << "\n";

            closesocket(sock);
            std::this_thread::sleep_for(std::chrono::seconds(RETRY_SECONDS));
            continue;
        }

        std::cout << "[Socket] Connected to phone!\n\n";

        XUSB_REPORT report{};
        std::string receiveBuffer;
        char buffer[4096];
        int debugCounter = 0;
        bool connectionLost = false;

        // 手机连接后的接收循环
        while (true)
        {
            int bytesReceived = recv(sock, buffer, sizeof(buffer), 0);

            if (bytesReceived > 0)
            {
                receiveBuffer.append(buffer, bytesReceived);

                size_t newlinePos;

                while ((newlinePos = receiveBuffer.find('\n')) != std::string::npos)
                {
                    std::string line = receiveBuffer.substr(0, newlinePos);
                    receiveBuffer.erase(0, newlinePos + 1);

                    if (line.empty())
                        continue;

                    try
                    {
                        json data = json::parse(line);

                        if (!UpdateControllerFromJson(data, report, debugCounter))
                            continue;

                        VIGEM_ERROR updateResult =
                            vigem_target_x360_update(client, controller, report);

                        if (!VIGEM_SUCCESS(updateResult))
                        {
                            std::cout
                                << "\n[ViGEm] 更新虚拟手柄失败，错误码: "
                                << updateResult
                                << "\n";

                            connectionLost = true;
                            break;
                        }
                    }
                    catch (const std::exception& e)
                    {
                        std::cout
                            << "\n[JSON] Parse error: "
                            << e.what()
                            << "\n";
                    }
                }

                if (connectionLost)
                    break;
            }
            else if (bytesReceived == 0)
            {
                std::cout << "\n[Socket] Phone disconnected.\n";
                connectionLost = true;
                break;
            }
            else
            {
                std::cout
                    << "\n[Socket] recv() 失败: "
                    << WSAGetLastError()
                    << "\n";

                connectionLost = true;
                break;
            }
        }

        // 手机断开后立即释放所有按键
        report = {};

        if (controller != nullptr && client != nullptr)
            vigem_target_x360_update(client, controller, report);

        closesocket(sock);

        std::cout << "[Socket] 2 秒后尝试重新连接...\n\n";
        std::this_thread::sleep_for(std::chrono::seconds(RETRY_SECONDS));
    }

    // 理论上不会执行到这里
    DestroyVirtualController(client, controller);
    WSACleanup();

    return 0;
}