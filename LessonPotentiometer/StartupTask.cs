using System;
using System.Text;
using Windows.ApplicationModel.Background;
using Windows.Devices.Spi;
using System.Threading.Tasks;
using Microsoft.Devices.Tpm;
using Microsoft.Azure.Devices.Client;

// The Background Application template is documented at http://go.microsoft.com/fwlink/?LinkID=533884&clcid=0x409

namespace LessonPotentiometer
{
    public sealed class StartupTask : IBackgroundTask
    {
        private const Int32 SPI_CHIP_SELECT_LINE = 0;       /* Line 0 maps to physical pin number 24 on the Rpi2        */
        private SpiDevice SpiADC;
        private readonly byte[] CHANNEL_SELECTION = { 0x80, 0x90, 0xA0, 0xB0, 0xC0, 0xD0, 0xE0, 0xF0 }; //channels 1..8 for MCP3008

        public int ReadADC(byte channel)
        {
            byte[] readBuffer = new byte[3]; /* Buffer to hold read data*/
            byte[] writeBuffer = new byte[3] { 0x01, 0x00, 0x00 };
            writeBuffer[1] = channel; //selecting ADC channel

            SpiADC.TransferFullDuplex(writeBuffer, readBuffer); /* Read data from the ADC */
            return ((readBuffer[1] & 3) << 8) + readBuffer[2]; //convert bytes to int
        }
        private async Task InitSPI()
        {
            var settings = new SpiConnectionSettings(SPI_CHIP_SELECT_LINE);
            settings.ClockFrequency = 500000;   /* 0.5MHz clock rate */
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
                SendMessages(ReadADC(CHANNEL_SELECTION[1]).ToString());
                Task.Delay(1000).Wait();
            }
        }
    }
}
