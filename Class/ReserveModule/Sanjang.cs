using Newtonsoft.Json;
using OpenQA.Selenium;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace AutoReserve.Class.ReserveModule
{
    /// <summary>
    /// 진행중
    /// </summary>
    public class Sanjang : BaseModule
    {
        public Sanjang(MessageSender msgSender, ReservationInfo reservationInfo) : base(msgSender, reservationInfo, 1000)
        {
            COMPANY_NAME = "산장관광지";
            COMPANY_URL = "https://tickets.interpark.com/goods/22016999";

            LOOP_SLEEP_MS = 750;

            if (RESERVATION_INFO.LstSite.Count == 0)
            {
                RESERVATION_INFO.LstSite.AddRange(new string[] { "캠핑장B(특대형)-2", "캠핑장B(특대형)-3", "캠핑장B(대형)-4", "캠핑장B(대형)-3" });
            }
        }

        protected override bool StartOpenRunChild()
        {
            return true;
        }

        protected override bool BeforeStartWatchingChild()
        {
            //인터파크 예약 정보 확인하여 없으면 로그인부터 진행
            bool existSteInfo = false;
            if (existSteInfo == false)
            {
                DRIVER.Navigate().GoToUrl(COMPANY_URL);
                WAIT.Until(x => x.IsElementVisible(By.Id("container")));

                CheckLoadingLib();

                string xpathLoginStateArea = "(//a[starts-with(@class, 'header_menu')])[1]";
                if (DRIVER.FindElement(By.XPath(xpathLoginStateArea)).Text == "로그인")
                {
                    //로그인 화면으로 이동
                    DRIVER.FindElement(By.XPath(xpathLoginStateArea)).Click();

                    //페이지 로딩 확인
                    WAIT.Until(x => x.IsElementVisible(By.Id("userId")));

                    //로그인 정보
                    Dictionary<string, string> dicLoginInfo = new Dictionary<string, string>();
                    dicLoginInfo.Add("atime4ux", System.Text.Encoding.UTF8.GetString(Convert.FromBase64String("bGNvMmd1c3Rqcg==")));
                    dicLoginInfo.Add("jjin00hs", System.Text.Encoding.UTF8.GetString(Convert.FromBase64String("bGp5NDU5MGhzIQ==")));

                    //id 입력 대기
                    for (int i = 0; i < 9; i++)
                    {
                        try
                        {
                            WAIT.Until(x => dicLoginInfo.Any(y => y.Key == x.FindElement(By.Id("userId")).GetAttribute("value")));

                            //id입력되면 비밀번호 입력 후 로그인
                            string userId = DRIVER.FindElement(By.Id("userId")).GetAttribute("value");
                            DRIVER.FindElement(By.Id("userPwd")).SendKeys(dicLoginInfo[userId]);
                            DRIVER.FindElement(By.Id("saveSess")).Click();
                            DRIVER.FindElement(By.Id("btn_login")).Click();
                            break;
                        }
                        catch (Exception ex)
                        {
                            if (DRIVER.FindElement(By.XPath(xpathLoginStateArea)).Text == "로그아웃")
                            {
                                //다른아이디로 로그인 한 경우
                                break;
                            }

                            if (i == 9)
                            {
                                throw new Exception("could not find login state");
                            }
                        }
                    }

                    //페이지 로딩 확인 - 로그인 여부 확인
                    WAIT.Until(x => x.IsElementVisible(By.XPath(xpathLoginStateArea)) && x.FindElement(By.XPath(xpathLoginStateArea)).Text == "로그아웃");
                }

                //날짜선택
                //기간선택
                //예매하기 선택
                //팝업 생성확인
                //부모창 닫기
                //팝업으로 스위치
            }
            else
            {
                //있으면 새로고침
                DRIVER.Navigate().Refresh();
            }

            return true;
        }

        protected override bool StartWatchingChild()
        {
            int[] resCheckSeat = new int[] { };
            string jsonRes = (string)DRIVER.ExecuteScript(getCheckSeatScript());
            resCheckSeat = JsonConvert.DeserializeObject<int[]>(jsonRes);

            if (resCheckSeat == null)
            {
                //오류 발생시 sleep 후 루프 재시작
                Thread.Sleep(60 * 1000);
            }
            else
            {
                if (resCheckSeat.Length > 0)
                {
                    //자리 선점
                    DRIVER.ExecuteScript(getProcReserveScript(resCheckSeat[0]));
                    WAIT.Until(x => x.IsElementVisible(By.Id("body_title")) && x.FindElement(By.XPath("//*[@id='body_title']//h1[1]")).Text == "예약신청 및 결제");
                    DateTime deadLine = DateTime.Now.AddMinutes(5);

                    MESSAGE_SENDER.Send($"waiting for pay until {deadLine.ToString("HH:mm")} {COMPANY_NAME} {RESERVATION_INFO.ReservationDate.ToString("yyyy-MM-dd")}");

                    string rCar1 = null;
                    while (string.IsNullOrEmpty(rCar1))
                    {
                        try
                        {
                            Thread.Sleep(1000);
                        }
                        catch (Exception ex)
                        { }

                        rCar1 = (string)DRIVER.ExecuteScript("return $(`:input[name=r_car1]`).val()");
                        if (deadLine < DateTime.Now && string.IsNullOrEmpty(rCar1))
                        {
                            //시간이 흘러도 결제 진행 안함 뒤로 이동해서 다시 선점
                            break;
                        }
                    }

                    if (rCar1.Length > 0)
                    {
                        Console.WriteLine("processing pay");
                        Console.ReadKey();

                        //watching, loop 중단
                        base.STOP_WATCHING = true;
                        return true;
                    }
                }
            }

            return false;
        }

        protected override bool AfterStartWatchingChild()
        {
            return true;
        }

        private string getCheckSeatScript()
        {
            string script = @"
return await(async () => {
    const reserveDate = '" + RESERVATION_INFO.ReservationDate.ToString("yyyy-MM-dd") + @"';
    const nightCnt = " + RESERVATION_INFO.NightCnt + @";
    const arrSiteId = [" + string.Join(",", RESERVATION_INFO.LstSite.Select(x => $"{x}")) + @"];

    $.ajaxSetup({ 'beforeSend': function (xhr) { xhr.overrideMimeType('text/html; charset=euc-kr'); } });

    const siteMapUrl = `https://www.chuamautocamping.or.kr/reservation/02_02_site_img.php?res_Day=${reserveDate}&room_Code=car&site_date=${nightCnt}&click=none`;

    const fnCheck = async () => {
        const siteMapResult = (await $.get(siteMapUrl)).replaceAll('\r\n', '').replaceAll('\t', '');
        const arrEnabledSite = arrSiteId.filter(e => siteMapResult.indexOf(`dc${e}.png`) > 0);
        if (arrEnabledSite.length > 0) {
            const msg = `예약풀림 " + COMPANY_NAME + @" ${reserveDate} ${arrEnabledSite[0]}`;

            $my().AlertNoti(msg, 'https://www.chuamautocamping.or.kr/reservation/01.htm');
            $my().AlertSlack(msg, 'https://www.chuamautocamping.or.kr/reservation/01.htm');

            const targetUrl = `https://www.chuamautocamping.or.kr/reservation/02_02.htm?type=car&today=${reserveDate}&col=4`;
            const checkDateResult = await $.get(targetUrl);

            //페이지 이동 대신 소스 치환 사용
            history.pushState({}, '', targetUrl);

            try {
                document.getElementsByTagName('html')[0].innerHTML = checkDateResult;
            }
            catch (err) {
                return null;
            }
        }
        else {
            const msg = `예약잠김 date:${reserveDate}, ${(new Date()).ToString('HH:mm:ss')} " + COUNTER.GetCountPerSecond() + @"`
            document.title = msg;
        }

        return arrEnabledSite;
    };

    return JSON.stringify(await fnCheck());
})();
";
            return script;
        }

        private string getProcReserveScript(int siteId)
        {
            string script = @"
((siteId) => {
    const nightCnt = " + RESERVATION_INFO.NightCnt + @";
    const userCnt = 4;//인원수
    const elemInput = document.createElement('input');
    elemInput.setAttribute('type', 'hidden');
    elemInput.setAttribute('name', 'r_site');
    elemInput.setAttribute('value', siteId);
    document.getElementsByName('res_form01')[0].appendChild(elemInput);
    console.log('before site id', document.res_form01.r_site.value);
    console.log('after site id', document.res_form01.r_site.value);

    $(`:input[name='res_For'][value='${nightCnt}']`).prop('checked', true);
    $('#res_Many').val(userCnt);

    document.res_form01.submit();
})('" + siteId + @"')
";
            return script;
        }
    }
}
