namespace SPACE_Hitachi_Console_Project
{
    public class WeatherForecastModel
    {
        public WeatherForecastModel(int dayParameter, int temperature, int wind, int humidity, int precipitation, string lightning, string clouds)
        {
            DayParameter = dayParameter;
            Temperature = temperature;
            Wind = wind;
            Humidity = humidity;
            Precipitation = precipitation;
            Lightning = lightning;
            Clouds = clouds;
        }

        public int DayParameter { get; set; }

        public int Temperature { get; set; }

        public int Wind { get; set; }

        public int Humidity { get; set; }

        public int Precipitation { get; set; }

        public string Lightning { get; set; }

        public string Clouds { get; set; }
        
        public double Latitude { get; set; }

        public string Location { get; set; }
    }
}
