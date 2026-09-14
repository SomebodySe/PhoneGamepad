#include <iostream>
#include <string>
#include <winsock2.h>
#include <windows.h>

#include <ViGEm/Client.h>
#include <nlohmann/json.hpp>

#pragma comment(lib, "ws2_32.lib")
#pragma comment(lib, "ViGEmClient.lib")
#pragma comment(lib, "SetupAPI.lib")

using json = nlohmann::json;


// ============================================================
// 把 [-1, 1] 转换成 XInput 摇杆范围
// ============================================================

SHORT StickToXInput(float value)
{
    if (value > 1.0f)
        value = 1.0f;

    if (value < -1.0f)
        value = -1.0f;

    if (value >= 0.0f)
    {
        return static_cast<SHORT>(
            value * 32767.0f
            );
    }
    else
    {
        return static_cast<SHORT>(
            value * 32768.0f
            );
    }
}


// ============================================================
// bool 转 Xbox Button
// ============================================================

void SetButton(
    WORD& buttons,
    bool pressed,
    WORD button)
{
    if (pressed)
    {
        buttons |= button;
    }
    else
    {
        buttons &= ~button;
    }
}


int main()
{
    std::cout << "PhoneGamepad Receiver\n";


    // ========================================================
    // 1. 初始化 ViGEm
    // ========================================================

    PVIGEM_CLIENT client = vigem_alloc();

    if (client == nullptr)
    {
        std::cout << "vigem_alloc() failed.\n";
        return 1;
    }

    VIGEM_ERROR error = vigem_connect(client);

    if (!VIGEM_SUCCESS(error))
    {
        std::cout
            << "vigem_connect() failed: "
            << error
            << std::endl;

        vigem_free(client);
        return 1;
    }

    std::cout << "Connected to ViGEmBus.\n";


    // ========================================================
    // 2. 创建虚拟 Xbox 360 Controller
    // ========================================================

    PVIGEM_TARGET controller =
        vigem_target_x360_alloc();

    if (controller == nullptr)
    {
        std::cout
            << "vigem_target_x360_alloc() failed.\n";

        vigem_disconnect(client);
        vigem_free(client);

        return 1;
    }

    error = vigem_target_add(
        client,
        controller
    );

    if (!VIGEM_SUCCESS(error))
    {
        std::cout
            << "vigem_target_add() failed: "
            << error
            << std::endl;

        vigem_target_free(controller);
        vigem_disconnect(client);
        vigem_free(client);

        return 1;
    }

    std::cout
        << "Virtual Xbox 360 Controller created!\n";


    // ========================================================
    // 3. 初始化 Winsock
    // ========================================================

    WSADATA wsaData;

    int result = WSAStartup(
        MAKEWORD(2, 2),
        &wsaData
    );

    if (result != 0)
    {
        std::cout
            << "WSAStartup failed: "
            << result
            << std::endl;

        vigem_target_remove(
            client,
            controller
        );

        vigem_target_free(controller);
        vigem_disconnect(client);
        vigem_free(client);

        return 1;
    }


    // ========================================================
    // 4. 创建 TCP Socket
    // ========================================================

    SOCKET sock = socket(
        AF_INET,
        SOCK_STREAM,
        IPPROTO_TCP
    );

    if (sock == INVALID_SOCKET)
    {
        std::cout
            << "socket() failed.\n";

        WSACleanup();

        vigem_target_remove(
            client,
            controller
        );

        vigem_target_free(controller);
        vigem_disconnect(client);
        vigem_free(client);

        return 1;
    }


    // ========================================================
    // 5. 连接手机
    // ========================================================

    sockaddr_in serverAddress{};

    serverAddress.sin_family =
        AF_INET;

    serverAddress.sin_port =
        htons(5066);

    serverAddress.sin_addr.s_addr =
        htonl(INADDR_LOOPBACK);


    std::cout
        << "Connecting to phone...\n";


    result = connect(
        sock,
        reinterpret_cast<sockaddr*>(
            &serverAddress
            ),
        sizeof(serverAddress)
    );

    if (result == SOCKET_ERROR)
    {
        std::cout
            << "connect() failed: "
            << WSAGetLastError()
            << std::endl;

        closesocket(sock);
        WSACleanup();

        vigem_target_remove(
            client,
            controller
        );

        vigem_target_free(controller);
        vigem_disconnect(client);
        vigem_free(client);

        return 1;
    }

    std::cout
        << "Connected to phone!\n";


    // ========================================================
    // 6. Xbox Controller 当前状态
    // ========================================================

    XUSB_REPORT report{};


    // ========================================================
    // 7. 接收手机 JSON
    // ========================================================

    char buffer[4096];

    std::string receiveBuffer;


    while (true)
    {
        int bytesReceived = recv(
            sock,
            buffer,
            sizeof(buffer),
            0
        );


        // ====================================================
        // 收到数据
        // ====================================================

        if (bytesReceived > 0)
        {
            receiveBuffer.append(
                buffer,
                bytesReceived
            );


            // TCP 是字节流
            // 按 \n 分割 JSON

            size_t newlinePos;


            while (
                (newlinePos =
                    receiveBuffer.find('\n'))
                != std::string::npos
                )
            {
                std::string line =
                    receiveBuffer.substr(
                        0,
                        newlinePos
                    );


                receiveBuffer.erase(
                    0,
                    newlinePos + 1
                );


                if (line.empty())
                    continue;


                try
                {
                    // ========================================
                    // JSON
                    // ========================================

                    json data =
                        json::parse(line);


                    // ========================================
                    // ABXY
                    // ========================================

                    bool A =
                        data.value(
                            "A",
                            false
                        );

                    bool B =
                        data.value(
                            "B",
                            false
                        );

                    bool X =
                        data.value(
                            "X",
                            false
                        );

                    bool Y =
                        data.value(
                            "Y",
                            false
                        );


                    SetButton(
                        report.wButtons,
                        A,
                        XUSB_GAMEPAD_A
                    );

                    SetButton(
                        report.wButtons,
                        B,
                        XUSB_GAMEPAD_B
                    );

                    SetButton(
                        report.wButtons,
                        X,
                        XUSB_GAMEPAD_X
                    );

                    SetButton(
                        report.wButtons,
                        Y,
                        XUSB_GAMEPAD_Y
                    );


                    // ========================================
                    // LB / RB
                    // ========================================

                    bool LB =
                        data.value(
                            "LB",
                            false
                        );

                    bool RB =
                        data.value(
                            "RB",
                            false
                        );


                    SetButton(
                        report.wButtons,
                        LB,
                        XUSB_GAMEPAD_LEFT_SHOULDER
                    );

                    SetButton(
                        report.wButtons,
                        RB,
                        XUSB_GAMEPAD_RIGHT_SHOULDER
                    );


                    // ========================================
                    // Start / Back
                    // ========================================

                    bool Start =
                        data.value(
                            "Start",
                            false
                        );

                    bool Back =
                        data.value(
                            "Back",
                            false
                        );


                    SetButton(
                        report.wButtons,
                        Start,
                        XUSB_GAMEPAD_START
                    );

                    SetButton(
                        report.wButtons,
                        Back,
                        XUSB_GAMEPAD_BACK
                    );


                    // ========================================
                    // D-Pad
                    // ========================================

                    bool DPadUp =
                        data.value(
                            "DPadUp",
                            false
                        );

                    bool DPadDown =
                        data.value(
                            "DPadDown",
                            false
                        );

                    bool DPadLeft =
                        data.value(
                            "DPadLeft",
                            false
                        );

                    bool DPadRight =
                        data.value(
                            "DPadRight",
                            false
                        );

                    // ========================================
// L3 / R3
// ========================================

                    bool L3 =
                        data.value(
                            "L3",
                            false
                        );

                    bool R3 =
                        data.value(
                            "R3",
                            false
                        );

                    SetButton(
                        report.wButtons,
                        L3,
                        XUSB_GAMEPAD_LEFT_THUMB
                    );

                    SetButton(
                        report.wButtons,
                        R3,
                        XUSB_GAMEPAD_RIGHT_THUMB
                    );


                    SetButton(
                        report.wButtons,
                        DPadUp,
                        XUSB_GAMEPAD_DPAD_UP
                    );

                    SetButton(
                        report.wButtons,
                        DPadDown,
                        XUSB_GAMEPAD_DPAD_DOWN
                    );

                    SetButton(
                        report.wButtons,
                        DPadLeft,
                        XUSB_GAMEPAD_DPAD_LEFT
                    );

                    SetButton(
                        report.wButtons,
                        DPadRight,
                        XUSB_GAMEPAD_DPAD_RIGHT
                    );


                    // ========================================
                    // LT / RT
                    //
                    // 用户要求：
                    // 不模拟按压深度
                    //
                    // 按下 = 255
                    // 松开 = 0
                    // ========================================

                    bool LT =
                        data.value(
                            "LT",
                            false
                        );

                    bool RT =
                        data.value(
                            "RT",
                            false
                        );


                    report.bLeftTrigger =
                        LT ? 255 : 0;

                    report.bRightTrigger =
                        RT ? 255 : 0;


                    // ========================================
                    // 左摇杆
                    // ========================================

                    float LX =
                        data.value(
                            "LX",
                            0.0f
                        );

                    float LY =
                        data.value(
                            "LY",
                            0.0f
                        );


                    report.sThumbLX =
                        StickToXInput(LX);

                    report.sThumbLY =
                        StickToXInput(-LY);


                    // ========================================
                    // 右摇杆
                    // ========================================

                    float RX =
                        data.value(
                            "RX",
                            0.0f
                        );

                    float RY =
                        data.value(
                            "RY",
                            0.0f
                        );


                    report.sThumbRX =
                        StickToXInput(RX);

                    report.sThumbRY =
                        StickToXInput(-RY);


                    // ========================================
                    // 更新虚拟 Xbox 手柄
                    // ========================================

                    static int debugCounter = 0;

                    debugCounter++;

                    if (debugCounter >= 5)
                    {
                        debugCounter = 0;

                        std::cout
                            << "\r"
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
                    }

                    VIGEM_ERROR updateResult =
                        vigem_target_x360_update(
                            client,
                            controller,
                            report
                        );


                    if (!VIGEM_SUCCESS(updateResult))
                    {
                        std::cout
                            << "Update failed: "
                            << updateResult
                            << std::endl;
                    }
                }
                catch (
                    const std::exception& e
                    )
                {
                    std::cout
                        << "JSON parse error: "
                        << e.what()
                        << std::endl;
                }
            }
        }


        // ====================================================
        // 手机断开
        // ====================================================

        else if (bytesReceived == 0)
        {
            std::cout
                << "Phone disconnected."
                << std::endl;

            break;
        }


        // ====================================================
        // recv 错误
        // ====================================================

        else
        {
            std::cout
                << "recv() failed: "
                << WSAGetLastError()
                << std::endl;

            break;
        }
    }


    // ========================================================
    // 8. 清理
    // ========================================================

    closesocket(sock);

    WSACleanup();


    vigem_target_remove(
        client,
        controller
    );

    vigem_target_free(
        controller
    );

    vigem_disconnect(
        client
    );

    vigem_free(
        client
    );


    return 0;
}