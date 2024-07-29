using System.Threading;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;
using System;
using System.Collections.Generic;

namespace AutoReserve.Class.ReserveModule
{
    public abstract class BaseModule
    {
        /*
         * 실행순서
         * StartWatching(base) -> BeforeStartWatchingChild -> BeforeStartWatching(base) -> StartWatchingChild -> AfterStartWatchingChild -> AfterStartWatching(base)
         */

        protected string COMPANY_NAME { get; set; }
        protected string COMPANY_URL { get; set; }

        protected MessageSender MESSAGE_SENDER { get; set; }
        protected ReservationInfo RESERVATION_INFO { get; set; }

        protected Counter COUNTER { get; set; }
        protected SurvivalSignal SURVIVAL_SIGNAL { get; set; }

        protected ChromeDriver DRIVER { get; set; }
        protected WebDriverWait WAIT { get; set; }

        /// <summary>
        /// 루프 인터벌(milliseconds)
        /// </summary>
        protected int LOOP_SLEEP_MS { get; set; }

        /// <summary>
        /// 최대 루프 실행시간(분)
        /// </summary>
        protected int LOOP_MINUTES { get; set; }

        /// <summary>
        /// 오픈런 실행시간
        /// </summary>
        protected string OPEN_TIME_HHMM { get; set; }

        /// <summary>
        /// 모니터링 종료
        /// </summary>
        protected bool STOP_WATCHING { get; set; }

        private readonly string SCRIPT_LIB = @"
return await(async () => {
    async function loadScript(src) {
        return new Promise((resolve, reject) => {
            const elem = document.createElement('script');
            elem.src = src;
            document.getElementsByTagName('body')[0].appendChild(elem);
            console.log('lib loading');

            elem.addEventListener('load', () => {
                if ($my != undefined) {
                    Notification.requestPermission();
                    resolve('Complete loading');
                }
                else {
                    resolve('Error loading : could not find lib');
                }
            });

            elem.addEventListener('error', (ev) => {
                reject(new Error(`Error loading lib: ${ev.message}`));
            });
        });
    }
    const res = await loadScript('" + System.Configuration.ConfigurationManager.AppSettings["LIB_URL"] + @"');
    return res;
})();
";

        public BaseModule(MessageSender msgSender, ReservationInfo reserveInfo, int loopSleepMs)
        {
            MESSAGE_SENDER = msgSender;
            RESERVATION_INFO = reserveInfo;

            COUNTER = new Counter();
            SURVIVAL_SIGNAL = new SurvivalSignal(msgSender);

            LOOP_SLEEP_MS = loopSleepMs;
            LOOP_MINUTES = 5;

            if (LOOP_SLEEP_MS <= 0)
            {
                throw new Exception("check loop sleep");
            }
        }

        protected abstract bool BeforeStartWatchingChild();
        private bool BeforeStartWatching()
        {
            bool result = BeforeStartWatchingChild();
            if (result == false)
            {
                Console.WriteLine("error [BeforeStartWatchingChild]");
                return false;
            }

            //lib로딩
            string resultLoadingLib = CheckLoadingLib();
            if (resultLoadingLib.Length > 0)
            {
                Console.WriteLine(resultLoadingLib);
                return false;
            }

#if DEBUG == false
            DRIVER.Manage().Window.Minimize();
#endif

            return true;
        }

        protected abstract bool StartWatchingChild();
        public void StartWatching()
        {
            string msgBase = $"{COMPANY_NAME} {RESERVATION_INFO.GetReserveSummary()}";
            MESSAGE_SENDER.Send($"start {msgBase}");

            try
            {
                DRIVER = new ChromeDriver(GetChromeOptions());

                WAIT = new WebDriverWait(DRIVER, TimeSpan.FromSeconds(30));
                WAIT.PollingInterval = TimeSpan.FromMilliseconds(100);
                //WAIT.IgnoreExceptionTypes(typeof(NoSuchElementException), typeof(InvalidOperationException));


                STOP_WATCHING = false;
                while (STOP_WATCHING == false)
                {
                    if (BeforeStartWatching())
                    {
                        DateTime dateLoopStart = DateTime.Now;
                        bool stopLoop = false;
                        while (stopLoop == false)
                        {
                            stopLoop = StartWatchingChild();

                            if (LOOP_MINUTES > 0 && dateLoopStart.AddMinutes(LOOP_MINUTES) < DateTime.Now)
                            {
                                //시간 경과시 루프 탈출, 새로고침 후 다시 시작
                                break;
                            }

                            COUNTER.IncreaseCounter();
                            SURVIVAL_SIGNAL.Send($"alive {COMPANY_NAME}, {COUNTER.GetCountPerSecond()} per sec, {RESERVATION_INFO.GetReserveSummary()}");

                            Thread.Sleep(LOOP_SLEEP_MS);
                        }
                        AfterStartWatching();
                    }
                    else
                    {
                        throw new Exception("error [BeforeStartWatching]");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                Console.WriteLine(ex.StackTrace);
                MESSAGE_SENDER.Send($"err {msgBase}");
                Util.WriteMsgThenWait("press any key to dispose driver");
            }
            finally
            {
                DRIVER.Dispose();
            }
        }

        protected abstract bool AfterStartWatchingChild();
        private bool AfterStartWatching()
        {
            bool result = AfterStartWatchingChild();
            return result;
        }

        public void StartOpenRun()
        {
            string msgBase = $"{COMPANY_NAME} {RESERVATION_INFO.GetReserveSummary()}";
            MESSAGE_SENDER.Send($"start {msgBase}");

            try
            {
                DRIVER = new ChromeDriver(GetChromeOptions());

                WAIT = new WebDriverWait(DRIVER, TimeSpan.FromSeconds(30));
                WAIT.PollingInterval = TimeSpan.FromMilliseconds(100);
                WAIT.IgnoreExceptionTypes(typeof(NoSuchElementException), typeof(InvalidOperationException));


                StartOpenRunChild();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                Console.WriteLine(ex.StackTrace);
                MESSAGE_SENDER.Send($"err {msgBase}");
            }
            finally
            {
                DRIVER.Dispose();
            }
        }

        protected abstract bool StartOpenRunChild();

        /// <summary>
        /// 로딩 성공시 공백 반환
        /// </summary>
        /// <returns></returns>
        protected string CheckLoadingLib()
        {
            string resultLoadingLib = (string)DRIVER.ExecuteScript(SCRIPT_LIB);
            return resultLoadingLib.StartsWith("Complete") ? "" : resultLoadingLib;
        }

        protected OpenQA.Selenium.Chrome.ChromeOptions GetChromeOptions()
        {
            // Chrome WebDriver 설정
            OpenQA.Selenium.Chrome.ChromeOptions chromeOptions = new OpenQA.Selenium.Chrome.ChromeOptions();
            //chromeOptions.AddArguments("--headless"); // 브라우저를 표시하지 않고 실행
            //chromeOptions.EnableMobileEmulation("Samsung Galaxy S20 Ultra");
            chromeOptions.AddUserProfilePreference("profile.default_content_setting_values.notifications", 1);

            return chromeOptions;
        }
    }

    public class Counter
    {
        private List<DateTime> lstDatetime { get; set; }

        public Counter()
        {
            lstDatetime = new List<DateTime>();
        }

        public void IncreaseCounter()
        {
            lstDatetime.Add(DateTime.Now);

            if (lstDatetime.Count > 10)
            {
                lstDatetime.RemoveAt(0);
            }
        }

        public decimal GetCountPerSecond()
        {
            decimal result = 0;

            if (lstDatetime.Count > 0)
            {
                decimal timeDiff = (decimal)(DateTime.Now - lstDatetime[0]).TotalMilliseconds;
                if (timeDiff > 0)
                {
                    decimal rate = lstDatetime.Count / (timeDiff * 1m / 1000);
                    result = Math.Round(rate, 2);
                }
            }

            return result;
        }
    }

    public class SurvivalSignal
    {
        private MessageSender MsgSender { get; set; }
        private int Interval { get; set; }
        private DateTime LastSend { get; set; }
        public SurvivalSignal(MessageSender msgSender, int intervalMinute = (2 * 60))
        {
            MsgSender = msgSender;
            Interval = intervalMinute * 60 * 1000;
            LastSend = DateTime.Now;
        }

        public async void Send(string msg)
        {
            DateTime dateNow = DateTime.Now;
            if (dateNow.Hour >= 8 && dateNow >= LastSend.AddMilliseconds(Interval))
            {
                await MsgSender.Send(msg);
                LastSend = dateNow;
            }
        }
    }
}
