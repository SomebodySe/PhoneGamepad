
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace PhoneGamepad;

public class TcpGamepadServer
{
    private TcpListener? listener;

    public async Task StartAsync(int port = 5066)
    {
        try
        {
            listener = new TcpListener(
                IPAddress.Loopback,
                port);

            listener.Start();

            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                await Application.Current!.MainPage!.DisplayAlert(
                    "TCP TEST",
                    "正在等待 Windows 连接",
                    "OK");
            });

            while (true)
            {
                TcpClient client =
                    await listener.AcceptTcpClientAsync();

                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    await Application.Current!.MainPage!.DisplayAlert(
                        "TCP TEST",
                        "连接成功",
                        "OK");
                });

                _ = HandleClientAsync(client);
            }
        }
        catch (Exception ex)
        {
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                await Application.Current!.MainPage!.DisplayAlert(
                    "TCP ERROR",
                    ex.ToString(),
                    "OK");
            });
        }
    }


    private async Task HandleClientAsync(TcpClient client)
    {
        try
        {
            using (client)
            using (NetworkStream stream = client.GetStream())
            using (StreamWriter writer = new StreamWriter(
                stream,
                new UTF8Encoding(false))
            {
                AutoFlush = true
            })
            {
                Console.WriteLine("Windows connected.");

                while (true)
                {
                    Console.WriteLine("Preparing gamepad state...");

                    GamepadState state = GetGamepadState();

                    Console.WriteLine("Gamepad state OK.");

                    string json = JsonSerializer.Serialize(state);

                    Console.WriteLine($"Sending: {json}");

                    await writer.WriteLineAsync(json);

                    Console.WriteLine("Send OK.");

                    await Task.Delay(20);
                }
            }
        }
        catch (IOException ex)
        {
            Console.WriteLine($"TCP IOException: {ex}");
        }
        catch (SocketException ex)
        {
            Console.WriteLine($"TCP SocketException: {ex}");
        }
        catch (ObjectDisposedException ex)
        {
            Console.WriteLine($"TCP ObjectDisposedException: {ex}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"TCP Client Error: {ex}");
        }
        finally
        {
            Console.WriteLine("Windows connection ended.");
        }
    }


    private GamepadState GetGamepadState()
    {
        return new GamepadState
        {
            A = MainPage.CurrentGamepad.A,
            B = MainPage.CurrentGamepad.B,
            X = MainPage.CurrentGamepad.X,
            Y = MainPage.CurrentGamepad.Y,

            LB = MainPage.CurrentGamepad.LB,
            RB = MainPage.CurrentGamepad.RB,

            LX = MainPage.CurrentGamepad.LX,
            LY = MainPage.CurrentGamepad.LY,

            RX = MainPage.CurrentGamepad.RX,
            RY = MainPage.CurrentGamepad.RY,

            L3 = MainPage.CurrentGamepad.L3,
            R3 = MainPage.CurrentGamepad.R3,

            LT = MainPage.CurrentGamepad.LT,
            RT = MainPage.CurrentGamepad.RT,

            Start = MainPage.CurrentGamepad.Start,
            Back = MainPage.CurrentGamepad.Back,

            DPadUp =
                MainPage.CurrentGamepad.DPadUp,

            DPadDown =
                MainPage.CurrentGamepad.DPadDown,

            DPadLeft =
                MainPage.CurrentGamepad.DPadLeft,

            DPadRight =
                MainPage.CurrentGamepad.DPadRight
        };
    }
}

