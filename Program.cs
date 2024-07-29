using AutoReserve.Class.ReserveModule;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AutoReserve
{
    class Program
    {
        static async Task Main(string[] args)
        {
            DateTime targetDate;
            int nightCnt;

            Dictionary<string, string> dicParam = new Dictionary<string, string>();
            dicParam.Add("module", "");
            dicParam.Add("operation", "");
            dicParam.Add("date", "");
            dicParam.Add("night", "");
            dicParam.Add("area", "");
            dicParam.Add("site", "");

            for (int i = 0; i < args.Length; i++)
            {
                if (args[i].StartsWith("-"))
                {
                    string paramKey = args[i].Replace("-", "");
                    if (dicParam[paramKey] == "")
                    {
                        dicParam[paramKey] = args[i + 1];
                        i++;
                    }
                    else
                    {
                        Util.WriteMsgThenWait($"check parameters - [{paramKey}]");
                        return;
                    }
                }
            }

            if (dicParam.Where(x => new string[] { "area", "site" }.Contains(x.Key) == false).Any(x => x.Value == ""))
            {
                Util.WriteMsgThenWait($"check parameters - [{string.Join(",", dicParam.Select(x => $"-{x.Key}"))}]");
                return;
            }

            try
            {
                targetDate = DateTime.ParseExact(dicParam["date"], "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
            }
            catch (FormatException)
            {
                Util.WriteMsgThenWait($"check parameters - [date : yyyy-MM-dd]");
                return;
            }

            try
            {
                nightCnt = int.Parse(dicParam["night"]);
            }
            catch (FormatException)
            {
                Util.WriteMsgThenWait($"check parameters - [night : integer]");
                return;
            }

            Console.Clear();
            string[] bannerLines = {
                @"############################################################",
                @"#                                                          #",
                @"#       /\_/\                                              #",
                @"#      ( o.o )                                             #",
                @"#       > ^ <                                              #",
                @"#      /      \                                            #",
                @"#     (        )                                           #",
                @"#      \      /                                            #",
                @"#       `----'                                             #",
                @"#                                                          #",
                @"############################################################",
                string.Join(" ", args),
                @"############################################################",
            };
            foreach (string line in bannerLines)
            {
                Console.WriteLine(line);
            }

            string[] arrSite = dicParam["site"].Split(',').Where(x => string.IsNullOrEmpty(x) == false).Select(x => x.Trim()).ToArray();
            var reservationInfo = new ReservationInfo(targetDate, nightCnt, arrSite);
            var objSlack = new Slack(System.Configuration.ConfigurationManager.AppSettings["SLACK_WEBHOOK_URL"]);

            BaseModule module = null;
            switch (dicParam["module"].ToLower())
            {
                case "jangho":
                    if (string.IsNullOrEmpty(dicParam["area"]))
                    {
                        module = new JanghoBeach(objSlack, reservationInfo);
                    }
                    else
                    {
                        module = new JanghoBeach(objSlack, reservationInfo, Convert.ToInt32(dicParam["area"]));
                    }
                    break;
                case "peace":
                    if (string.IsNullOrEmpty(dicParam["area"]))
                    {
                        module = new Peace(objSlack, reservationInfo);
                    }
                    else
                    {
                        module = new Peace(objSlack, reservationInfo, Convert.ToInt32(dicParam["area"]));
                    }
                    break;
                case "chuam":
                    module = new Chuam(objSlack, reservationInfo);
                    break;
                default:
                    break;
            }

            if (module != null)
            {
                string operation = dicParam["operation"];
                if (operation == "watching")
                {
                    module.StartWatching();
                }
                else if (operation == "openrun")
                {
                    module.StartOpenRun();
                }
                else
                {
                    Util.WriteMsgThenWait($"check parameters - [operation]");
                    return;
                }
            }
            else
            {
                Util.WriteMsgThenWait($"check parameters - [module]");
                return;
            }

            Util.WriteMsgThenWait("job complete");
            Console.ReadKey();
        }
    }

    public class ReservationInfo
    {
        public readonly string UserName = "이현석";
        public readonly string UserPhone = "01029124856";

        // 결제 과정 필요 정보
        public readonly string CheckoutPhone = "01029124856";
        public readonly string CheckoutBirth = "810305";

        public readonly DateTime ReservationDate;
        public readonly int NightCnt;
        public readonly List<string> LstSite;

        public ReservationInfo(DateTime reserveDate, int nightCnt, string[] arrSite)
        {
            ReservationDate = reserveDate;
            NightCnt = nightCnt;
            LstSite = arrSite.ToList();
        }

        public string GetReserveSummary()
        {
            return $"{ReservationDate.ToString("yyyy-MM-dd")} {NightCnt} night {(string.Join(",", LstSite.Take(5)))}{(LstSite.Count > 5 ? "..." : "")}";
        }
    }
}
