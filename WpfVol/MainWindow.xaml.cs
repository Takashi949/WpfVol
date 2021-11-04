using NAudio.CoreAudioApi;
using System;
using System.Diagnostics;
using System.Windows;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Devices.Enumeration;
using Windows.Storage.Streams;

namespace WpfVol
{
    /// <summary>
    /// MainWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class MainWindow : Window
    {
        const int sampleNum = 100;
        const float maxValue = 3100.0f;

        AudioSessionControl audioSes = null;
        MMDeviceEnumerator mde;
        bool isAEVSetted = false;
        float s1, s2;
        uint counter = 0;

        Guid servUuids = new Guid("4fafc201-1fb5-459e-8fcc-c5c9c331916c");
        Guid characUuids = new Guid("beb5483e-36e1-4688-b7f5-ea07361b2679");
        GattCharacteristic gattCharacteristic;
        DeviceWatcher DeviceWatcher;
        DeviceInformation deviceInformation;
        BluetoothLEDevice device;
        bool isReadable = false;

        public void Start()
        {
            string selector = "(" + GattDeviceService.GetDeviceSelectorFromUuid(this.servUuids) + ")";

            // ウォッチャー(機器を監視、検索するやつ)を作成
            // private DeviceWatcher DeviceWatcher { get; set; }
            DeviceWatcher = DeviceInformation.CreateWatcher(selector);

            // デバイス情報更新時のハンドラを登録
            DeviceWatcher.Added += Watcher_DeviceAdded;

            // ウォッチャーをスタート(検索開始)
            DeviceWatcher.Start();

            Debug.WriteLine("startadv");
        }
        private async void Watcher_DeviceAdded(DeviceWatcher sender, DeviceInformation deviceInfo)
        {
            Debug.WriteLine("found" + deviceInfo.Name.ToString());
            if (deviceInfo.Name == "ESP32_BLE_jst1")
            {
                Debug.WriteLine("esp32 found");
                // デバイス情報を保存
                deviceInformation = deviceInfo;

                // デバイス情報更新時のハンドラを解除しウォッチャーをストップ
                DeviceWatcher.Added -= Watcher_DeviceAdded;
                DeviceWatcher.Stop();

                device = await BluetoothLEDevice.FromIdAsync(deviceInformation.Id);
                var services = await device.GetGattServicesForUuidAsync(this.servUuids);
                var characteristics = await services.Services[0].GetCharacteristicsForUuidAsync(this.characUuids);

                gattCharacteristic = characteristics.Characteristics[0];

                isReadable = true;
                this.notifyrecv();
            }
        }

        private async void Characteristic_ValueChanged(GattCharacteristic sender, GattValueChangedEventArgs args)
        {
            if (!isAEVSetted)
            {
                mde = new MMDeviceEnumerator();
                isAEVSetted = true;
            }

            var reader = DataReader.FromBuffer(args.CharacteristicValue);
            byte[] input = new byte[reader.UnconsumedBufferLength];
            reader.ReadBytes(input);
            UInt16 in1 = BitConverter.ToUInt16(input, 0);
            UInt16 in2 = BitConverter.ToUInt16(input, 2);
            Debug.WriteLine(in1 + "+" + in2);

            if(counter < sampleNum)
            {
                s1 += in1;
                s2 += in2;
                ++counter;
            }
            else
            {
                counter = 0;
                float f1 = s1 / (float)sampleNum / maxValue;
                float f2 = s2 / (float)sampleNum / maxValue;
                s1 = 0;
                s2 = 0;
                mde.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia)
                   .AudioEndpointVolume.MasterVolumeLevelScalar = f1;
                audioSes.SimpleAudioVolume.Volume = f2;
            }
        }

        private async void notifyrecv()
        {
            try
            {
                gattCharacteristic.ValueChanged += this.Characteristic_ValueChanged;
                Debug.WriteLine("Successfully");
                await this.gattCharacteristic.WriteClientCharacteristicConfigurationDescriptorAsync(GattClientCharacteristicConfigurationDescriptorValue.Notify);
            }
            catch (Exception e)
            {
                Debug.WriteLine(e.Message);
            }
        }

        public MainWindow()
        {
            InitializeComponent();
            SessionCollection sessionCollection = new MMDeviceEnumerator()
                .GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia)
                .AudioSessionManager.Sessions;
            for (int i = 0; i < sessionCollection.Count; i++)
            {
                audioSes = sessionCollection[i];
                Process p = Process.GetProcessById((int)audioSes.GetProcessID);
                Debug.WriteLine(p.MainWindowTitle);
                if (p.MainWindowTitle.Contains("Firefox")) {
                    break;
                }
                //YouTube? Mozilla Firefox
            }
            this.Start();
        }
    }
}
