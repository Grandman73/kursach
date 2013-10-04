﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Text.RegularExpressions;
using Newtonsoft.Json;

namespace KursachV3
{
    class Tour
    {
        public int ProgressParsing;
        /// <summary>
        /// Таблица со спарсенными турами
        /// </summary>
        readonly DataTable _tableTours = new DataTable();
        readonly Db _db = new Db(Properties.Settings.Default.Database1ConnectionString);
        /// <summary>
        /// Хранит города в виде : (город;id)
        /// </summary>
        readonly Hashtable _htb = new Hashtable();
        /// <summary>
        /// Страны
        /// </summary>
        readonly Dictionary<string, int> _parametrs = new Dictionary<string, int>();
        public Tour(Db database)
        {
            _db = database;
        }

        DataTable ParseTours(string country, DateTime dateTour, int page,int nightFrom, int nightTo, DateTime dateFrom, DateTime dateTo,string townFrom)
        {
            DataTable tableTours= new DataTable();
            tableTours.Columns.Add("Name");
            tableTours.Columns.Add("Stars");
            tableTours.Columns.Add("Nights");
            tableTours.Columns.Add("Cost");
            const string url = "http://www.1001tur.ru/cgi-bin/Client.cgi?tourSearchPage=1";
            var resultpage = HttpRequest.GetResponse(url, "act=search&Page="+page+"&Country=" + _parametrs[country] + "&Curort=&Hotel=&Kat=&Food=&check1=0&TipRazm=2&Chld1=&Chld2=&NightOt=" + nightFrom + "&NightDo=" + nightTo + "&PriceOt=&PriceDo=&SDay=" + dateFrom.Day + "&SMonth=" + dateFrom.Month + "&SYear="+dateFrom.Year+"&EDay=" + dateTo.Day + "&EMonth=" + dateTo.Month + "&EYear="+dateTo.Year+"&SortBy=Price&ShowBy=3&MorePage=1&MorePageStop=1&ctours=0&from_city=" + _htb[townFrom] + "&is_ski=&sortfilter=price&simpleSearch=0", "POST");
            resultpage = resultpage.Replace("   ", " ");
            const string patternNameUrl = @"(<div class=""sr_val1_sub1""><!--googleoff: all--><a href="")(http\://[a-zA-Z0-9\-\.]+\.[a-zA-Z]{2,3}(/\S*)?)("" target=""_blank"" rel=""nofollow"">)([^<]*)(</a><!--googleon: all--></div>)";
            const string patternStars = @"(<span class=""sr_hotelscat"">)(\d*)([*]+)(\*</span><span class=""sr_stars""></span>)";
            const string patternNights = @"(<span class=""sr_night"">)(\d*)(</span> ночей)";
            const string patternCost = @"(<span class=""actualization_price"">)(\d*)(&nbsp;)(\d*)(</span> руб</a>)";
            const string patternDate = @"(<td class=""sr_val3""><div class=""sr_val3_sub1"">)(\d*).(\d*)(</div></td>)";
            MatchCollection nameAndUrlMatches = Regex.Matches(resultpage, patternNameUrl);
            MatchCollection starsMatches = Regex.Matches(resultpage, patternStars);
            MatchCollection nightsMatches = Regex.Matches(resultpage, patternNights);
            MatchCollection costsMatches = Regex.Matches(resultpage, patternCost);
            MatchCollection dateMatches = Regex.Matches(resultpage, patternDate);
            for (int mtch = 0; mtch < nameAndUrlMatches.Count; mtch++)
            {
                tableTours.Rows.Add(nameAndUrlMatches[mtch].Groups[5].Value, starsMatches[mtch].Groups[2].Value, nightsMatches[mtch].Groups[2].Value, costsMatches[mtch].Groups[2].Value + costsMatches[mtch].Groups[4].Value, nameAndUrlMatches[mtch].Groups[2].Value, dateMatches[mtch].Groups[2], country);
            }
            return tableTours; 
        }
        public string GetCity(string city)
        {
            return "" + _htb[city];
        }
        public DataTable GetAllToursFromBd(int money = 0)
        {
            var table = money == 0 ? _db.Select("*", "tours", "id", "desc") : _db.Select("*", "tours", "", "", "price<='" + money + "'");
            return table;
        }

        public string[] GetCities()
        {
            _htb.Clear();
            const string url = "http://www.1001tur.ru/searest.htm";
            var page = HttpRequest.GetResponse(url,"","GET");
            var pattern = @"(<select name=""from_city"" class=""sfs_fake_chouse_select jq_fake_chouse_select"">)([\s\S]*)(\t{4}</select>)";
            var regex = new Regex(pattern);
            var match = regex.Match(page);
            var cities = match.Groups[2].Value;
            pattern = @"(<OPTION value="")([0-9]{1,6})("" class=""""(?: >| SELECTED>))(\D*)(</OPTION>)";
            regex = new Regex(pattern);
            match = regex.Match(cities);
            while (match.Success)
            {
                _htb.Add(match.Groups[4].Value, match.Groups[2].Value);//HASHTABLE (ГОРОД;ID)
                match = match.NextMatch();
            }
            var result = new string[_htb.Keys.Count]; 
            _htb.Keys.CopyTo(result,0);
            return result;
        }
        [JsonObject(MemberSerialization.OptIn)]
        struct Country
        {
            [JsonProperty("value")]
            public int Value { get; set; }

            [JsonProperty("name")]
            public string Name { get; set; }
        }
        public Dictionary<string, int> JsonParsingCountries(int cityId) // происходит при выборе города
        {
            var id = _htb[cityId].ToString();
            var json = HttpRequest.GetResponse("http://www.1001tur.ru/cgi-bin/get_countries.pl?from_city=" + id + "&json=1","","GET");
            if (json == null)
            {
                return null;
            }
            var country = JsonConvert.DeserializeObject<Country[]>(json);
            _parametrs.Clear();
            foreach (var countries in country)
            {
                _parametrs.Add(countries.Name, countries.Value);
            }

            return _parametrs;
        }
        public void UpdateTour(int id, int price,DateTime date, string description,string hotel,int stars,string url,string country)
        {
            var vars = ToursValues(price, date, description, hotel, stars, url, country);
            _db.Update("tours",vars,id);
        }
        public void AddTour(int price, DateTime date, string description, string hotel, int stars, string url, string country)
        {
            var vars = ToursValues(price, date, description, hotel, stars, url, country);
            _db.Insert("tours",vars);
        }
        private static Dictionary<string,object> ToursValues(int price, DateTime date, string description, string hotel, int stars, string url, string country)
        {
            var vars = new Dictionary<string, object>
                           {
                               {"price", price},
                               {"date", date},
                               {"description", description},
                               {"hotel", hotel},
                               {"stars", stars},
                               {"url", url},
                               {"country", country}
                           };

            return vars;
        }
        public void DelTour(int id)
        {
            _db.Delete("tours", id);
        }
    }
}
