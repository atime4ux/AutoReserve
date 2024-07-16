using OpenQA.Selenium;
using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;

namespace AutoReserve.Class.ReserveModule
{
    public abstract class BaseMakeTicket : BaseModule
    {
        protected string IDKEY { get; set; }
        protected string GD_SEQ { get; set; }

        protected string SD_DATE { get; set; }
        protected string RI_AREA_CODE { get; set; }
        protected string SD_SEQ { get; set; }

        protected bool USE_CAPTCHA = false;

        protected bool FLAG_SUSPEND = false;

        protected int AREA_SEQ { get; set; }

        public BaseMakeTicket(MessageSender msgSender, ReservationInfo guest, int areaSeq) : base(msgSender, guest, 2500)
        {
            AREA_SEQ = areaSeq;

            if (AREA_SEQ <= 0)
            {
                throw new Exception("check area seq");
            }
        }

        protected override bool StartOpenRunChild()
        {
            MESSAGE_SENDER.Send($"start {COMPANY_NAME} {RESERVATION_INFO.GetReserveSummary()}");

            try
            {
                DRIVER.Navigate().GoToUrl(COMPANY_URL);

                //캡차 대기 -달력 출력 확인
                for (int i = 0; i < 10; i++)
                {
                    try
                    {
                        WAIT.Until(x => x.IsElementVisible(By.Id($"calendar_28")) || x.IsElementVisible(By.Id($"calendar_29")) || x.IsElementVisible(By.Id($"calendar_30")) || x.IsElementVisible(By.Id($"calendar_31")));
                        break;
                    }
                    catch (Exception ex)
                    {
                        if (i == 9)
                        {
                            throw new Exception("could not find calendar");
                        }
                    }
                }

                //lib로딩
                string resultLoadingLib = base.CheckLoadingLib();
                if (resultLoadingLib.Length > 0)
                {
                    Console.WriteLine(resultLoadingLib);
                    return false;
                }

                //스크립트 테스트
                int remainCnt = 0;
                string resCheckSeat = (string)DRIVER.ExecuteScript(getCheckSeatScript());
                if (resCheckSeat == "err")
                {
                    remainCnt = -100;
                }
                else if (resCheckSeat == "not open")
                {
                    remainCnt = -1;
                }
                else
                {
                    string[] arrParam = resCheckSeat.Split('(')[1].Split(',').Select(x => Regex.Replace(x, "[^a-zA-Z0-9]", "")).ToArray();
                    SD_DATE = arrParam[0];
                    RI_AREA_CODE = arrParam[1];
                    SD_SEQ = arrParam[2];
                    remainCnt = Convert.ToInt32(arrParam[3]);
                }

                if (remainCnt < -1)
                {
                    Console.WriteLine("error check seat");
                    return false;
                }


                //오픈 3초전부터 스크립트 실행
                DateTime dateOpen = DateTime.ParseExact(DateTime.Now.ToString($"yyyyMMdd{OPEN_TIME_HHMM}00"), "yyyyMMddHHmmss", System.Globalization.CultureInfo.InvariantCulture);
                while ((dateOpen - DateTime.Now).TotalSeconds > 3)
                {
                    Thread.Sleep(1000);
                    Console.WriteLine($"open in {(dateOpen - DateTime.Now).TotalSeconds} seconds, {COMPANY_NAME}, {RESERVATION_INFO.ReservationDate}, {RESERVATION_INFO.NightCnt} night");
                }

                bool calendarOpen = remainCnt > 0 ? true : false;
                while (calendarOpen == false)
                {
                    Thread.Sleep(100);

                    resCheckSeat = (string)DRIVER.ExecuteScript(getCheckSeatScript());
                    if (resCheckSeat == "err")
                    {
                        remainCnt = -100;
                    }
                    else if (resCheckSeat == "not open")
                    {
                        remainCnt = -1;
                    }
                    else
                    {
                        string[] arrParam = resCheckSeat.Split('(')[1].Split(',').Select(x => Regex.Replace(x, "[^a-zA-Z0-9]", "")).ToArray();
                        SD_DATE = arrParam[0];
                        RI_AREA_CODE = arrParam[1];
                        SD_SEQ = arrParam[2];
                        remainCnt = Convert.ToInt32(arrParam[3]);
                    }

                    if (remainCnt < -1)
                    {
                        Console.WriteLine("error check seat");
                        return false;
                    }
                    else if (remainCnt > 0)
                    {
                        calendarOpen = true;
                    }
                }

                Console.WriteLine("start open run");

                if (remainCnt > 0)
                {
                    DRIVER.ExecuteScript(getMoveReservationScript());

                    if (USE_CAPTCHA)
                    {
                        WAIT.Until(x => x.IsElementVisible(By.Id("catpcha")));
                    }

                    //자리 선점
                    string reserveResult = (string)DRIVER.ExecuteScript(getProcReserveScript());
                    if (reserveResult == "success")
                    {
                        Console.WriteLine("processing pay");
                        Console.ReadKey();
                    }
                    else
                    {
                        Console.WriteLine("fail to open run");
                        Console.ReadKey();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                Console.WriteLine(ex.StackTrace);
            }

            return false;
        }

        protected Func<bool> InjectBeforeStartWatchingChild { get; set; }

        protected override bool BeforeStartWatchingChild()
        {
            if (string.IsNullOrEmpty(SD_DATE))
            {
                DRIVER.Navigate().GoToUrl(COMPANY_URL);

                if (InjectBeforeStartWatchingChild != null)
                {
                    InjectBeforeStartWatchingChild();
                }

                //페이지 로딩 확인 - 달력 출력 확인
                for (int i = 0; i < 10; i++)
                {
                    try
                    {
                        WAIT.Until(x => x.IsElementVisible(By.Id($"calendar_28")) || x.IsElementVisible(By.Id($"calendar_29")) || x.IsElementVisible(By.Id($"calendar_30")) || x.IsElementVisible(By.Id($"calendar_31")));
                        break;
                    }
                    catch (Exception)
                    {
                        if (i == 9)
                        {
                            throw new Exception("could not find calendar");
                        }
                    }
                }
            }
            else
            {
                DRIVER.Navigate().Refresh();
            }

            return true;
        }

        protected override bool StartWatchingChild()
        {
            //빈자리 체크, 빈자리 발생시 submit하여 자리 선점 진행
            int remainCnt = 0;

            string resCheckSeat = (string)DRIVER.ExecuteScript(getCheckSeatScript());
            if (resCheckSeat == "err")
            {
                FLAG_SUSPEND = true;
                return false;
            }
            else if (resCheckSeat == "not open")
            {
                throw new Exception("target date is closed");
            }
            else
            {
                string[] arrParam = resCheckSeat.Split('(')[1].Split(',').Select(x => Regex.Replace(x, "[^a-zA-Z0-9]", "")).ToArray();
                SD_DATE = arrParam[0];
                RI_AREA_CODE = arrParam[1];
                SD_SEQ = arrParam[2];
                remainCnt = Convert.ToInt32(arrParam[3]);
            }

            if (remainCnt > 0)
            {
                DRIVER.ExecuteScript(getMoveReservationScript());

                if (USE_CAPTCHA)
                {
                    WAIT.Until(x => x.IsElementVisible(By.Id("catpcha")));
                }

                //17분 대기 후 다시 루프 시작
                DateTime deadLine = DateTime.Now.AddMinutes(17);

                //자리 선점
                string reserveResult = (string)DRIVER.ExecuteScript(getProcReserveScript());
                if (reserveResult == "success")
                {
                    IWebElement btnNext;
                    while (true)
                    {
                        try
                        {
                            btnNext = DRIVER.FindElement(By.Id("btn_div_area_next"));
                            if (btnNext.GetAttribute("class").Split(' ').Any(x => x == "on"))
                            {
                                break;
                            }
                        }
                        catch (Exception ex)
                        { }

                        Thread.Sleep(100);
                    }

                    MESSAGE_SENDER.Send($"waiting for pay until {deadLine.ToString("HH:mm")} {COMPANY_NAME} {RESERVATION_INFO.ReservationDate.ToString("yyyy-MM-dd")}");

                    string curStep = (string)DRIVER.ExecuteScript("return $('.step .on').text()");
                    while (curStep.StartsWith("02"))
                    {
                        try
                        {
                            Thread.Sleep(1000);
                        }
                        catch (Exception ex)
                        { }

                        if (deadLine < DateTime.Now && curStep.StartsWith("02"))
                        {
                            //시간이 흘러도 결제 진행 안함 뒤로 이동해서 다시 선점
                            break;
                        }

                        curStep = (string)DRIVER.ExecuteScript("return $('.step .on').text()");
                    }

                    if (curStep.StartsWith("02") == false)
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
            if (FLAG_SUSPEND)
            {
                //오류 발생
                int suspendSec = 60 * 60;//1시간 대기
                MESSAGE_SENDER.Send($"suspend for {suspendSec}sec");
                while (suspendSec > 0)
                {
                    Console.WriteLine($"suspend {suspendSec}sec left");
                    Thread.Sleep(1000);
                    suspendSec--;
                }
            }

            FLAG_SUSPEND = false;

            return true;
        }

        protected string getCheckSeatScript()
        {
            string checkSeatScript = @"
return await(async () => {
    const title = document.title;
    const targetDate = '" + RESERVATION_INFO.ReservationDate.ToString("yyyyMMdd") + @"';

    const res = await $.post('https://forest.maketicket.co.kr/event/event.do?command=event_view_m_area_info', {
        idkey: '" + IDKEY + @"',
        gd_seq: '" + GD_SEQ + @"',
        sd_date: targetDate,
        view_type: 'new'
    });

    const $res = $(res);
    const $seatType = $res.find('li:eq(" + (AREA_SEQ - 1) + @")');
    if (res.indexOf('구역정보가 없습니다.') > 0) {
        return 'not open';
    } else if ($seatType.length > 0) {
        const $label = $seatType.find('label');
        const remainCnt = parseInt($label.text().match(/\d+/g).join(''));
        if (remainCnt == 0) {
            const msg = `예약잠김 date:${targetDate}, ${(new Date()).ToString('HH:mm:ss')} " + COUNTER.GetCountPerSecond() + @"`
            document.title = msg;
        }

        const strScript = $seatType.attr('onclick');
        return strScript;
    }
    else {
        console.log('parsing error', res);

        var alertMsg = `${targetDate} " + COMPANY_NAME + @" 오류 발생`;
        $my().AlertNoti(alertMsg);
        $my().AlertSlack(alertMsg);

        return 'err';
    }
})();
";

            return checkSeatScript;
        }

        protected string getMoveReservationScript()
        {
            string moveReservationScript = @"
(() => {
    const alertMsg = `${'" + RESERVATION_INFO.ReservationDate.ToString("yyyyMMdd") + @"'} " + COMPANY_NAME + @" 빈자리 발생`;

    $my().AlertNoti(alertMsg);
    $my().AlertSlack(alertMsg, 'https://forest.maketicket.co.kr/ticket/" + GD_SEQ + @"');

    history.pushState({}, '', 'https://forest.maketicket.co.kr/ticket/" + GD_SEQ + @"');

    const form = document.createElement('form');
    form.method = 'POST';
    form.action = '/event/event.do';

    const command = document.createElement('input');
    command.type = 'hidden';
    command.name = 'command';
    command.value = 'reserve_area';
    form.appendChild(command);

    const idkey = document.createElement('input');
    idkey.type = 'hidden';
    idkey.name = 'idkey';
    idkey.value = '" + IDKEY + @"';
    form.appendChild(idkey);

    const gd_seq = document.createElement('input');
    gd_seq.type = 'hidden';
    gd_seq.name = 'gd_seq';
    gd_seq.value = '" + GD_SEQ + @"';
    form.appendChild(gd_seq);

    const sd_date = document.createElement('input');
    sd_date.type = 'hidden';
    sd_date.name = 'sd_date';
    sd_date.value = '" + SD_DATE + @"';
    form.appendChild(sd_date);

    const ri_area_code = document.createElement('input');
    ri_area_code.type = 'hidden';
    ri_area_code.name = 'ri_area_code';
    ri_area_code.value = '" + RI_AREA_CODE + @"';
    form.appendChild(ri_area_code);

    const sd_seq = document.createElement('input');
    sd_seq.type = 'hidden';
    sd_seq.name = 'sd_seq';
    sd_seq.value = '" + SD_SEQ + @"';
    form.appendChild(sd_seq);

    document.body.appendChild(form);

    form.submit();
})();
";
            return moveReservationScript;
        }

        protected string getProcReserveScript()
        {
            string procReserveScript = @"
return await(async () => {
    const TimerPromise = (fn, ms) => {
        return new Promise((resolve, reject) => {
            setTimeout(() => {
                const res = fn();
                resolve(res);
            }, ms);
        });
    };

    const SleepThread = async (ms) => {
        await TimerPromise(() => { }, ms);
    };

    const reserveWIthCaptchaYn = '" + (USE_CAPTCHA ? "Y" : "N") + @"';

    window.alert = (e) => { console.log('alert', e) };


    if(reserveWIthCaptchaYn == 'Y') {
        const $img = $('#catpcha img');
        console.log($img);
        let imgLoaded = $img[0].complete;
        $img.on('load', function () {
            imgLoaded = true;
        });

        for (let i = 0; i < 20; i++) {
            // 이미지가 로드될 때까지 대기
            if (imgLoaded == true) {
                break;
            }

            console.log(`waiting for loading catpcha img ${i + 1}`);
            await SleepThread(100);
        }


        const capchaImg = $img.get()[0];
        const canvas = document.createElement('canvas');
        canvas.width = capchaImg.width;
        canvas.height = capchaImg.height;
        const ctx = canvas.getContext('2d');
        ctx.drawImage(capchaImg, 0, 0);
        const base64String = canvas.toDataURL('image/png');

        const resOCR = await $.post('https://api.ocr.space/parse/image', {
            apikey: '8554fdc64988957',
            base64Image: base64String,
            OCREngine: 2,
            filetype: 'PNG',
            detectOrientation: false,
            isTable: false
        });

        const parsedText = resOCR?.ParsedResults[0]?.ParsedText;
        if (parsedText) {
            const capchaNumber = parsedText.match(/\d+/g)[0];
            if (capchaNumber.length == 4) {
                $('#answer').val(capchaNumber);
                $('#frmSubmit').click();
            }
        }
    }

    let nightCnt = " + RESERVATION_INFO.NightCnt + @";
    var sitePriority = [" + string.Join(",", RESERVATION_INFO.LstSite.Select(x => $"'{x}'")) + @"];

    $('#lodge_day_div').click();
    $('#lodge_day_div').next('ul').find(`li[data-value=${nightCnt}]`).click();

    while (true) {
        if ($('#marker_area li').length > 0) {
            var arrMarker = $('#marker_area li').get().map(e => {
                return {
                    id: $(e).attr('id'),
                    left: parseInt($(e).css('left').replace('px', '')),
                    $a: $(e).find('a:first')
                };
            }).filter(e => e.$a.hasClass('dis') == false).sort((a, b) => {
                var idxA = sitePriority.indexOf(a.$a.text());
                idxA = idxA < 0 ? a.left : idxA;
                var idxB = sitePriority.indexOf(b.$a.text());
                idxB = idxB < 0 ? b.left : idxB;

                return idxB - idxA;
            });

            if (arrMarker.length > 0) {
                var arrHref = arrMarker[0].$a.attr('href').split(':');
                var strFunc = arrHref.length > 0 ? arrHref[1] : arrHref[0];

                //자리 선택 스크립트 실행
                eval(strFunc);

                if (arrMarker[0].$a.hasClass('sel')) {
                    //선택 성공하면 루프 중단, 실패시 다시 루프
                    return 'success';
                }
            }
            else {
                if (nightCnt > 1) {
                    //1박 줄여서 다시 확인
                    nightCnt--

                    $('#lodge_day_div').click();
                    $('#lodge_day_div').next('ul').find(`li[data-value=${nightCnt}]`).click();
                }
                else {
                    //선택 가능한 자리가 없음
                    return 'fail';
                }
            }
        }

        await SleepThread(10);
    }
})();
";
            return procReserveScript;
        }
    }
}
