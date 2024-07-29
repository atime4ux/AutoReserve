using Newtonsoft.Json;
using OpenQA.Selenium;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace AutoReserve.Class.ReserveModule
{
    public class Mangsang : BaseModule
    {
        public Mangsang(MessageSender msgSender, ReservationInfo reservationInfo) : base(msgSender, reservationInfo, 1000)
        {
            COMPANY_NAME = "망상오토캠핑리조트";
            COMPANY_URL = $"https://www.campingkorea.or.kr/reservation/06.htm";

            if (RESERVATION_INFO.LstSite.Count == 0)
            {
                RESERVATION_INFO.LstSite.AddRange(new string[] { "13", "12", "14", "4", "27" });
            }

            LOOP_MINUTES = 4;
        }

        protected override bool StartOpenRunChild()
        {
            return true;
        }

        protected override bool BeforeStartWatchingChild()
        {
            DRIVER.Navigate().GoToUrl(COMPANY_URL);
            WAIT.Until(x => x.IsElementVisible(By.Id("topmn")));

            string xpathLoginStateArea = "//*[@id='topmn']//a[1]";
            if (DRIVER.FindElement(By.XPath(xpathLoginStateArea)).Text == "로그인")
            {
                //로그인 화면으로 이동
                DRIVER.Navigate().GoToUrl("https://www.campingkorea.or.kr/member/login.htm");

                //페이지 로딩 확인
                WAIT.Until(x => x.IsElementVisible(By.Id("topmn")));

                //로그인 정보
                Dictionary<string, string> dicLoginInfo = new Dictionary<string, string>();
                dicLoginInfo.Add("atime4ux", System.Text.Encoding.UTF8.GetString(Convert.FromBase64String("bGNvMmd1c3Rqcg==")));

                //id 입력 대기
                for (int i = 0; i < 9; i++)
                {
                    try
                    {
                        WAIT.Until(x => DRIVER.FindElement(By.XPath(xpathLoginStateArea)).Text == "로그아웃" || dicLoginInfo.Any(y => y.Key == x.FindElement(By.Id("userid")).GetAttribute("value")));

                        //id입력되면 비밀번호 입력 후 로그인
                        string userId = DRIVER.FindElement(By.Id("userid")).GetAttribute("value");
                        DRIVER.FindElement(By.Id("passwd")).SendKeys(dicLoginInfo[userId]);
                        DRIVER.FindElement(By.XPath("//*[contains(@class, 'btn_login')]")).Click();
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
                WAIT.Until(x => x.IsElementVisible(By.Id("topmn")) && x.FindElement(By.XPath(xpathLoginStateArea)).Text == "로그아웃");

                //예약화면으로 이동
                DRIVER.Navigate().GoToUrl(COMPANY_URL);
            }

            WAIT.Until(x => x.IsElementVisible(By.XPath("//*[@id='snb2m5']//a[contains(@class, 'on')]")));

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
                    DRIVER.ExecuteScript($"macro_modal(`./06_01.htm?type=car&today={RESERVATION_INFO.ReservationDate.ToString("yyyy-MM-dd")}&col=5`);");
                    //보안문자 처리
                    WAIT.Until(x => x.IsElementVisible(By.Id("jepgimg")));

                    int captchaRetry = 0;
                    try
                    {
                        while (captchaRetry < 3)
                        {
                            string parsedCaptcha = (string)DRIVER.ExecuteScript(getParseCaptchScript());
                            DRIVER.FindElement(By.Id("auth_name")).SendKeys(parsedCaptcha);
                            break;
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"err parseing captcha\r\n{ex.Message}");
                        captchaRetry++;
                    }

                    if (captchaRetry == 0)
                    {
                        Thread.Sleep(3000);
                    }
                    DRIVER.ExecuteScript("$('#macro_pop_layer').next().find('button:first').click()");

                    //자리 선택 화면 로딩 확인
                    WAIT.Until(x => x.IsElementVisible(By.XPath("//*[@class='join_process']//li[1][contains(@class, 'on')]")));

                    //자리 선점
                    CheckLoadingLib();//페이지 전환되었으므로 lib 재적용
                    DRIVER.ExecuteScript(getProcReserveScript(resCheckSeat[0]));

                    WAIT.Until(x => x.IsElementVisible(By.XPath("//*[@class='join_process']//li[2][contains(@class, 'on')]")));
                    DateTime deadLine = DateTime.Now.AddMinutes(4);

                    MESSAGE_SENDER.Send($"waiting for pay until {deadLine.ToString("HH:mm")} {COMPANY_NAME} {RESERVATION_INFO.ReservationDate.ToString("yyyy-MM-dd")}");

                    string agreeChecked = null;
                    while (string.IsNullOrEmpty(agreeChecked))
                    {
                        try
                        {
                            Thread.Sleep(1000);
                        }
                        catch (Exception ex)
                        { }

                        agreeChecked = (string)DRIVER.ExecuteScript("return $('#res_Check').is(':checked') ? 'Y' : ''");
                        if (deadLine < DateTime.Now && string.IsNullOrEmpty(agreeChecked))
                        {
                            //시간이 흘러도 결제 진행 안함 뒤로 이동해서 다시 선점
                            break;
                        }
                    }

                    if (agreeChecked == "Y")
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
window.alert = (a)=>console.log(a);
window.confirm = (a)=>{console.log(a); return true;};

return await(async () => {
    const reserveDate = '" + RESERVATION_INFO.ReservationDate.ToString("yyyy-MM-dd") + @"';
    const nightCnt = " + RESERVATION_INFO.NightCnt + @";
    const arrSiteId = [" + string.Join(",", RESERVATION_INFO.LstSite.Select(x => $"{x}")) + @"];
    const companyUrl = '" + COMPANY_URL + @"';

    //$.ajaxSetup({ 'beforeSend': function (xhr) { xhr.overrideMimeType('text/html; charset=euc-kr'); } });

    const siteMapUrl = `https://www.campingkorea.or.kr/reservation/06_01_img.htm?res_Day=${reserveDate}&room_Code=car&site_date=${nightCnt}&click=`;

    const fnCheck = async () => {
        const siteMapResult = (await $.get(siteMapUrl)).replaceAll('\r\n', '').replaceAll('\t', '');
        const $siteMapResult = $(siteMapResult);
        const arrEnabledSite = arrSiteId.filter(e => $siteMapResult.find(`.place${e} span.c6`).hasClass('e_end') == false && $siteMapResult.find(`.place${e} span.c6`).hasClass('r_ing') == false);
        if (arrEnabledSite.length > 0) {
            const msg = `예약풀림 " + COMPANY_NAME + @" ${reserveDate} ${arrEnabledSite[0]}`;

            $my().AlertNoti(msg, companyUrl);
            $my().AlertSlack(msg, companyUrl);
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

        private string getParseCaptchScript()
        {
            string script = @"
return await $my().ParseCaptcha($my('#jepgimg'));
";
            return script;
        }

        private string getProcReserveScript(int siteId)
        {
            string script = @"
await(async(siteId) => {
    const nightCnt = " + RESERVATION_INFO.NightCnt + @";
    
    $('#mapimg').html('');
    $(':input[name=res_For]').val(nightCnt);
    Calculate();
    while ($('#mapimg a').length == 0) {
        //비동기 처리 될때까지 대기
        await $my().SleepThread(100);
    }


    const selectSiteScript = $(`#mapimg .place${siteId}`).attr('href');
    if (selectSiteScript.indexOf('이미 예약된') > 0) {
        //자리 없어짐
        return false;
    }

    $('#mapimg').html('');
    eval(selectSiteScript);
    while ($('#mapimg a').length == 0) {
        //비동기 처리 될때까지 대기
        await $my().SleepThread(100);
    }

    if ($(`#mapimg .place${siteId} .on`).length == 0) {
        //자리 없어짐
        return false;
    }

    document.res_form01.submit();
})('" + siteId + @"')
";
            return script;
        }
    }
}
