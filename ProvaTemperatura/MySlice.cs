using SalamiClient;
using SalamiClient.slice;
using SalamiClient.utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Devices.Enumeration;
using Windows.Devices.I2c;
using Windows.UI.Xaml;

namespace ApplicazioneBella
{
    public class MySlice : ABSSlice
    {
        //canale di comunicazione che rappresenta il sensore che comunica tramite I2C
        private I2cDevice temperatureSensor;

        //Indirizzo del sensore di temperatura
        private const byte TMP75C_ADDRESS = 0x48; //slave address

        //Registro per leggere i dati della temperatura (è qui che il sensore salva i dati). Registro read-only
        private const byte TEMP_I2C_REGISTER = 0x00;

        public MySlice() : base(1) { }

        public override void on_message(string message)
        {
            //Logger.info(message);
        }

        public new void send_ingredient_update()
        {
            Logger.debug("SONO ALL'INVIO DELL'INGREDIENT");
            InitI2CSensor();
        }

        private async void InitI2CSensor()
        {
            try
            {
                //Tramite il canale di comunicazione , acquisisco una stringa per effettuare query sul sensore
                string i2cDeviceSelector = I2cDevice.GetDeviceSelector();

                //Lista che rappresenta i dispositivi trovati con il precedente comando
                IReadOnlyList<DeviceInformation> devices = await DeviceInformation.FindAllAsync(i2cDeviceSelector);

                //Imposta i settaggi per la comunicazione con il sensore , specificando l'indirizzo dello slave
                var tmp75c_settings = new I2cConnectionSettings(TMP75C_ADDRESS);

                //Imposta la velocità di lettura 
                tmp75c_settings.BusSpeed = I2cBusSpeed.FastMode;

                //Crea il canale di comunicazione (asincrono) con il device i2c utilizzando le impostazioni specificate
                temperatureSensor = await I2cDevice.FromIdAsync(devices[0].Id, tmp75c_settings);
                Logger.debug("SENSOR OK");
                //polling timer
                /*
                timer = new DispatcherTimer() { Interval = TimeSpan.FromMilliseconds(5000) };
                timer.Tick += Timer_Tick;
                timer.Start();
                */
                while(true)
                {
                    await Task.Delay(1000);
                    Timer_Tick();
                }      
            }
            catch (Exception e)
            {
                Debug.WriteLine(e.Message);
            }
        }

        private void Timer_Tick()
        {
            try
            {
                //Rappresenta il registro dove leggere la temperatura (in esadecimale)
                var command = new byte[1];

                //Rappresenta un array di 12 bit, divisi in msb , xsb e lsb nel quale andranno scritti i valori della temperatura
                //ritornati dal sensore
                var temperatureData = new byte[2];

                //imposta il registro dove leggere la temperatura
                command[0] = TEMP_I2C_REGISTER;

                //Effettua l'operazione di lettura nel registro specificato in 'command' e li immagazzina nell'array temperatureData
                temperatureSensor.WriteRead(command, temperatureData);

                var msb = temperatureData[0];
                var lsb = temperatureData[1];

                //Conversione dei dati grezzi e shift dei bit come da manuale
                var rawTempReading = (msb << 8 | lsb) >> 4;

                //Algoritmo per la conversione da raw a celsius
                double rawTemperature = rawTempReading * 0.0625;

                //Approssimazione a due cifre decimali del risultato
                double temperature = Math.Round(rawTemperature, 2);
                int tempInt = Convert.ToInt32(temperature);

                //INVIO 
                this._iot.update_ingredient(SalamiConstants.CMD_SEND_DATA_EVENT_AC, "temperatura",tempInt.ToString());
                Logger.warning("TEMPERATURE IS " + temperature);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
            }

        }
    }
}