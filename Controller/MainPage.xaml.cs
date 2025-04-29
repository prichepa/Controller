using Plugin.BLE;
using Plugin.BLE.Abstractions.Contracts;
using System.Text;

namespace Controller
{
    public partial class MainPage : ContentPage
    {
        double _x, _y;
        const double JoystickRadius = 100;
        const double KnobRadius = 30;

        string currentDir = "0";
        bool isWriting = false;

        IBluetoothLE ble;
        IAdapter adapter;
        IDevice? connectedDevice;
        ICharacteristic? writeChar;

        public MainPage()
        {
            InitializeComponent();

            ble = CrossBluetoothLE.Current;
            adapter = CrossBluetoothLE.Current.Adapter;
        }

        async void OnConnectBtnClicked(object sender, EventArgs e)
        {
            try
            {
                exLabel.Text = "Searching...";

                adapter.DeviceDiscovered += async (s, a) =>
                {
                    if (a.Device.Name == "HMSoft") // Название модуля
                    {
                        exLabel.Text = "Connecting...";

                        await adapter.StopScanningForDevicesAsync(); // Останавливаем сканирование перед подключением
                        await adapter.ConnectToDeviceAsync(a.Device);

                        var services = await a.Device.GetServicesAsync();
                        var service = services.FirstOrDefault(s => s.Id.ToString().ToLower().Contains("ffe0")); // Найди нужный service
                        if (service == null)
                        {
                            exLabel.Text = "Connection error";
                            return;
                        }

                        var characteristics = await service.GetCharacteristicsAsync();
                        writeChar = characteristics.FirstOrDefault(c => c.Id.ToString().ToLower().Contains("ffe1"));

                        if (writeChar != null)
                        {
                            exLabel.Text = "Connected";
                        }
                        else
                        {
                            exLabel.Text = "Connection error";
                        }

                        connectedDevice = a.Device;
                    }
                };

                await adapter.StartScanningForDevicesAsync();
            }
            catch (Exception ex)
            {
                exLabel.Text = ex.Message;
            }
        }

        void OnPanUpdated(object sender, PanUpdatedEventArgs e)
        {
            switch (e.StatusType)
            {
                case GestureStatus.Started:
                    _x = JoystickKnob.TranslationX;
                    _y = JoystickKnob.TranslationY;
                    break;

                case GestureStatus.Running:
                    double rawX = _x + e.TotalX;
                    double rawY = _y + e.TotalY;

                    // Ограничение в круге
                    double maxDistance = JoystickRadius - KnobRadius;
                    double distance = Math.Sqrt(rawX * rawX + rawY * rawY);

                    if (distance > maxDistance)
                    {
                        double scale = maxDistance / distance;
                        rawX *= scale;
                        rawY *= scale;
                    }

                    // Плавное сглаживание
                    const double smoothing = 0.2;
                    double newX = JoystickKnob.TranslationX + (rawX - JoystickKnob.TranslationX) * smoothing;
                    double newY = JoystickKnob.TranslationY + (rawY - JoystickKnob.TranslationY) * smoothing;

                    JoystickKnob.TranslationX = newX;
                    JoystickKnob.TranslationY = newY;
                    posLabel.Text = $"X: {Math.Round(newX)} | Y: {Math.Round(newY)}";
                    SendCommand(newX, newY);

                    break;

                case GestureStatus.Completed:
                case GestureStatus.Canceled:
                    // Возврат в центр (если нужно)
                    JoystickKnob.TranslationX = 0;
                    JoystickKnob.TranslationY = 0;
                    posLabel.Text = "X: 0 | Y: 0";
                    moveLabel.Text = "🛑";
                    SendCommand(0, 0);
                    break;
            }
        }

        async void SendCommand(double newX, double newY)
        {
            if (isWriting) return;

            if (writeChar == null)
            {
                exLabel.Text = "Not connected";
                return;
            }

            try
            {
                isWriting = true;

                string command = GetCommand(newX, newY);
                if (!string.IsNullOrEmpty(command))
                {
                    await writeChar.WriteAsync(Encoding.UTF8.GetBytes(command));
                    await Task.Delay(100);
                }
            }
            finally
            {
                isWriting = false;
            }
        }

        string GetCommand(double newX, double newY)
        {
            if(writeChar == null)
            {
                exLabel.Text = "Not connected";
                return "";
            }

            double threshold = 30;

            string newDir;
            if (Math.Abs(newX) < threshold && newY < -threshold)
            {
                moveLabel.Text = "⬆";
                newDir = "1";
            }
            else if (Math.Abs(newX) < threshold && newY > threshold)
            {
                moveLabel.Text = "⬇";
                newDir = "2";
            }
            else if (newX < -threshold && Math.Abs(newY) < threshold)
            {
                moveLabel.Text = "⬅";
                newDir = "3";
            }
            else if (newX > threshold && Math.Abs(newY) < threshold)
            {
                moveLabel.Text = "➡";
                newDir = "4";
            }
            else if (newX < -threshold && newY < -threshold)
            {
                moveLabel.Text = "↖";
                newDir = "5";
            }
            else if (newX > threshold && newY < -threshold)
            {
                moveLabel.Text = "↗";
                newDir = "6";
            }
            else if (newX < -threshold && newY > threshold)
            {
                moveLabel.Text = "↙";
                newDir = "7";
            }
            else if (newX > threshold && newY > threshold)
            {
                moveLabel.Text = "↘";
                newDir = "8";
            }
            else
            {
                moveLabel.Text = "🛑";
                newDir = "0";
            }

            if (currentDir != newDir)
            {
                currentDir = newDir;
                return newDir;
            }
            return "";
        }
    }
}