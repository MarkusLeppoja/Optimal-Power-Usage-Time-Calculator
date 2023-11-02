using System;
using System.Globalization;
using System.Net.Http;

namespace ElektriTarbija
{


    internal class Program
    {
        //static String year, month, day;

        static int year, month, day, day_prev;
        static String s_year, s_month, s_day, s_day_prev;

        static Uri request_destination_link;
        static int line_index, cur_index;

        // Requested data
        static float[] hourly_price_array = new float[24];  // Stores the current day power prices. Begins from index 0 and indicates the current hour/index price
        static String http_request_responce;

        static void get_date_time()
        {
            DateTime date = DateTime.Now;

            year = date.Year;
            month = date.Month;
            day = date.Day;

            // Timezone compensation
            if (day <= 1)
            {
                month--;
                day = 31;
            }

            if (month <= 1)
            {
                year--;
                month = 12;
                day = 31;
            }

            // If date is a single number, add 0 before the day && month
            s_year = $"{year}";

            if (day < 10) s_day = $"0{day}";
            else s_day = $"{day}";

            if (month < 10) s_month = $"0{month}";
            else s_month = $"{month}";

            day_prev = day - 1;
            if (day_prev < 10) s_day_prev = $"0{day_prev}";
            else s_day_prev = $"{day_prev}";

            // Log current date
            Console.WriteLine($"Current Date: {year}-{month}-{day}");
        }

        static void create_request_string()
        {
            // UTC and estonia time difference is 2h. So we just request data 2 hours before 00.
            // also we the end data request is at 21 because we only want to know current day prices, but if 22 is used it will return the next day 00 price
            String default_link = $"https://dashboard.elering.ee/api/nps/price/csv?start={s_year}-{s_month}-{s_day_prev}T21%3A01%3A00.000Z&end={s_year}-{s_month}-{s_day}T21%3A00%3A00.000Z&fields=ee";

            // Log request Url
            Console.WriteLine($"Request Url: {default_link}");

            request_destination_link = new Uri(default_link);
        }

        static int execute_request(Uri url)
        {
            HttpClient new_client = new HttpClient();
            HttpResponseMessage response_message = new_client.GetAsync(url).Result;

            // Get and print status code
            int status_code = (int)response_message.StatusCode;
            Console.WriteLine($"Request Status: {status_code}");

            // Get data if request was successful, otherwise return
            if (status_code != 200) return -1;
            http_request_responce = response_message.Content.ReadAsStringAsync().Result;
            
            return 1;
        }
        
        static void sort_incoming_data(String data_stream)
        {
            // Split all data to a list
            String[] split_data = http_request_responce.Split(';');



            Console.WriteLine("Todays Power Prices Are The Following:");

            // Loop thru the list, find the price data
            // Remove everything other than the price and replace the comma with a dot
            // Add it to a list and integrate index
            foreach (String line in split_data)
            {
                if (line_index % 2 == 0 && line_index > 3)
                {
                    // Format the string to be convertable into a float
                    String data = line;
                    data = data.Remove(data.IndexOf('\"'), 1);
                    data = data.Remove(data.IndexOf('\"'), data.Length -  data.IndexOf('\"'));
                    data = data.Replace(',', '.');  // Replaces the {,} with {.}
                    
                    // Convert the price from a string to a float and put it in the array
                    float current_hour_price = (float) Convert.ToDouble(data, CultureInfo.InvariantCulture);
                    hourly_price_array[cur_index] = current_hour_price;

                    // Print out the price
                    Console.Write($"{cur_index} - {cur_index+1}: ");
                    Console.WriteLine(current_hour_price);
                    cur_index++;
                }
                line_index++;
            }
        }

        static double sum, average, lowest_usage_price;
        static int lowest_power_price_timestamp;
        // Takes in 
        // usage_duration - How long is the user planning to use power for
        // max_power_price - If the power price is higher than the given value -1 will be returned. To disable this, put -1 in there
        static int calculate_optimal_power_usage_time_duration(int usage_duration, int current_time = 0, float max_power_price = -1) //todo
        {
            double[] usage_duration_average_price = new double[24];   // This stores the average power price during a {usage_duration} period

            // Calculate the average power price over the usage duration
            for (int i = current_time; i < usage_duration_average_price.Length - usage_duration + 1; i++)
            {
                // Beginning from time {i} add each hour of usage price into the sum variable
                for (int j = 0; j < usage_duration; j++)
                {
                    sum += (double) hourly_price_array[i + j];
                }

                // Calculate the average power price over usage duration
                average = (double) (sum / usage_duration);
                
                usage_duration_average_price[i] = average;

                // Reset
                sum = average = 0;
            }

            // Give it an initial value or it will always return 0
            lowest_usage_price = 1000000;

            // Find the lowest power price for the usage duration in the currenty day
            for (int i = current_time; i < usage_duration_average_price.Length - usage_duration + 1; i++)
            {
                //Console.WriteLine($"Average: {i} - {i+1}: {usage_duration_average_price[i]}"); Debug
                if (lowest_usage_price > usage_duration_average_price[i])
                {
                    lowest_usage_price = usage_duration_average_price[i];
                    lowest_power_price_timestamp = i;
                }
            }

            // If the cheapest possible price is higher than the maximum price return -1 (Couldn't find a suitable time
            if (usage_duration_average_price[lowest_power_price_timestamp] >= max_power_price && max_power_price != -1) lowest_power_price_timestamp = -1;

            // Return the beginning time of the lowest average power price for the usage duration
            return lowest_power_price_timestamp;
        }

        static void get_current_day_power_data()
        {
            get_date_time();
            create_request_string();
            execute_request(request_destination_link);
            sort_incoming_data(http_request_responce);
        }

        static void Main(string[] args)
        {
            get_current_day_power_data(); // Get API data and sort it

            while(true)
            {
                Console.WriteLine("How many hours straight would you like to consume power for?");
                int input_duration = Convert.ToInt32(Console.ReadLine());

                if (input_duration > 0 && input_duration < 24 - DateTime.Now.Hour)
                {
                    int optimal_usage_time = calculate_optimal_power_usage_time_duration(input_duration, 7);

                    // Anlysise the price, if the returned value is -1 print out that there is no power consumtion time enlisiting user specifified requirements
                    Console.WriteLine($"The best time to consume {input_duration} hours of power for is starting from {optimal_usage_time}");
                }
                else
                {
                    Console.WriteLine("User enterd an incorrect value. Please try again!");
                }
            }

        }
    }
}
