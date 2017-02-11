using System;
using System.Text;
using Windows.ApplicationModel.Background;
using Windows.Devices.Spi;
using System.Threading.Tasks;
using Microsoft.Devices.Tpm;
using Microsoft.Azure.Devices.Client;
using Windows.Devices.Gpio;

namespace LessonPotentiometer
{ 
    public sealed class StartupTask : IBackgroundTask
    {
        private SpiDevice SpiADC;
        GpioPin greenPin, redPin;
        private class Channel
        {
            public static byte C1 = 0x80;
            public static byte C2 = 0x90;
            public static byte C3 = 0xA0;
            public static byte C4 = 0xB0;
            public static byte C5 = 0xC0;
            public static byte C6 = 0xD0;
            public static byte C7 = 0xE0;
            public static byte C8 = 0xF0;
        }
        private void initGpio()
        {
            int GREEN_LED_PIN = 35;
            int RED_LED_PIN = 47;
            var gpio = GpioController.GetDefault();
            greenPin = gpio.OpenPin(GREEN_LED_PIN);
            greenPin.SetDriveMode(GpioPinDriveMode.Output);
            redPin = gpio.OpenPin(RED_LED_PIN);
            redPin.SetDriveMode(GpioPinDriveMode.Output);
        }

        private int ReadADC(byte channel)
        {
            byte[] readBuffer = new byte[3]; // Buffer to hold read data
            byte[] writeBuffer = new byte[3] { 0x01, 0x00, 0x00 };
            writeBuffer[1] = channel;

            SpiADC.TransferFullDuplex(writeBuffer, readBuffer); // Read data from the ADC
            return ((readBuffer[1] & 3) << 8) + readBuffer[2]; //convert bytes to int
        }
        private async Task InitSPI()
        {
            try
            {
                var settings = new SpiConnectionSettings(0); // 0 maps to physical pin number 24 on the Rpi2
                settings.ClockFrequency = 500000;   // 0.5MHz clock rate
                settings.Mode = SpiMode.Mode0;      // The ADC expects idle-low clock polarity so we use Mode0

                var controller = await SpiController.GetDefaultAsync();
                SpiADC = controller.GetDevice(settings);
            }
            catch (Exception ex)
            {
                throw new Exception("SPI Initialization Failed", ex);
            }
        }
        double rawRange = 1024; // 3.3v
        double logRange = 5; // 3.3v = 10^5 lux
        double RawToLux(int raw)
        {
            double logLux = raw * logRange / rawRange;
            return Math.Pow(10, logLux);
        }
        public async void Run(IBackgroundTaskInstance taskInstance)
        {
            await InitSPI();
            initGpio();
            while (true)
            {
                int val = ReadADC(Channel.C1);
                double lux = RawToLux(val);
                string message = "raw: "+val+" lux: "+lux;
                if (val < 1024 / 3) //ADC value third low 0...341
                {
                    redPin.Write(GpioPinValue.High);
                    greenPin.Write(GpioPinValue.Low);
                }
                else if (val > 1024 * 2 / 3) //ADC value third high 683...1024
                {
                    redPin.Write(GpioPinValue.Low);
                    greenPin.Write(GpioPinValue.High);
                }
                else //ADC value in the middle 342...682
                {
                    redPin.Write(GpioPinValue.High);
                    greenPin.Write(GpioPinValue.High);
                }
                Task.Delay(1000).Wait();
            }
        }
    }
}
