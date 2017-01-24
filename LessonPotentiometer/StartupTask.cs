using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net.Http;
using Windows.ApplicationModel.Background;
using Windows.Devices.Spi;
using System.Threading.Tasks;
using Microsoft.Devices.Tpm;
using Microsoft.Azure.Devices.Client;
using System.Runtime.InteropServices.WindowsRuntime;

// The Background Application template is documented at http://go.microsoft.com/fwlink/?LinkID=533884&clcid=0x409

namespace LessonPotentiometer
{
    public sealed class StartupTask : IBackgroundTask
    {
        enum AdcDevice { NONE, MCP3002, MCP3208, MCP3008 };
        /* Important! Change this to either AdcDevice.MCP3002, AdcDevice.MCP3208 or AdcDevice.MCP3008 depending on which ADC you chose     */
        private AdcDevice ADC_DEVICE = AdcDevice.MCP3008;
        private const string SPI_CONTROLLER_NAME = "SPI0";  /* Friendly name for Raspberry Pi 2 SPI controller          */
        private const Int32 SPI_CHIP_SELECT_LINE = 0;       /* Line 0 maps to physical pin number 24 on the Rpi2        */
        private SpiDevice SpiADC;
        private const byte MCP3002_CONFIG = 0x68; /* 01101000 channel configuration data for the MCP3002 */
        private const byte MCP3208_CONFIG = 0x06; /* 00000110 channel configuration data for the MCP3208 */
        private readonly byte[] MCP3008_CONFIG = { 0x01, 0x80 }; /* 00000001 10000000 channel configuration data for the MCP3008 */
        private int adcValue;

        /* Convert the raw ADC bytes to an integer */
        public int convertToInt([ReadOnlyArrayAttribute()] byte[]  data)
        {
            int result = 0;
            switch (ADC_DEVICE)
            {
                case AdcDevice.MCP3002:
                    result = data[0] & 0x03;
                    result <<= 8;
                    result += data[1];
                    break;
                case AdcDevice.MCP3208:
                    result = data[1] & 0x0F;
                    result <<= 8;
                    result += data[2];
                    break;
                case AdcDevice.MCP3008:
                    result = data[1] & 0x03;
                    result <<= 8;
                    result += data[2];
                    break;
            }
            return result;
        }

        public void ReadADC()
        {
            byte[] readBuffer = new byte[3]; /* Buffer to hold read data*/
            byte[] writeBuffer = new byte[3] { 0x00, 0x00, 0x00 };

            /* Setup the appropriate ADC configuration byte */
            switch (ADC_DEVICE)
            {
                case AdcDevice.MCP3002:
                    writeBuffer[0] = MCP3002_CONFIG;
                    break;
                case AdcDevice.MCP3208:
                    writeBuffer[0] = MCP3208_CONFIG;
                    break;
                case AdcDevice.MCP3008:
                    writeBuffer[0] = MCP3008_CONFIG[0];
                    writeBuffer[1] = MCP3008_CONFIG[1];
                    break;
            }

            SpiADC.TransferFullDuplex(writeBuffer, readBuffer); /* Read data from the ADC                           */
            adcValue = convertToInt(readBuffer);                /* Convert the returned bytes into an integer value */

        }
        private async Task InitSPI()
        {
                var settings = new SpiConnectionSettings(SPI_CHIP_SELECT_LINE);
                settings.ClockFrequency = 500000;   /* 0.5MHz clock rate                                        */
                settings.Mode = SpiMode.Mode0;      /* The ADC expects idle-low clock polarity so we use Mode0  */
                var controller = await SpiController.GetDefaultAsync();
                SpiADC = controller.GetDevice(settings);
        }
        private void initDevice()
        {
            TpmDevice device = new TpmDevice(0);
            string hubUri = device.GetHostName();
            string deviceId = device.GetDeviceId();
            string sasToken = device.GetSASToken();
            _sendDeviceClient = DeviceClient.Create(hubUri, AuthenticationMethodFactory.CreateAuthenticationWithToken(deviceId, sasToken), TransportType.Amqp);
        }
        private DeviceClient _sendDeviceClient;
        private async void SendMessages(string strMessage)
        {
            string messageString = strMessage;
            var message = new Message(Encoding.ASCII.GetBytes(messageString));
            await _sendDeviceClient.SendEventAsync(message);
        }
        public async void Run(IBackgroundTaskInstance taskInstance)
        {
            await InitSPI();
            initDevice();
            while (true)
            {
                ReadADC();
                SendMessages(adcValue.ToString());
                Task.Delay(1000).Wait();
            }
            // 
            // TODO: Insert code to perform background work
            //
            // If you start any asynchronous methods here, prevent the task
            // from closing prematurely by using BackgroundTaskDeferral as
            // described in http://aka.ms/backgroundtaskdeferral
            //
        }
    }
}
