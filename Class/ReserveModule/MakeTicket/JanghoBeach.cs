using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;
using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;

namespace AutoReserve.Class.ReserveModule
{
    public class JanghoBeach : BaseMakeTicket
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="msgSender"></param>
        /// <param name="guest"></param>
        /// <param name="areaSeq">오토캠핑:3</param>
        public JanghoBeach(MessageSender msgSender, ReservationInfo guest, int areaSeq = 3) : base(msgSender, guest, areaSeq)
        {
            COMPANY_NAME = "장호비치캠핑장";
            COMPANY_URL = "https://forest.maketicket.co.kr/ticket/GD41";
            OPEN_TIME_HHMM = "1513";

            USE_CAPTCHA = true;
            IDKEY = "5M8190";
            GD_SEQ = "GD41";

            LOOP_MINUTES = 0;

            if (RESERVATION_INFO.LstSite.Count == 0)
            {
                RESERVATION_INFO.LstSite.AddRange(new string[] { "C9", "C8", "C7", "C6", "C5", "C4", "C3", "C2", "C1", "C17", "C16", "C15", "C14", "C13", "C12", "C11", "C10" });
            }
        }
    }
}
