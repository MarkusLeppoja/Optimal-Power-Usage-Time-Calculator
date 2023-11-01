using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;

namespace ElektriTarbija
{


    internal class Program
    {
        //static String year, month, day;
        static String http_request_responce;

        static int year, month, day, day_prev;
        static String s_year, s_month, s_day, s_day_prev;

        static Uri request_destination_link;
        static int cur_index;

        // Requested data
        static float[] hourly_price_array = new float[24];  // Stores the current day power prices. Begins from index 0 and indicates the current hour/index price

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
            http_request_responce = response_message.Content.ReadAsStringAsync().Result.ToString();

            return 1;
        }

        static void sort_incoming_data(String http_request_responce)
        {
            // Split all data to a list
            String[] split_data = http_request_responce.Split(';');

            Console.WriteLine("Todays Power Prices Are The Following:");
            
            // Loop thru the list, find the price data by searching for a comma (Only price contains a comma)
            // Remove everything other than the price and replace the comma with a dot
            // Add it to a list and integrate index
            foreach (String raw_data in split_data)
            {
                if (raw_data.Contains(","))
                {
                    // Format the string to be convertable into a float
                    String data = raw_data;
                    data = data.Remove(data.IndexOf('\"', data.IndexOf(",")), data.Length - data.IndexOf('\"', data.IndexOf(","))); // Removes last {"} and everything after that
                    data = data.Remove(data.IndexOf('\"'), 1);  // Removes the first {"}
                    data = data.Replace(',', '.');  // Replaces the {,} with {.}

                    // Convert the price from a string to a float and put it in the array
                    float current_hour_price = (float) Convert.ToDouble(data, CultureInfo.InvariantCulture);
                    hourly_price_array[cur_index] = current_hour_price;

                    // Print out the price
                    Console.Write($"{cur_index} - {cur_index+1}: ");
                    Console.WriteLine(current_hour_price);

                    cur_index++;
                }
            }
        }

        static float sum, average, lowest_usage_price;
        static int lowest_power_price_timestamp;
        static int calculate_optimal_power_usage_time_duration(int usage_duration)
        {
            float[] usage_duration_average_price = new float[24];   // This stores the average power price during a {usage_duration} period

            // Calculate the average power price over the usage duration
            for (int i = 0; i < usage_duration_average_price.Length - usage_duration; i++)
            {
                // Beginning from time {i} add each hour of usage price into the sum variable
                for (int j = 0; j < usage_duration; j++)
                {
                    sum += (float) hourly_price_array[i + j];
                }

                // Calculate the average power price over usage duration
                average = (float) sum / usage_duration;
                
                usage_duration_average_price[i] = average;

                // Reset
                sum = average = 0;
            }

            // Give it an initial value or it will always return 0
            lowest_usage_price = 1000000;

            // Find the lowest power price for the usage duration in the currenty day
            for (int i = 0; i < usage_duration_average_price.Length - usage_duration; i++)
            {
                if (lowest_usage_price > usage_duration_average_price[i])
                {
                    lowest_usage_price = usage_duration_average_price[i];
                    lowest_power_price_timestamp = i;
                }
            }

            // Return the beginning time of the lowest average power price for the usage duration
            return lowest_power_price_timestamp;
        }

        static void Main(string[] args)
        {
            get_date_time();
            create_request_string();
            execute_request(request_destination_link);
            sort_incoming_data(http_request_responce);

            Console.WriteLine("How many hours straight would you like to consume power for?");
            int result = Convert.ToInt32(Console.ReadLine());

            Console.WriteLine($"The best time to consume {result} hours of power for is starting from {calculate_optimal_power_usage_time_duration(result) + 1}");
        }
    }
}
