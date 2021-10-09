// Copyright RoSchmi April 2020, License: Apache v 2.0
// This App shows GHI SC20260D Dev Board reacting on commands sent
// via MQTT-protocol using AWS-IoT Shadow service 

// https://docs.aws.amazon.com/iot/latest/developerguide/iot-device-shadows.html

// If commented out, ENC28 Ethernet Module is used
#define UseWifiModule

using System;
using System.Text;
using System.Threading;
using GHIElectronics.TinyCLR.Networking.Mqtt;
using System.Net;
using GHIElectronics.TinyCLR.Pins;
using GHIElectronics.TinyCLR.Devices.Network;
using GHIElectronics.TinyCLR.Devices.Gpio;
using GHIElectronics.TinyCLR.Devices.Spi;
using GHIElectronics.TinyCLR.Data.Json;
using GHIElectronics.TinyCLR.Devices.Display;
using System.Drawing;
using System.Security.Cryptography.X509Certificates;
using System.Diagnostics;
using RoSchmi.AWS.Models;


namespace SC20260_AWS_Test_01
{
    class Program
    {
        private static GpioPin ButtonApp;

        private static bool linkReady = false;

        private static NetworkController networkController;

        static System.Drawing.Graphics screen;


        // Set your WiFi Credentials here or store them in the Resources
        static string wiFiSSID_1 = ResourcesSecret.GetString(ResourcesSecret.StringResources.WifiSSID_1);
        //static string wiFiSSID_1 = "VirtualWiFi";

        static string wiFiKey_1 = ResourcesSecret.GetString(ResourcesSecret.StringResources.WifiKey_1);
        //static string wiFiKey_1 = "MySecretWiFiKey";

        static Mqtt iotClient = null;

        static ushort packetId = 1;

        // Name of this device in your Amazon Webservice IoT Account
        static string deviceId = "Board_01";

        static string StateReportedColor = "{\"state\":{\"reported\":{\"Leftcolor\":\"blue\", \"Rightcolor\":\"red\"}}}";
        static string message = StateReportedColor;

        static string topicShadowUpdate = string.Format("$aws/things/{0}/shadow/update", deviceId);

        static string topicShadowUpdateAccepted = string.Format("$aws/things/{0}/shadow/update/accepted", deviceId);
        static string topicShadowUpdateRejected = string.Format("$aws/things/{0}/shadow/update/rejected", deviceId);
        static string topicShadowGet = string.Format("$aws/things/{0}/shadow/get", deviceId);

        // some other topics of the Shadow service, not used in this App
        static string topicShadowUpdateDelta = string.Format("$aws/things/{0}/shadow/update/delta", deviceId);
        static string topicShadowUdateDocuments = string.Format("$aws/things/{0}/shadow/update/documents", deviceId);
        static string topicShadowUpdateTopic = string.Format("$aws/things/{0}/shadow/update/topic", deviceId);       
        static string topicShadowDeleteAccepted = string.Format("$aws/things/{0}/shadow/delete/accepted", deviceId);
        static string topicShadowDeleteRejected = string.Format("$aws/things/{0}/shadow/delete/rejected", deviceId);


        static string iotArnString = ResourcesSecret.GetString(ResourcesSecret.StringResources.iotArnString);
        // something like
        //var iotArnString = "123456789-ats.iot.eu-central-1.amazonaws.com";

        #region Region Main
        static void Main()
        {
            // Set up the 'APP' button
            ButtonApp = GpioController.GetDefault().OpenPin(SC20260.GpioPin.PB7);
            ButtonApp.SetDriveMode(GpioPinDriveMode.InputPullUp);
            ButtonApp.ValueChanged += ButtonApp_ValueChanged;

            // Set up backlight for the screen
            GpioPin backlight = GpioController.GetDefault().OpenPin(SC20260.GpioPin.PA15);
            backlight.SetDriveMode(GpioPinDriveMode.Output);
            backlight.Write(GpioPinValue.High);

            var displayController = DisplayController.GetDefault();

            // Enter the proper display configurations
            displayController.SetConfiguration(new ParallelDisplayControllerSettings
            {
                Width = 480,
                Height = 272,
                DataFormat = DisplayDataFormat.Rgb565,
                Orientation = DisplayOrientation.Degrees0, //Rotate display.
                PixelClockRate = 10000000,
                PixelPolarity = false,
                DataEnablePolarity = false,
                DataEnableIsFixed = false,
                HorizontalFrontPorch = 2,
                HorizontalBackPorch = 2,
                HorizontalSyncPulseWidth = 41,
                HorizontalSyncPolarity = false,
                VerticalFrontPorch = 2,
                VerticalBackPorch = 2,
                VerticalSyncPulseWidth = 10,
                VerticalSyncPolarity = false,
            });

            displayController.Enable(); //This line turns on the display I/O and starts
                                        //  refreshing the display. Native displays are
                                        //  continually refreshed automatically after this
                                        //  command is executed.

            screen = Graphics.FromHdc(displayController.Hdc);

            #region Region Draws some shapes on the screen
            screen.Clear();

            screen.FillEllipse(new SolidBrush(System.Drawing.Color.FromArgb  // Blue
            (255, 0, 0, 255)), 0, 0, 240, 136);

            screen.FillEllipse(new SolidBrush(System.Drawing.Color.FromArgb  // Red
                (220, 255, 80, 0)), 240, 0, 240, 136);

            screen.FillEllipse(new SolidBrush(System.Drawing.Color.FromArgb  // black
                       (255, 50, 50, 50)), 320, 30, 80, 80);

            screen.FillEllipse(new SolidBrush(System.Drawing.Color.FromArgb  // black
                       (255, 50, 50, 50)), 80, 30, 80, 80);

            
            screen.FillRectangle(new SolidBrush(Color.Teal), 20, 200, 440, 100);

            //screen.DrawRectangle(new Pen(Color.Yellow), 10, 150, 140, 100);
            
            screen.DrawEllipse(new Pen(Color.Purple), 190, 120, 100, 70);
            screen.DrawEllipse(new Pen(Color.Gray), 220, 145, 16, 16);
            screen.DrawEllipse(new Pen(Color.Gray), 245, 145, 16, 16);


            screen.DrawLine(new Pen(Color.White), 10, 271, 470, 271);
            //screen.SetPixel(240, 200, 0xFF0000);
           
            screen.Flush();
            #endregion

            #region Region Example, how a command can be created (not used in this App
            
            CommandClass command = new CommandClass()
            {
                state = new State() { desired = (State.Predicate)new State.Predicate() { Leftcolor = "blue", Rightcolor = "red" } } ,
                metadata = new Metadata() { desired = (Metadata.MetaPredicate)new Metadata.MetaPredicate() { color = (Metadata.MetaPredicate.MetaSubject)new Metadata.MetaPredicate.MetaSubject() { timestamp = 1 } } },
                version = 55,
                timestamp = 12345 
            };            
            var result = JsonConverter.Serialize(command);           
            var stringValue = result.ToString();
            var cmd = (CommandClass)JsonConverter.DeserializeObject(stringValue, typeof(CommandClass), CreateInstance);
            
            
            #endregion

#if UseWifiModule
            SetupWiFi7Click_SC20260D_MicroBus2();
#else
            SetupEnc28_SC20260D_MicroBus1();
#endif       
            DoTestAwsMqtt();
            Thread.Sleep(-1);
        }
        #endregion

        #region Method CreateInstance (needed for JSON Deserialization
        private static object CreateInstance(string path, JToken token, Type baseType, string name, int length)
        {
            if (name == "intArray")
                return new int[length];
            else if (name == "stringArray")
                return new string[length];
            else
                return null;
        }
        #endregion


        #region DoTestAwsMqtt()
        static void DoTestAwsMqtt()
        {
            var iotPort = 8883;
                      
            var caCertSource = UTF8Encoding.UTF8.GetBytes(Resources.GetString(Resources.StringResources.AmazonRootCA1));      
            var clientCertSource = ResourcesSecret.GetBytes(ResourcesSecret.BinaryResources.c9examplebc_certificate_pem);           
            var privateKeyData = UTF8Encoding.UTF8.GetBytes(ResourcesSecret.GetString(ResourcesSecret.StringResources.c9examplebc_private_pem));

            X509Certificate CaCert = new X509Certificate(caCertSource);
            X509Certificate ClientCert = new X509Certificate(clientCertSource);

            ClientCert.PrivateKey = privateKeyData;
              
            var clientSetting = new MqttClientSetting
            {
                BrokerName = iotArnString,
                BrokerPort = iotPort,
                CaCertificate = CaCert,
                ClientCertificate = ClientCert,
                SslProtocol = System.Security.Authentication.SslProtocols.Tls12
            };

            try
            {
                iotClient = new Mqtt(clientSetting);
            }  
            catch (Exception ex)  
            {
                string theMess = ex.Message;
            }

            iotClient.PublishReceivedChanged += IotClient_PublishReceivedChanged;
            
            iotClient.SubscribedChanged += (a, b) => {            
                Debug.WriteLine("Subscribed");               
                };

            iotClient.PublishedChanged += (a, b, c) => { Debug.WriteLine("Published " + a.ToString()); };

            var connectSetting = new MqttConnectionSetting
            {
                ClientId = deviceId,
                UserName = null,
                Password = null,
                KeepAliveTimeout = 60
            };

            var connectCode = iotClient.Connect(connectSetting);
               
            iotClient.Subscribe(new string[] { topicShadowUpdateAccepted }, new QoSLevel[]
                { QoSLevel.LeastOnce }, packetId++);

            iotClient.Subscribe(new string[] { topicShadowUpdateRejected }, new QoSLevel[]
                { QoSLevel.LeastOnce }, packetId++);

            iotClient.Publish(topicShadowUpdate, Encoding.UTF8.GetBytes(message),
                QoSLevel.MostOnce, false, packetId++);


            //iotClient.Subscribe(new string[] { topicShadowGet }, new QoSLevel[]
            //    { QoSLevel.LeastOnce }, packetId++);


            //iotClient.Subscribe(new string[] { topicShadowUpdate }, new QoSLevel[]
            //   { QoSLevel.LeastOnce }, packetId++);

            int dummy = 1;
        }
        #endregion

        #region Event IotClient_PublishReceivedChanged
        private static void IotClient_PublishReceivedChanged(object sender, MqttPacket packet)
        {    
            string Message = Encoding.UTF8.GetString(packet.Data);

            // RoSchmi

            //int[] intArray = new int[]{1, 2 }; 

            //var theJToken = new JArray(intArray);

            //var command = (CommandClass)JsonConverter.DeserializeObject(Message, typeof(CommandClass), CreateInstance);
            //var command = (CommandClass)JsonConverter.DeserializeObject(Message, typeof(CommandClass), new InstanceFactory(CreateInst(Message, theJToken, typeof(CommandClass), "Hello", 6)));
            var command = (CommandClass)JsonConverter.DeserializeObject(Message, typeof(CommandClass), CreateInstance);

            var theString = new String(new char[2] { 'h', 'e' });

            if (command.state.desired != null)
            {
                screen.Clear();
                screen.Flush();
                switch (command.state.desired.Rightcolor)
                {
                    case "red":
                        {
                            screen.FillEllipse(new SolidBrush(System.Drawing.Color.FromArgb  // Red
                            (255, 255, 80, 0)), 240, 0, 240, 136);

                            screen.FillEllipse(new SolidBrush(System.Drawing.Color.FromArgb  // black
                            (255, 50, 50, 50)), 280, 30, 80, 80);

                            break;
                        }
                    case "blue":
                        {
                            screen.FillEllipse(new SolidBrush(System.Drawing.Color.FromArgb  // blue
                            (255, 0, 0, 255)), 240, 0, 240, 136);

                            screen.FillEllipse(new SolidBrush(System.Drawing.Color.FromArgb  // black
                            (255, 50, 50, 50)), 360, 30, 80, 80);

                            break;
                        }

                }
                switch (command.state.desired.Leftcolor)
                {
                    case "red":
                        {
                            screen.FillEllipse(new SolidBrush(System.Drawing.Color.FromArgb  // Red
                            (220, 255, 80, 0)), 0, 0, 240, 136);

                            screen.FillEllipse(new SolidBrush(System.Drawing.Color.FromArgb  // black
                             (255, 50, 50, 50)), 120, 30, 80, 80);

                            break;
                        }
                    case "blue":
                        {
                            screen.FillEllipse(new SolidBrush(System.Drawing.Color.FromArgb  // blue
                            (255, 0, 0, 255)), 0, 0, 240, 136);

                            screen.FillEllipse(new SolidBrush(System.Drawing.Color.FromArgb  // black
                            (255, 50, 50, 50)), 40, 30, 80, 80);

                            break;
                        }
                }
                screen.FillRectangle(new SolidBrush(Color.Teal), 20, 200, 440, 100);
                screen.DrawEllipse(new Pen(Color.Purple), 190, 120, 100, 70);
                screen.DrawEllipse(new Pen(Color.Gray), 220, 145, 16, 16);
                screen.DrawEllipse(new Pen(Color.Gray), 245, 145, 16, 16);
                screen.DrawLine(new Pen(Color.White), 10, 271, 470, 271);
                screen.Flush();
            }
            Debug.WriteLine ("Received message: " + Message);
        }
        #endregion

        #region Method CreateInstance (needed for JSON Deserialization
        private static object CreateInstance(string path, string name, int length)
        {
            if (name == "intArray")
                return new int[length];
            else if (name == "stringArray")
                return new string[length];
            else
                return null;
        }
        #endregion

        #region Region Eventhandler ButtonApp_ValueChanged
        private static void ButtonApp_ValueChanged(GpioPin sender, GpioPinValueChangedEventArgs e)
        {
            if (ButtonApp.Read() == GpioPinValue.High)
            {
               // Debug.WriteLine("Button pressed");

                message = StateReportedColor;
                iotClient.Publish(topicShadowUpdate, Encoding.UTF8.GetBytes(message),
                    QoSLevel.MostOnce, false, packetId++);

                //iotClient.Publish(topicShadowGet, Encoding.UTF8.GetBytes(message),
                //    QoSLevel.MostOnce, false, packetId++);
            }
        }
        #endregion

        #region SetupEnc28_SC20260D_MicroBus1()
        static void SetupEnc28_SC20260D_MicroBus1()
        {
            networkController = NetworkController.FromName
                ("GHIElectronics.TinyCLR.NativeApis.ENC28J60.NetworkController");

            var networkInterfaceSetting = new EthernetNetworkInterfaceSettings();

            var networkCommunicationInterfaceSettings = new
                SpiNetworkCommunicationInterfaceSettings();

            var cs = GHIElectronics.TinyCLR.Devices.Gpio.GpioController.GetDefault().
            OpenPin(GHIElectronics.TinyCLR.Pins.SC20260.GpioPin.PG12);

            var settings = new GHIElectronics.TinyCLR.Devices.Spi.SpiConnectionSettings()
            {
                ChipSelectLine = cs,
                ClockFrequency = 4000000,
                Mode = GHIElectronics.TinyCLR.Devices.Spi.SpiMode.Mode0,
                ChipSelectType = GHIElectronics.TinyCLR.Devices.Spi.SpiChipSelectType.Gpio,
                ChipSelectHoldTime = TimeSpan.FromTicks(10),
                ChipSelectSetupTime = TimeSpan.FromTicks(10)                
            };

            networkCommunicationInterfaceSettings.SpiApiName =
                GHIElectronics.TinyCLR.Pins.SC20260.SpiBus.Spi3;

            networkCommunicationInterfaceSettings.GpioApiName =
                GHIElectronics.TinyCLR.Pins.SC20260.GpioPin.Id;

            networkCommunicationInterfaceSettings.SpiSettings = settings;           
            networkCommunicationInterfaceSettings.InterruptPin = GHIElectronics.TinyCLR.Devices.Gpio.GpioController.GetDefault().
                OpenPin(GHIElectronics.TinyCLR.Pins.SC20260.GpioPin.PG6);
            networkCommunicationInterfaceSettings.InterruptEdge = GpioPinEdge.FallingEdge;
            networkCommunicationInterfaceSettings.InterruptDriveMode = GpioPinDriveMode.InputPullUp;           
            networkCommunicationInterfaceSettings.ResetPin = GHIElectronics.TinyCLR.Devices.Gpio.GpioController.GetDefault().
                OpenPin(GHIElectronics.TinyCLR.Pins.SC20260.GpioPin.PI8);
            networkCommunicationInterfaceSettings.ResetActiveState = GpioPinValue.Low;


           
            networkInterfaceSetting.Address = new IPAddress(new byte[] { 192, 168, 1, 122 });
            networkInterfaceSetting.SubnetMask = new IPAddress(new byte[] { 255, 255, 255, 0 });
            networkInterfaceSetting.GatewayAddress = new IPAddress(new byte[] { 192, 168, 1, 1 });
            networkInterfaceSetting.DnsAddresses = new IPAddress[] { new IPAddress(new byte[]
        { 75, 75, 75, 75 }), new IPAddress(new byte[] { 75, 75, 75, 76 }) };

            //     networkInterfaceSetting.DnsAddresses = new IPAddress[] { new IPAddress(new byte[]
            // { 192, 168, 1, 1 }), new IPAddress(new byte[] { 75, 75, 75, 76 }) };


            networkInterfaceSetting.MacAddress = new byte[] { 0x00, 0x4, 0x00, 0x00, 0x00, 0x00 };
            networkInterfaceSetting.DhcpEnable = true;
            networkInterfaceSetting.DhcpEnable = true;


            networkInterfaceSetting.TlsEntropy = new byte[] { 0, 1, 2, 3 };

            networkController.SetInterfaceSettings(networkInterfaceSetting);
            networkController.SetCommunicationInterfaceSettings
                (networkCommunicationInterfaceSettings);

            networkController.SetAsDefaultController();

            networkController.NetworkAddressChanged += NetworkController_NetworkAddressChanged;
            networkController.NetworkLinkConnectedChanged +=
                NetworkController_NetworkLinkConnectedChanged;

            networkController.Enable();

            while (linkReady == false) ;

            System.Diagnostics.Debug.WriteLine("Network is ready to use");
        }
        #endregion

        #region SetupWiFi7Click_SC20260D_MicroBus2
        static void SetupWiFi7Click_SC20260D_MicroBus2()
        {

            var enablePin = GpioController.GetDefault().OpenPin(SC20260.GpioPin.PI5);

            enablePin.SetDriveMode(GpioPinDriveMode.Output);
            enablePin.Write(GpioPinValue.High);

            SpiNetworkCommunicationInterfaceSettings networkCommunicationInterfaceSettings =
                new SpiNetworkCommunicationInterfaceSettings();

            var cs = GHIElectronics.TinyCLR.Devices.Gpio.GpioController.GetDefault().
               OpenPin(GHIElectronics.TinyCLR.Pins.SC20260.GpioPin.PC13);

            var settings = new GHIElectronics.TinyCLR.Devices.Spi.SpiConnectionSettings()
            {

                ChipSelectLine = cs,
                ClockFrequency = 4000000,
                Mode = GHIElectronics.TinyCLR.Devices.Spi.SpiMode.Mode0,
                ChipSelectType = GHIElectronics.TinyCLR.Devices.Spi.SpiChipSelectType.Gpio,
                ChipSelectHoldTime = TimeSpan.FromTicks(10),
                ChipSelectSetupTime = TimeSpan.FromTicks(10)
            };

            networkCommunicationInterfaceSettings.SpiApiName =
                GHIElectronics.TinyCLR.Pins.SC20260.SpiBus.Spi3;

            networkCommunicationInterfaceSettings.GpioApiName =
                GHIElectronics.TinyCLR.Pins.SC20260.GpioPin.Id;

            networkCommunicationInterfaceSettings.SpiSettings = settings;
            networkCommunicationInterfaceSettings.InterruptPin = GHIElectronics.TinyCLR.Devices.Gpio.GpioController.GetDefault().
                OpenPin(GHIElectronics.TinyCLR.Pins.SC20260.GpioPin.PJ13);
            networkCommunicationInterfaceSettings.InterruptEdge = GpioPinEdge.FallingEdge;
            networkCommunicationInterfaceSettings.InterruptDriveMode = GpioPinDriveMode.InputPullUp;
            networkCommunicationInterfaceSettings.ResetPin = GHIElectronics.TinyCLR.Devices.Gpio.GpioController.GetDefault().
                OpenPin(GHIElectronics.TinyCLR.Pins.SC20260.GpioPin.PI11);
            networkCommunicationInterfaceSettings.ResetActiveState = GpioPinValue.Low;

            var networkController = NetworkController.FromName
                ("GHIElectronics.TinyCLR.NativeApis.ATWINC15xx.NetworkController");

            WiFiNetworkInterfaceSettings networkInterfaceSetting = new WiFiNetworkInterfaceSettings()
            {
                Ssid = wiFiSSID_1,
                Password = wiFiKey_1,
            };

            networkInterfaceSetting.Address = new IPAddress(new byte[] { 192, 168, 1, 122 });
            networkInterfaceSetting.SubnetMask = new IPAddress(new byte[] { 255, 255, 255, 0 });
            networkInterfaceSetting.GatewayAddress = new IPAddress(new byte[] { 192, 168, 1, 1 });
            networkInterfaceSetting.DnsAddresses = new IPAddress[] { new IPAddress(new byte[]
        { 75, 75, 75, 75 }), new IPAddress(new byte[] { 75, 75, 75, 76 }) };

            //networkInterfaceSetting.MacAddress = new byte[] { 0x00, 0x4, 0x00, 0x00, 0x00, 0x00 };
            networkInterfaceSetting.MacAddress = new byte[] { 0x4A, 0x28, 0x05, 0x2A, 0xA4, 0x0F };

            networkInterfaceSetting.DhcpEnable = true;
            networkInterfaceSetting.DhcpEnable = true;

            networkInterfaceSetting.TlsEntropy = new byte[] { 1, 2, 3, 4 };

            networkController.SetInterfaceSettings(networkInterfaceSetting);
            networkController.SetCommunicationInterfaceSettings
                (networkCommunicationInterfaceSettings);

            networkController.SetAsDefaultController();

            networkController.NetworkAddressChanged += NetworkController_NetworkAddressChanged;
            networkController.NetworkLinkConnectedChanged +=
                NetworkController_NetworkLinkConnectedChanged;

            

            networkController.Enable();


            while (linkReady == false) ;

            // Network is ready to used
        }
        #endregion

        #region NetworkController Events
        private static void NetworkController_NetworkLinkConnectedChanged
            (NetworkController sender, NetworkLinkConnectedChangedEventArgs e)
        {
            // Raise event connect/disconnect
        }

        private static void NetworkController_NetworkAddressChanged
            (NetworkController sender, NetworkAddressChangedEventArgs e)
        {
            var ipProperties = sender.GetIPProperties();
            var address = ipProperties.Address.GetAddressBytes();

            linkReady = address[0] != 0;
        }
        #endregion
    }
}
