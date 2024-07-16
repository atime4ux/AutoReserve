using OpenQA.Selenium;
using System;
using System.Linq;

namespace AutoReserve.Class.ReserveModule
{
    public class Peace : BaseMakeTicket
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="msgSender"></param>
        /// <param name="guest"></param>
        /// <param name="areaSeq">일반캠핑존A:1, 타프존:3, 오토캠핑:4</param>
        public Peace(MessageSender msgSender, ReservationInfo guest, int areaSeq = 4) : base(msgSender, guest, areaSeq)
        {
            COMPANY_NAME = "평화누리캠핑장";
            COMPANY_URL = "https://ggtour.or.kr/camping/main.web";

            USE_CAPTCHA = false;
            IDKEY = "5M4400";
            GD_SEQ = "GD123";
        }

        protected override bool BeforeStartWatchingChild()
        {
            base.InjectBeforeStartWatchingChild = () => {
                //팝업 닫기
                WAIT.Until(x => x.IsElementVisible(By.Id("logo")));
                while (DRIVER.WindowHandles.Count > 1)
                {
                    DRIVER.SwitchTo().Window(DRIVER.WindowHandles.Last());
                    DRIVER.Close();
                }
                DRIVER.SwitchTo().Window(DRIVER.WindowHandles.First());

                //신규탭으로 이동
                WAIT.Until(x => x.WindowHandles.Count > 1);
                DRIVER.Close();
                DRIVER.SwitchTo().Window(DRIVER.WindowHandles.Last());

                //카카오로그인 선택
                WAIT.Until(x => x.IsElementVisible(By.ClassName("btn-kakao")));
                DRIVER.FindElement(By.ClassName("btn-kakao")).Click();

                return true;
            };

            base.BeforeStartWatchingChild();

            return true;
        }
    }
}
