using Newtonsoft.Json.Linq;
using System.Net;
using System.Net.Mail;
using System.Text;

namespace SPACE_Hitachi_Console_Project
{
    public class Program
    {
        // Constants for various parameters
        private static List<string> ALLOW_LIGHTNING_VALUES = new List<string>
        {
            "Yes",
            "No"
        };

        private static List<string> ALLOW_CLOUDS_TYPES = new List<string>
        {
            CloudsConstants.Cumulonimbus,
            CloudsConstants.Cumulus,
            CloudsConstants.Stratus,
            CloudsConstants.Nimbus,
            CloudsConstants.Cirrus           
        };

        private const int MAX_PARAMETER_COUNT = 7;

        private const int DAYS_COUNT_OF_JULY = 15;

        private const string SMTP_SERVER_NAME = "smtp-mail.outlook.com";

        private const int SMTP_PORT = 587;

        private const string FORMAT = "{0}\\{1}";

        private const string FILE_NAME = "LaunchAnalysisReport.csv";

        private const string MONTH = " July";

        static async Task Main(string[] args)
        {
            string path = null;
            while (string.IsNullOrEmpty(path))
            {
                Console.WriteLine("Enter the path to the files:");
                path = Console.ReadLine();
            }

            string senderEmail = null;
            while (string.IsNullOrEmpty(senderEmail))
            {
                Console.WriteLine("Enter the sender email address:");
                senderEmail = Console.ReadLine();
            }

            string password = null;
            while (string.IsNullOrEmpty(password))
            {
                Console.WriteLine("Enter the password:");
                password = Console.ReadLine();
            }

            string receiverEmail = null;
            while (string.IsNullOrEmpty(receiverEmail))
            {
                Console.WriteLine("Enter the receiver email address:");
                receiverEmail = Console.ReadLine();
            }           

            // Get files from the given path
            string[] fileNames = Directory.GetFiles(path);

            // Take the top results from files asynchronously
            List<WeatherForecastModel> topResults = await GetTopResultsFromFilesAsync(fileNames);

            // Get the best result among the top results
            WeatherForecastModel? bestResult = topResults
                .OrderBy(x => x.Latitude).FirstOrDefault();

            if (bestResult == null)
            {
                Console.WriteLine("No result found.");
                return;
            }

            // Create output data and write to file
            string output = CreateOutputData(bestResult);

            File.WriteAllText(string.Format(FORMAT, path, FILE_NAME), output);

            // Send email with the file as attachment
            SendEmailResult(path, senderEmail, password, receiverEmail);
        }

        // Method to send email with result file as attachment
        private static void SendEmailResult(string path, string senderEmail, string password, string receiverEmail)
        {
            string fileDir = Path.Combine(path, FILE_NAME);

            if (!File.Exists(fileDir))
            {
                throw new ArgumentException($"File {FILE_NAME} is not found.");
            }

            // Set up email client and send email
            MailMessage mail = new MailMessage(senderEmail, receiverEmail);
            SmtpClient client = new SmtpClient(SMTP_SERVER_NAME, SMTP_PORT);

            client.UseDefaultCredentials = false;
            client.Credentials = new NetworkCredential(senderEmail, password);
            client.EnableSsl = true;
            client.DeliveryMethod = SmtpDeliveryMethod.Network;

            Attachment attachment = new Attachment(fileDir);
            mail.Attachments.Add(attachment);

            try
            {
                client.Send(mail);
                Console.WriteLine("Email sent successfully.");
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            finally
            {
                attachment.Dispose();
            }
        }

        // Method to read files and take the top results
        private static async Task<List<WeatherForecastModel>> GetTopResultsFromFilesAsync(string[] fileNames)
        {
            List<WeatherForecastModel> topResults = new List<WeatherForecastModel>();

            foreach (string filePath in fileNames)
            {
                // Read text from file
                string allText = await ReadAsync(filePath);

                // Parse text to dictionary
                string[] allLines = allText.Split(Environment.NewLine);
                Dictionary<string, List<string>> dict = ParseRowDataToDictionary(allLines);

                // Validate data and create weather forecast models
                List<WeatherForecastModel> weatherResult = ValidateAndCreateModels(dict);

                // Select the top result
                WeatherForecastModel? topResult = weatherResult
                    .Where(x => (x.Temperature > 1 && x.Temperature < 32)
                    && x.Wind < 11
                    && x.Humidity < 55
                    && x.Precipitation == 0
                    && x.Lightning == "No"
                    && (x.Clouds != "Cumulus" && x.Clouds != "Nimbus"))
                    .OrderBy(x => x.Wind)
                    .ThenBy(x => x.Humidity)
                    .FirstOrDefault();

                if (topResult == null)
                {
                    continue;
                }

                // Get latitude of the city from file name
                string cityName = filePath.Split("\\").Last().Split(".").First();

                // Assume that in the directory where the files are, the city is taken according to the name of the file
                double latitude = await GetCityLatitude(cityName); 

                topResult.Latitude = latitude;
                topResult.Location = cityName;

                topResults.Add(topResult);
            }

            return topResults;
        }

        // Method to create output data from best result
        private static string CreateOutputData(WeatherForecastModel? bestResult)
        {
            StringBuilder sb = new StringBuilder();

            sb.Append(bestResult.Location).Append(", ");
            sb.Append(bestResult.DayParameter).Append(MONTH);

            string output = sb.ToString();
            return output;
        }

        // Method to get latitude of a city from OpenStreetMap API
        private static async Task<double> GetCityLatitude(string cityName)
        {
            string geocodingUrl = $"https://nominatim.openstreetmap.org/search?city={Uri.EscapeDataString(cityName)}&format=json";

            using (var httpClient = new HttpClient())
            {
                httpClient.DefaultRequestHeaders.Add("User-Agent", "TestApp/1.0 (n.n.gradinarov@outlook.com)");

                var response = await httpClient.GetStringAsync(geocodingUrl);
                JArray jsonResponse = JArray.Parse(response);

                if (jsonResponse.Count > 0)
                {
                    JToken location = jsonResponse[0];
                    double latitude = (double)location["lat"];

                    return latitude;
                }
                else
                {
                    return double.MaxValue;
                }
            }
        }

        // Method to validate data and create weather forecast models
        private static List<WeatherForecastModel> ValidateAndCreateModels(Dictionary<string, List<string>> dict)
        {
            var result = new List<WeatherForecastModel>();

            for (int i = 0; i < DAYS_COUNT_OF_JULY; i++)
            {
                List<string> values = dict.Keys.Select(key => dict[key][i]).ToList();

                if (MAX_PARAMETER_COUNT != values.Count)
                {
                    throw new ArgumentException("Missing parameters! Check input.");
                }

                if (!int.TryParse(values[0], out int dayParameter))
                {
                    throw new ArgumentException("Invalid day parameter value.");
                }

                if (!int.TryParse(values[1], out int temperature))
                {
                    throw new ArgumentException("Invalid temperature value.");
                }

                if (!int.TryParse(values[2], out int wind))
                {
                    throw new ArgumentException("Invalid wind value.");
                }

                if (!int.TryParse(values[3], out int humidity))
                {
                    throw new ArgumentException("Invalid humidity value.");
                }

                if (!int.TryParse(values[4], out int precipitation))
                {
                    throw new ArgumentException("Invalid precipitation value.");
                }

                string lightning = values[5];

                if (string.IsNullOrEmpty(lightning) && !ALLOW_LIGHTNING_VALUES.Contains(lightning))
                {
                    throw new ArgumentException("Invalid lighning value.");
                }

                string cloudType = values[6];

                if (string.IsNullOrEmpty(cloudType) && !ALLOW_CLOUDS_TYPES.Contains(cloudType))
                {
                    throw new ArgumentException("Invalid clouds types.");
                }

                var model = new WeatherForecastModel(dayParameter, temperature, wind, humidity, precipitation, lightning, cloudType);

                result.Add(model);
            }

            return result;
        }

        // Method to parse row data into a dictionary
        private static Dictionary<string, List<string>>ParseRowDataToDictionary(string[] allLines)
        {
            var dict = new Dictionary<string, List<string>>();

            // Iterate through each line of data
            foreach (var line in allLines)
            {
                string[] lineSplit = line.Split(",", StringSplitOptions.RemoveEmptyEntries);

                // The first element is considered as the key
                string key = lineSplit[0];

                List<string> value = lineSplit.Skip(1).Take(lineSplit.Length).ToList();

                if (!dict.ContainsKey(lineSplit[0]))
                {
                    dict[key] = value;
                    continue;
                }

                dict[key].AddRange(value);
            }

            return dict;
        }

        // Method to read text from a file asynchronously
        private static async Task<string> ReadAsync(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                throw new ArgumentException("No path provided");
            }

            string textFile = await File.ReadAllTextAsync(path);

            return textFile;
        }
    }
}